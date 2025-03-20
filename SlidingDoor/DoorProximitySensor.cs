using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

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

            // Check for players already inside the collider
            CheckForPlayersAlreadyInside();
        }
        else
        {
            Debug.LogError("[DoorProximitySensor] NO DOOR CONTROLLER ASSIGNED!");
        }
    }

    private void CheckForPlayersAlreadyInside()
    {
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);

        Collider col = GetComponent<Collider>();
        if (col == null) return;

        foreach (VRCPlayerApi player in players)
        {
            if (player != null && col.bounds.Contains(player.GetPosition()))
            {
                if (debugLogging)
                {
                    Debug.Log($"[DoorProximitySensor] Player already inside: {player.displayName}");
                }
                doorController.TriggerSensorPlayerEntered(player);
            }
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
            Debug.Log($"[DoorProximitySensor] Player entered: {player.displayName}");
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
        
        if (debugLogging)
        {
            Debug.Log($"[DoorProximitySensor] Player exited: {player.displayName}");
        }
        
        // Only call ONE method - let the door handle all the logic
        doorController.TriggerSensorPlayerExited(player);
    }
    
    // Simple manual control method
    public void ForceTriggerDoor(bool open)
    {
        if (doorController == null) return;
        if (open)
            doorController.OpenDoor();
        else
            doorController.CloseDoor();
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
}
