using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class KeypadToggle : UdonSharpBehaviour
{
    [SerializeField, Tooltip("The objects to toggle when receiving keypad events. If empty, uses this GameObject.")]
    private GameObject[] targetObjects;
    
    [SerializeField, Tooltip("Delay in seconds before disabling the object")]
    private float disableDelay = 0.0f;

    public void _keypadGranted()
    {
        if (targetObjects != null && targetObjects.Length > 0)
        {
            foreach (GameObject obj in targetObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }
        else
        {
            // Default to the GameObject this script is on
            gameObject.SetActive(true);
        }
    }

    public void _keypadClosed()
    {
        if (targetObjects != null && targetObjects.Length > 0)
        {
            if (disableDelay > 0.0f)
            {
                SendCustomEventDelayedSeconds("_DelayedDisable", disableDelay);
            }
            else
            {
                foreach (GameObject obj in targetObjects)
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                    }
                }
            }
        }
        else
        {
            // Default to the GameObject this script is on
            if (disableDelay > 0.0f)
            {
                SendCustomEventDelayedSeconds("_DelayedDisable", disableDelay);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
    
    public void _DelayedDisable()
    {
        if (targetObjects != null && targetObjects.Length > 0)
        {
            foreach (GameObject obj in targetObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
        else
        {
            // Default to the GameObject this script is on
            gameObject.SetActive(false);
        }
    }
}
