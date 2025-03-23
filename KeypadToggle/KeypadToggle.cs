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
    
    [SerializeField, Tooltip("Enable network syncing of object states")]
    private bool networkSync = false;

    public void _keypadGranted()
    {
        if (networkSync)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "NetworkedEnable");
        }
        else
        {
            EnableObjects();
        }
    }

    public void NetworkedEnable()
    {
        EnableObjects();
    }
    
    private void EnableObjects()
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
        if (networkSync)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            if (disableDelay > 0.0f)
            {
                SendCustomEventDelayedSeconds("NetworkedDelayedDisable", disableDelay);
            }
            else
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "NetworkedDisable");
            }
        }
        else
        {
            if (disableDelay > 0.0f)
            {
                SendCustomEventDelayedSeconds("_DelayedDisable", disableDelay);
            }
            else
            {
                DisableObjects();
            }
        }
    }
    
    public void NetworkedDelayedDisable()
    {
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "NetworkedDisable");
    }
    
    public void NetworkedDisable()
    {
        DisableObjects();
    }
    
    private void DisableObjects()
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
    
    public void _DelayedDisable()
    {
        DisableObjects();
    }
}
