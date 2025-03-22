using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[AddComponentMenu("izy/SlidingDoor/DoorProximitySensor")]
public class DoorProximitySensor : UdonSharpBehaviour
{
    public SlidingDoor doorController;
    public bool debugLogging = true;
    
    void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("[DoorProximitySensor] No collider found! Please add a collider.");
            return;
        }
        
        col.isTrigger = true;
        col.enabled = true;
        
        if (doorController != null)
        {
            doorController.UseProximitySensor = true;
            if (debugLogging) Debug.Log("[DoorProximitySensor] Connected to door and set proximity mode");

            doorController.CheckForLocalPlayerAlreadyInside();
        }
        else
        {
            Debug.LogError("[DoorProximitySensor] NO DOOR CONTROLLER ASSIGNED!");
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (doorController == null || player == null)
        {
            Debug.LogError("[DoorProximitySensor] Missing references in OnPlayerTriggerEnter");
            return;
        }
        
        if (debugLogging)
        {
            Debug.Log("[DoorProximitySensor] Player entered");
        }
        
        doorController.TriggerSensorPlayerEntered(player);
    }
    
    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (doorController == null || player == null)
        {
            Debug.LogError("[DoorProximitySensor] Missing references in OnPlayerTriggerExit");
            return;
        }
        
        if (debugLogging)
        {
            Debug.Log("[DoorProximitySensor] Player exited");
        }
        
        doorController.TriggerSensorPlayerExited(player);
        SendCustomEventDelayedSeconds(nameof(ForceCloseAfterExit), 1.0f);
    }
    
    public void ForceCloseAfterExit()
    {
        if (doorController == null) return;
        
        if (debugLogging) 
        {
            Debug.Log("[DoorProximitySensor] ForceCloseAfterExit called as backup close mechanism");
        }
        
        doorController.ResetPlayerCount();
        doorController.ForceCloseNow();
    }

    public void ForceTriggerDoor(bool open)
    {
        if (doorController == null) return;
        
        if (debugLogging)
        {
            Debug.Log($"[DoorProximitySensor] ForceTriggerDoor called with open={open}");
        }
        
        if (open)
            doorController.OpenDoor();
        else
        {
            doorController.ResetPlayerCount();
            doorController.ForceCloseNow();
        }
    }

    public void ForceResetPlayerCount()
    {
        if (doorController == null) return;
        
        if (debugLogging)
        {
            Debug.Log("[DoorProximitySensor] Force-resetting player count");
        }
        
        doorController.ResetPlayerCount();
    }

    void OnDisable()
    {
        if (doorController == null) return;

        if (debugLogging)
        {
            Debug.Log("[DoorProximitySensor] Sensor disabled - notifying door controller");
        }
        
        doorController.OnProximitySensorDeactivated();
    }
}
