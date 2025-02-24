using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DolphinMovement : MonoBehaviour, ITrigger
{
    [SerializeField] public IdleBehaviour idleBehaviour;

    [SerializeField] private GameObject gameObjectWithInputHandlers;
    //must be child of object with this script
    [SerializeField] private GameObject dolphinObject;
    [SerializeField] private float horizontalMovementSpeed = 5;
    [SerializeField] private float verticalMovementSpeed = 5;
    [SerializeField] private float horizontalMovementLimit = 20;
    [SerializeField] private float verticalMovementLimit = 20;
    [SerializeField, Range(0, 1)] private float steeringDeadzone = 0.2f;
    [SerializeField] private float raycastAngle = 45;
    [SerializeField] private float raycastLength = 5;
    [SerializeField] private float raycastFarLength = 6;
    [SerializeField] private float speedMultiplier = 2;
    
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField, Range(0, 90)] private float maxBankAngle = 30f;
    [SerializeField, Range(0, 90)] private float maxPitchAngle = 20f;
    [SerializeField] private float rotationAnglePerFrame = 0.05f;

    public delegate void OnPlayerMovement(TrackSide horizontal, TrackSide vertical);
    public static OnPlayerMovement OnPlayerMoved;

    private Quaternion targetRotation;
    private float horizontalInput;
    private float verticalInput;

    private Vector3 leftVector;
    private Vector3 rightVector;
    private Vector3 upVector;
    private Vector3 downVector;

    private bool overrideMovement = false;

    private List<IInputHandler> availableInputHandlers;
    private IInputHandler desiredInputHandler;
    private CartController cartController;

    // Monobehaviour Methods
    private void Start()
    {
        availableInputHandlers = gameObjectWithInputHandlers.GetComponentsInChildren<IInputHandler>().ToList();
        cartController = FindObjectOfType<CartController>();
    }

    private void OnEnable() {
        StartCoroutine(RotateDolphin());    
    }

    void Update()
    {
        CheckForAvailableInputHandler();

        //gets input
        GetInput();

        //changes horizonatal input or verticalinput if the raycasts hit somethings
        LockMovement();
        AvoidCollisions();

        //moves the player acording to the inputs
        Movement();
    }

    // Public Methods
    public void TriggerEvent(object parameters)
    {
        if (parameters.GetType() != typeof(bool))
        {
            return;
        }

        bool state = (bool)parameters;
        overrideMovement = state;
    }

    public void ToggleMovement(bool _enabled)
    {
        overrideMovement = !_enabled;
    }

    public void GetInput()
    {
        if ((idleBehaviour != null && idleBehaviour.IsIdle) || overrideMovement)
        {
            horizontalInput = 0;
            verticalInput = 0;
        }
        else
        {
            horizontalInput = desiredInputHandler.GetXMovement();
            verticalInput = desiredInputHandler.GetYMovement();
        }

        leftVector = Quaternion.AngleAxis(-60, transform.up) * transform.forward;
        rightVector = Quaternion.AngleAxis(60, transform.up) * transform.forward;
        upVector = Quaternion.AngleAxis(-raycastAngle, transform.right) * transform.forward;
        downVector = Quaternion.AngleAxis(raycastAngle, transform.right) * transform.forward;

        TrackSide trackSideVertical;
        TrackSide trackSideHorizontal;

        if (horizontalInput < -steeringDeadzone)
        {
            trackSideHorizontal = TrackSide.left;
        }
        else if (horizontalInput > steeringDeadzone)
        {
            trackSideHorizontal = TrackSide.right;
        }
        else
        {
            trackSideHorizontal = TrackSide.neutral;
        }

        if (verticalInput < -steeringDeadzone)
        {
            trackSideVertical = TrackSide.up;
        }
        else if (verticalInput > steeringDeadzone)
        {
            trackSideVertical = TrackSide.down;
        }
        else
        {
            trackSideVertical = TrackSide.neutral;
        }
        cartController.SetDirection(trackSideVertical, trackSideHorizontal);

        if (verticalInput != 0 || horizontalInput != 0)
        {
            OnPlayerMoved?.Invoke(trackSideHorizontal, trackSideVertical);
        }        
    }

    // Private Methods
        //Arduino is priority so you can only play with keyboard when arduino is NOT connected.
    private void CheckForAvailableInputHandler()
    {
        foreach (IInputHandler inputHandler in availableInputHandlers)
        {
            bool isConnected = inputHandler.CheckIfConnected();
            if (isConnected)
            {
                desiredInputHandler = inputHandler;
                break;
            }
        }
    }


    private void Movement()
    {
        //Add yaw pitch and roll to the dolphin
        float targetYaw = Mathf.Lerp(0, maxPitchAngle, Mathf.Abs(horizontalInput)) * Mathf.Sign(horizontalInput);
        float targetPitch = Mathf.Lerp(0, maxPitchAngle, Mathf.Abs(verticalInput)) * Mathf.Sign(verticalInput);
        float targetRoll = Mathf.Lerp(0, maxBankAngle, Mathf.Abs(horizontalInput)) * -Mathf.Sign(horizontalInput);

        //Move the dolphin
        if (idleBehaviour != null && idleBehaviour.IsIdle)
        {
            transform.localPosition = Vector3.zero;
        }
        else
        {
            transform.localPosition += new Vector3(horizontalInput * Time.deltaTime * horizontalMovementSpeed, (verticalInput * -1f) * Time.deltaTime * verticalMovementSpeed, 0);
        }

        // Rotate Dolphin
        targetRotation = Quaternion.Euler(Vector3.up * targetYaw + Vector3.right * targetPitch + Vector3.forward * targetRoll);
    }

    private void AvoidCollisions()
    {
        Ray downRay = new(transform.position, downVector);
        Ray upRay = new(transform.position, upVector);
        Ray leftRay = new(transform.position, leftVector);
        Ray rightRay = new(transform.position, rightVector);
        RaycastHit hit;

        if (Physics.Raycast(leftRay, out hit, raycastLength, collisionLayers))
        {
            horizontalInput = 1 * Math.Min(5, 5 / hit.distance);
        }

        if (Physics.Raycast(rightRay, out hit, raycastLength, collisionLayers))
        {
            horizontalInput = -1f * Math.Min(5, 5 / hit.distance);
        }

        if (Physics.Raycast(upRay, out hit, raycastLength, collisionLayers))
        {
            verticalInput = 1f * Math.Min(10, 10 / hit.distance);
        }

        // Ground Detection
        if (Physics.Raycast(downRay, out hit, raycastLength, collisionLayers))
        {
            Debug.DrawRay(hit.point, hit.normal, Color.blue);

            Vector3 normalHitVector = hit.normal;
            Vector3 adjustedNormalVector = Quaternion.AngleAxis(90, transform.right) * normalHitVector;
            
            float angleBetween = Vector3.Angle(transform.forward, adjustedNormalVector);
            verticalInput = -1f * Mathf.Max(verticalInput, (angleBetween / maxPitchAngle));
        }
    }

    private void LockMovement()
    {
        if (Physics.Raycast(transform.position, leftVector, raycastFarLength, collisionLayers) || transform.localPosition.x <= horizontalMovementLimit * -1f)
        {
            if (horizontalInput < 0)
            {
                horizontalInput = 0;
            }
        }

        if (Physics.Raycast(transform.position, rightVector, raycastFarLength, collisionLayers) || transform.localPosition.x >= horizontalMovementLimit)
        {
            if (horizontalInput > 0)
            {
                horizontalInput = 0;
            }
        }

        if (Physics.Raycast(transform.position, upVector, raycastFarLength, collisionLayers) || transform.localPosition.y >= verticalMovementLimit)
        {
            if (verticalInput < 0)
            {
                verticalInput = 0;
            }
        }

        if (Physics.Raycast(transform.position, downVector, raycastFarLength, collisionLayers) || transform.localPosition.y <= verticalMovementLimit * -1f)
        {
            if (verticalInput > 0)
            {
                verticalInput = 0;
            }
        }
    }

    private IEnumerator RotateDolphin()
    {
        while(true)
        {            
            Quaternion startRotation = new Quaternion(dolphinObject.transform.localRotation.x, dolphinObject.transform.localRotation.y, dolphinObject.transform.localRotation.z, dolphinObject.transform.localRotation.w);
            Quaternion storedTargetRotation = new Quaternion(targetRotation.x, targetRotation.y, targetRotation.z, targetRotation.w);
            for(float i = 0; i < 1; i = Mathf.Min(i + rotationAnglePerFrame, 1))
            {
                dolphinObject.transform.localRotation = Quaternion.Lerp(startRotation, storedTargetRotation, i);
                yield return new WaitForEndOfFrame();
            }
        }
    }

    private void OnDrawGizmos() //used to see Ray in editor without update function
    {
        Debug.DrawRay(transform.position, leftVector * raycastLength, Color.red);
        Debug.DrawRay(transform.position, rightVector * raycastLength, Color.red);
        Debug.DrawRay(transform.position, upVector * raycastLength, Color.green);
        Debug.DrawRay(transform.position, downVector * raycastLength, Color.green);
    }
}
