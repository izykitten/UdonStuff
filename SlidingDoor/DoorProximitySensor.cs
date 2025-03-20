using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class DoorProximitySensor : UdonSharpBehaviour
{
    public SlidingDoor doorController;
    public bool debugLogging = false;
    
    void Start()
    {
        // Make sure any collider on this object is a trigger
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("[DoorProximitySensor] No collider found! Please add a collider to this GameObject.");
            return;
        }
        
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.Log("[DoorProximitySensor] Converted collider to trigger");
        }
        
        // Check for rigidbody (needed for reliable trigger detection)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning("[DoorProximitySensor] No Rigidbody component. For reliable trigger detection, add a Rigidbody with isKinematic=true and useGravity=false");
        }
        
        Debug.Log("[DoorProximitySensor] Started and configured");

        // Check for players already inside the collider
        if (doorController != null)
        {
            SendCustomEventDelayedSeconds(nameof(DelayedCheckForPlayers), 0.5f);
        }
    }
    
    // Special method to handle case where players are already inside when the sensor starts
    private void CheckForPlayersAlreadyInCollider()
    {
        Debug.Log("[DoorProximitySensor] Checking for players already in collider...");
        
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        
        Collider col = GetComponent<Collider>();
        if (col == null) return;
        
        foreach (VRCPlayerApi player in players)
        {
            if (player != null && col.bounds.Contains(player.GetPosition()))
            {
                Debug.Log("[DoorProximitySensor] Found player already in collider: " + player.displayName);
                doorController.PlayerEnteredProximity(player);
            }
        }
    }

    public void DelayedCheckForPlayers()
    {
        CheckForPlayersAlreadyInCollider();
    }
    
    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (debugLogging)
        {
            Debug.Log("[DoorProximitySensor] Player entered: " + player.displayName);
        }
        
        if (doorController != null && player != null)
        {
            doorController.PlayerEnteredProximity(player);
        }
    }
    
    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (debugLogging)
        {
            Debug.Log("[DoorProximitySensor] Player exited: " + player.displayName);
        }
        
        if (doorController != null && player != null)
        {
            doorController.PlayerExitedProximity(player);
        }
    }
}
