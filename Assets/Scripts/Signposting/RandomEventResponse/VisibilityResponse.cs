using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisibilityResponse : MonoBehaviour,IResponse
{
    [SerializeField]
    private GameObject toggledObject;

    // Public Methods
    public void Activate()
    {
        toggledObject.SetActive(true);
    }

    public void ResetResponse()
    {
        toggledObject.SetActive(false);
    }

}
