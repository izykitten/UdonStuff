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
            // Make sure the door is set to proximity mode
            doorController.UseProximitySensor = true;
            Debug.Log("[DoorProximitySensor] Connected to door and set proximity mode");

            // Check for local player already inside the collider
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
        
        // Only process for local player
        if (!player.isLocal) return;
        
        if (debugLogging)
        {
            Debug.Log($"[DoorProximitySensor] Local player entered");
        }
        
        // Only call ONE method - let the door handle all the logic
        doorController.TriggerSensorPlayerEntered(player);
    }
    
    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (doorController == null || player == null) 
        {
            Debug.LogError("[DoorProximitySensor] Missing references in OnPlayerTriggerExit");
            return;
        }
        
        // Only process for local player
        if (!player.isLocal) return;
        
        if (debugLogging)
        {
            Debug.Log($"[DoorProximitySensor] Local player exited - WILL CLOSE DOOR");
        }
        
        // Call ForceCloseNow directly after a short delay
        if (doorController != null)
        {
            // First tell the door the player left
            doorController.TriggerSensorPlayerExited(player);
            
            // Then force it to close after a short delay as backup
            SendCustomEventDelayedSeconds(nameof(ForceCloseAfterExit), 1.0f);
        }
    }
    
    // Add a method to force close the door after exit with a delay
    public void ForceCloseAfterExit()
    {
        if (doorController == null) return;
        
        if (debugLogging) 
        {
            Debug.Log("[DoorProximitySensor] ForceCloseAfterExit called as backup close mechanism");
        }
        
        // Force reset player count and close the door
        doorController.ResetPlayerCount();
        doorController.ForceCloseNow();
    }

    // Enhanced ForceTriggerDoor to be more reliable
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
            // For closing, reset player count first then close
            doorController.ResetPlayerCount();
            doorController.ForceCloseNow();
        }
    }

    // Add new method to forcibly reset player detection state
    public void ForceResetPlayerCount()
    {
        if (doorController == null) return;
        
        if (debugLogging)
        {
            Debug.Log("[DoorProximitySensor] Force-resetting player count");
        }
        
        // Call the ResetPlayerCount method on the door controller
        doorController.ResetPlayerCount();
    }

    void OnDisable()
    {
        if (doorController == null) return;

        if (debugLogging)
        {
            Debug.Log("[DoorProximitySensor] Sensor disabled - notifying door controller");
        }
        
        // If disabling this sensor, tell the door to reset player count
        doorController.OnProximitySensorDeactivated();
    }
}
