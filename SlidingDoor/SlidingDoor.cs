using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum DoorState : int
{
    Closed,
    Opening,
    Open,
    Closing
}

// Fix the enum definition to make sure values are sequential
public enum DoorOperationMode : int
{
    Normal = 0,
    ProximitySensor = 1  // Change from 2 to 1 for proper sequential values
}

public class SlidingDoor : UdonSharpBehaviour
{
    [SerializeField]
    private Transform leftDoor;

    [SerializeField]
    private Transform rightDoor;

    [SerializeField]
    private Vector3 leftOpenOffset = new Vector3(-1f, 0f, 0f);

    [SerializeField]
    private Vector3 rightOpenOffset = new Vector3(1f, 0f, 0f);

    [SerializeField]
    private float openCloseTime = 1.0f;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private float timer;
    private int playerCount = 0;
    
    // Flag to track if this StartOpening call came from the player trigger
    private bool openingFromTrigger = false;

    private int syncedDoorState;
    [NonSerialized]
    private DoorState doorState = DoorState.Closed;

    [SerializeField]
    private int operationModeValue = 0;
    public DoorOperationMode OperationMode 
    { 
        get { return (DoorOperationMode)operationModeValue; } 
        set { operationModeValue = (int)value; } 
    }

    [SerializeField]
    private int playCloseSoundInReverseValue = 0;
    public bool PlayCloseSoundInReverse 
    { 
        get { return playCloseSoundInReverseValue != 0; } 
        set { playCloseSoundInReverseValue = value ? 1 : 0; } 
    }

    [SerializeField]
    private AudioClip openSound;

    [SerializeField]
    private AudioClip closeSound;

    [SerializeField]
    private GameObject audioSourceObject;

    private AudioSource audioSource;

    // Fix the UseProximitySensor property to properly check and set the operation mode
    public bool UseProximitySensor 
    { 
        get { 
            bool isProximityMode = operationModeValue == (int)DoorOperationMode.ProximitySensor;
            return isProximityMode;
        } 
        set { 
            if (value) {
                operationModeValue = (int)DoorOperationMode.ProximitySensor;
            } else {
                operationModeValue = (int)DoorOperationMode.Normal;
            }
            Debug.Log("[SlidingDoor] Setting operationModeValue to " + operationModeValue + " (" + (value ? "ProximitySensor" : "Normal") + ")");
        } 
    }

    [SerializeField]
    private int doorLockedValue = 0;
    public bool DoorLocked 
    { 
        get { return doorLockedValue != 0; } 
        set { doorLockedValue = value ? 1 : 0; } 
    }

    [SerializeField]
    private int enableLockingSystemValue = 0;
    public bool EnableLockingSystem
    {
        get { return enableLockingSystemValue != 0; }
        set { enableLockingSystemValue = value ? 1 : 0; }
    }

    // Add flags to track pending door operations
    private bool pendingCloseAfterOpen = false;
    private bool pendingOpenAfterClose = false;

    [SerializeField]
    private bool syncLockState = false;

    [SerializeField]
    private GameObject proximitySensorObject;

    void Start()
    {
        leftClosedPos = leftDoor.localPosition;
        rightClosedPos = rightDoor.localPosition;

        // Log the initial operation mode value
        if (debugLogging) Debug.Log("[SlidingDoor] START: Initial operationModeValue = " + operationModeValue);

        // Force an initial check of the proximity mode to ensure it's set correctly
        bool proximityEnabled = UseProximitySensor;
        
        if (proximityEnabled)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] START: Proximity sensor mode is ENABLED");
        }
        else
        {
            if (debugLogging) Debug.Log("[SlidingDoor] START: Proximity sensor mode is DISABLED");
        }

        audioSource = (audioSourceObject != null) 
                       ? audioSourceObject.GetComponent<AudioSource>() 
                       : GetComponent<AudioSource>();

        if (audioSource == null)
        {
            Debug.LogWarning("No AudioSource found on this GameObject or the assigned audioSourceObject.");
        }

        if (UseProximitySensor)
        {
            Debug.Log("[SlidingDoor] IMPORTANT: For proximity sensing to work correctly, use the separate DoorProximitySensor script attached to your trigger collider and reference this SlidingDoor from it!");
        }

        if (proximitySensorObject != null)
        {
            proximitySensorObject.SetActive(UseProximitySensor && !DoorLocked);
        }
    }

    public void ToggleDoor()
    {
        if (doorState == DoorState.Open)
        {
            StartClosing();
        }
        else if (doorState == DoorState.Closed)
        {
            StartOpening();
        }
    }

    // Fix _keypadGranted to use UnlockDoor() instead of directly changing DoorLocked
    public void _keypadGranted()
    {
        bool isProximityMode = UseProximitySensor;
        
        if (!Networking.IsOwner(gameObject))
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "_keypadGranted");
            return;
        }
        
        // Change from: DoorLocked = false
        // To: Use the UnlockDoor method to properly handle sensor state
        UnlockDoor();
        
        if (isProximityMode)
        {
            // ...existing code...
        }
        // ...existing code...
    }
    
    public void _keypadClosed()
    {
        if (!Networking.IsOwner(gameObject))
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "_keypadClosed");
            return;
        }
        
        // Always lock the door, regardless of operation mode
        LockDoor();

        // Reverse the door direction if it's opening
        if (doorState == DoorState.Opening)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] _keypadClosed: Reversing door direction from Opening to Closing");
            StartClosing();
        }
        // Always close the door if it's open
        else if (doorState == DoorState.Open)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] _keypadClosed: Closing door from Open state");
            StartClosing();
        }
    }

    void Update()
    {
        if (doorState == DoorState.Opening)
        {
            UpdateDoorPosition(leftClosedPos, leftClosedPos + leftOpenOffset,
                               rightClosedPos, rightClosedPos + rightOpenOffset, DoorState.Open);
        }
        else if (doorState == DoorState.Closing)
        {
            UpdateDoorPosition(leftClosedPos + leftOpenOffset, leftClosedPos,
                               rightClosedPos + rightOpenOffset, rightClosedPos, DoorState.Closed);
        }

        // Only auto-close if in proximity sensor mode
        if (UseProximitySensor && doorState == DoorState.Open && playerCount == 0)
        {
            StartClosing();
        }
    }

    // Fix the StartOpening method to properly handle trigger-based opening
    // Fix the StartOpening method to immediately reverse closing doors
    public void StartOpening()
    {
        bool wasOpeningFromTrigger = openingFromTrigger;
        bool isProximityMode = UseProximitySensor;
        
        openingFromTrigger = false;

        if (EnableLockingSystem && DoorLocked)
        {
            Debug.Log("[SlidingDoor] Cannot open door: Door is locked");
            return;
        }

        if (isProximityMode && !wasOpeningFromTrigger && doorState == DoorState.Closed)
        {
            Debug.Log("[SlidingDoor] Door opening blocked - using proximity sensor mode and not triggered by player entry");
            return;
        }

        if (!Networking.IsOwner(gameObject))
        {
            if (wasOpeningFromTrigger)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetworkStartOpeningFromTrigger");
            }
            else
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetworkStartOpening");
            }
            return;
        }

        if (doorState == DoorState.Opening || doorState == DoorState.Open)
        {
            Debug.Log("[SlidingDoor] Door is already opening or open");
            return;
        }

        // IMPORTANT CHANGE: If door is closing and this was triggered by player proximity, 
        // immediately reverse the door direction instead of waiting for close to complete
        if (doorState == DoorState.Closing)
        {
            if (wasOpeningFromTrigger)
            {
                Debug.Log("[SlidingDoor] Immediately reversing closing door - player entered proximity");
                // Calculate progress from 1 (just started closing) to 0 (almost closed)
                float closeProgress = 1f - Mathf.Clamp01(timer / openCloseTime);
                
                // Reset the animation with the current position as starting point
                pendingOpenAfterClose = false;
                syncedDoorState = (int)DoorState.Opening;
                RequestSerialization();
                doorState = DoorState.Opening;
                timer = closeProgress * openCloseTime; // Set timer proportionally to maintain smooth motion
                
                if (openSound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(openSound);
                }
                return;
            }
            else
            {
                Debug.Log("[SlidingDoor] Door is still closing - setting flag to open after close completes");
                pendingOpenAfterClose = true;
                return;
            }
        }

        pendingCloseAfterOpen = false;

        syncedDoorState = (int)DoorState.Opening;
        RequestSerialization();
        doorState = DoorState.Opening;
        timer = 0;

        if (openSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(openSound);
        }
        Debug.Log("[SlidingDoor] Door StartOpening called - Door is " + (DoorLocked ? "locked" : "unlocked") + ", Proximity: " + isProximityMode + ", FromTrigger: " + wasOpeningFromTrigger + ", State: " + doorState);
    }

    // Network version should also check operation mode
    public void NetworkStartOpening()
    {
        bool isProximityMode = UseProximitySensor;
        
        if (debugLogging) Debug.Log("[SlidingDoor] NetworkStartOpening called. Proximity mode: " + isProximityMode);
        
        if (EnableLockingSystem && DoorLocked)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] NetworkStartOpening: Door is locked");
            return;
        }

        if (isProximityMode)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] Network door opening blocked - using proximity sensor mode");
            return;
        }

        if (doorState == DoorState.Closing)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] NetworkStartOpening: Door is still closing - setting flag to open after close completes");
            pendingOpenAfterClose = true;
            return;
        }

        pendingCloseAfterOpen = false;
        
        syncedDoorState = (int)DoorState.Opening;
        RequestSerialization();
        doorState = DoorState.Opening;
        timer = 0;

        if (!DoorLocked && openSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(openSound);
        }
    }

    // Special network method for trigger-based opening
    public void NetworkStartOpeningFromTrigger()
    {
        if (debugLogging) Debug.Log("[SlidingDoor] NetworkStartOpeningFromTrigger called");
        
        if (EnableLockingSystem && DoorLocked)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] NetworkStartOpeningFromTrigger: Door is locked");
            return;
        }

        if (doorState == DoorState.Closing)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] NetworkStartOpeningFromTrigger: Door is still closing - setting flag to open after close completes");
            pendingOpenAfterClose = true;
            return;
        }

        pendingCloseAfterOpen = false;
        
        syncedDoorState = (int)DoorState.Opening;
        RequestSerialization();
        doorState = DoorState.Opening;
        timer = 0;

        if (!DoorLocked && openSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(openSound);
        }
    }

    // Update StartClosing for consistency
    // Update StartClosing to not close if players are present in proximity sensor
    // Update StartClosing to reverse the opening door animation smoothly
    public void StartClosing()
    {
        Debug.Log("[SlidingDoor] StartClosing called - playerCount: " + playerCount); // Log player count when closing

        // If we're in proximity mode and there are players in the trigger, don't close
        if (UseProximitySensor && playerCount > 0)
        {
            Debug.Log("[SlidingDoor] Not closing door - players still in proximity: " + playerCount);
            return;
        }

        if (!Networking.IsOwner(gameObject))
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetworkStartClosing");
            return;
        }

        if (doorState == DoorState.Closing || doorState == DoorState.Closed)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] Door is already closing or closed");
            return;
        }

        // If door is opening, immediately reverse the door direction
        if (doorState == DoorState.Opening)
        {
            Debug.Log("[SlidingDoor] Immediately reversing opening door to close");
            
            // Calculate progress from 0 (just started opening) to 1 (almost fully open)
            float openProgress = Mathf.Clamp01(timer / openCloseTime);
            
            // Reset the animation with the current position as starting point
            pendingCloseAfterOpen = false;
            syncedDoorState = (int)DoorState.Closing;
            RequestSerialization();
            doorState = DoorState.Closing;
            timer = openProgress * openCloseTime; // Set timer proportionally to maintain smooth motion
            
            if (closeSound != null && audioSource != null)
            {
                if (PlayCloseSoundInReverse)
                {
                    audioSource.pitch = -1;
                    audioSource.PlayOneShot(closeSound);
                    audioSource.pitch = 1;
                }
                else
                {
                    audioSource.PlayOneShot(closeSound);
                }
            }
            return;
        }

        pendingOpenAfterClose = false;

        if (debugLogging) Debug.Log("[SlidingDoor] Starting to close door");
        syncedDoorState = (int)DoorState.Closing;
        RequestSerialization();
        doorState = DoorState.Closing;
        timer = 0;

        if (closeSound != null && audioSource != null)
        {
            if (PlayCloseSoundInReverse)
            {
                audioSource.pitch = -1;
                audioSource.PlayOneShot(closeSound);
                audioSource.pitch = 1;
            }
            else
            {
                audioSource.PlayOneShot(closeSound);
            }
        }
    }

    // Add network-callable version of StartClosing
    // Update NetworkStartClosing for consistency
    public void NetworkStartClosing()
    {
        // If door is opening, immediately reverse the door direction
        if (doorState == DoorState.Opening)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] NetworkStartClosing: Immediately reversing opening door");
            
            // Calculate progress from 0 (just started opening) to 1 (almost fully open)
            float openProgress = Mathf.Clamp01(timer / openCloseTime);
            
            pendingCloseAfterOpen = false;
            syncedDoorState = (int)DoorState.Closing;
            RequestSerialization();
            doorState = DoorState.Closing;
            timer = openProgress * openCloseTime;
            
            if (closeSound != null && audioSource != null)
            {
                if (PlayCloseSoundInReverse)
                {
                    audioSource.pitch = -1;
                    audioSource.PlayOneShot(closeSound);
                    audioSource.pitch = 1;
                }
                else
                {
                    audioSource.PlayOneShot(closeSound);
                }
            }
            return;
        }

        pendingOpenAfterClose = false;
        
        syncedDoorState = (int)DoorState.Closing;
        RequestSerialization();
        doorState = DoorState.Closing;
        timer = 0;

        if (closeSound != null && audioSource != null)
        {
            if (PlayCloseSoundInReverse)
            {
                audioSource.pitch = -1;
                audioSource.PlayOneShot(closeSound);
                audioSource.pitch = 1;
            }
            else
            {
                audioSource.PlayOneShot(closeSound);
            }
        }
    }

    // Fix the OnPlayerTriggerEnter to better handle proximity detection
    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        // Always log this regardless of debug setting
        Debug.Log("[SlidingDoor] OnPlayerTriggerEnter called! Player: " + (player != null ? player.displayName : "NULL"));

        // Important: Log the GameObject that owns this collider
        Debug.Log("[SlidingDoor] Trigger source GameObject: " + gameObject.name);

        if (player == null)
        {
            Debug.LogError("[SlidingDoor] OnPlayerTriggerEnter called with null player");
            return;
        }

        bool isProximityMode = UseProximitySensor;
        
        if (debugLogging)
        {
            Debug.Log("[SlidingDoor] Trigger ENTER: " + player.displayName + 
                      " (Local: " + player.isLocal + 
                      ") Proximity mode: " + isProximityMode + 
                      ", OperationMode: " + OperationMode + 
                      ", Current count: " + playerCount);
        }

        if (isProximityMode)
        {
            playerCount++;
            
            if (doorState == DoorState.Closed && (!EnableLockingSystem || !DoorLocked))
            {
                openingFromTrigger = true; // Set flag to indicate opening from trigger
                StartOpening();
                
                if (debugLogging) Debug.Log("[SlidingDoor] Proximity trigger opening door. Player count: " + playerCount);
            }
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        Debug.Log("[SlidingDoor] OnPlayerTriggerExit called!"); // Check if the function is called

        if (player == null)
        {
            Debug.LogError("[SlidingDoor] OnPlayerTriggerExit called with null player");
            return;
        }

        bool isProximityMode = UseProximitySensor;
        
        if (debugLogging)
        {
            Debug.Log("[SlidingDoor] Trigger EXIT: " + player.displayName + 
                      " (Local: " + player.isLocal + 
                      ") Proximity mode: " + isProximityMode + 
                      ", OperationMode: " + OperationMode + 
                      ", Current count: " + playerCount);
        }

        if (!isProximityMode)
        {
            return;
        }
        
        playerCount = Mathf.Max(0, playerCount - 1);
        
        // Always log the player count change regardless of debug setting
        Debug.Log("[SlidingDoor] Player exited - player count now: " + playerCount);
        
        if (doorState == DoorState.Open && playerCount == 0)
        {
            // Add a small delay before closing to prevent issues with rapid enter/exit
            SendCustomEventDelayedSeconds(nameof(DelayedDoorClose), 0.5f);
            Debug.Log("[SlidingDoor] Scheduling delayed door close");
        }
    }

    // Add a new method for delayed door closing
    public void DelayedDoorClose()
    {
        Debug.Log("[SlidingDoor] DelayedDoorClose called - playerCount: " + playerCount);
        
        // Double-check player count is still zero before closing
        if (playerCount == 0 && doorState == DoorState.Open)
        {
            Debug.Log("[SlidingDoor] Executing delayed door close");
            StartClosing();
        }
        else
        {
            Debug.Log("[SlidingDoor] Cancelled delayed door close - playerCount: " + playerCount);
        }
    }

    // Update the UpdateDoorPosition method to handle pending operations
    private void UpdateDoorPosition(Vector3 leftStart, Vector3 leftTarget, Vector3 rightStart, Vector3 rightTarget, DoorState finalState)
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / openCloseTime);
        leftDoor.localPosition = Vector3.Lerp(leftStart, leftTarget, t);
        rightDoor.localPosition = Vector3.Lerp(rightStart, rightTarget, t);

        // If we've reached the end of the animation
        if (t >= 1f)
        {
            doorState = finalState;
            
            // Check for pending operations
            if (doorState == DoorState.Open && pendingCloseAfterOpen)
            {
                if (debugLogging) Debug.Log("[SlidingDoor] Door finished opening - executing pending close operation");
                pendingCloseAfterOpen = false;
                StartClosing();
            }
            else if (doorState == DoorState.Closed && pendingOpenAfterClose)
            {
                if (debugLogging) Debug.Log("[SlidingDoor] Door finished closing - executing pending open operation");
                pendingOpenAfterClose = false;
                openingFromTrigger = false; // Not opening from trigger in this case
                StartOpening();
            }
        }
    }

    public override void OnDeserialization()
    {
        doorState = (DoorState)syncedDoorState;

        if (doorState == DoorState.Opening)
        {
            UpdateDoorPosition(leftClosedPos, leftClosedPos + leftOpenOffset,
                               rightClosedPos, rightClosedPos + rightOpenOffset, DoorState.Open);
        }
        else if (doorState == DoorState.Closing)
        {
            UpdateDoorPosition(leftClosedPos + leftOpenOffset, leftClosedPos,
                               rightClosedPos + rightOpenOffset, rightClosedPos, DoorState.Closed);
        }
        else if (doorState == DoorState.Closed)
        {
            UpdateDoorPosition(leftClosedPos + leftOpenOffset, leftClosedPos,
                               rightClosedPos + rightOpenOffset, rightClosedPos, DoorState.Closed);
        }
        else if (doorState == DoorState.Open)
        {
            UpdateDoorPosition(leftClosedPos, leftClosedPos + leftOpenOffset,
                               rightClosedPos, rightClosedPos + rightOpenOffset, DoorState.Open);
        }
    }

    private bool CheckLockingConditions()
    {
        return !DoorLocked;
    }

    private void RefreshLockState()
    {
        if (EnableLockingSystem)
        {
            if (CheckLockingConditions())
            {
                // Only automatically open the door if not in proximity sensor mode
                if (doorState == DoorState.Closed && !UseProximitySensor)
                {
                    openingFromTrigger = false; // Not opening from trigger
                    StartOpening();
                }
            }
            else
            {
                if (doorState == DoorState.Open)
                {
                    StartClosing();
                }
            }
        }
    }

    // Add debug logging to UnlockDoor
    public void UnlockDoor()
    {
        DoorLocked = false;
        if (syncLockState)
        {
            RequestSerialization();
        }
        if (proximitySensorObject != null)
        {
            bool shouldBeActive = UseProximitySensor && !DoorLocked;
            proximitySensorObject.SetActive(shouldBeActive);
            Debug.Log("[SlidingDoor] Proximity sensor object active state set to: " + shouldBeActive);
        }
    }
    
    // Add debug logging to LockDoor
    public void LockDoor()
    {
        DoorLocked = true;
        if (syncLockState)
        {
            RequestSerialization();
        }
        if (proximitySensorObject != null)
        {
            bool shouldBeActive = UseProximitySensor && !DoorLocked;
            proximitySensorObject.SetActive(shouldBeActive);
            Debug.Log("[SlidingDoor] Proximity sensor object active state set to: " + shouldBeActive);
        }
    }

    public void DoUnlockDoor()
    {
        UnlockDoor();
    }
    
    public void DoLockDoor()
    {
        LockDoor();
    }

    // Manually expose OpenDoor method for UI buttons and other interactions
    public void OpenDoor()
    {
        if (debugLogging) Debug.Log("OpenDoor method called");
        if (doorState == DoorState.Closed)
        {
            openingFromTrigger = false; // Not opening from trigger
            StartOpening();
        }
    }
    
    public void CloseDoor()
    {
        if (doorState == DoorState.Open)
        {
            StartClosing();
        }
    }

    // Add method to toggle proximity mode for debugging
    public void ToggleProximityMode()
    {
        bool currentMode = UseProximitySensor;
        UseProximitySensor = !currentMode;
        if (debugLogging) Debug.Log("[SlidingDoor] Toggled proximity mode from " + currentMode + " to " + UseProximitySensor);
    }

    // Add these new public methods for the proximity sensor to call
    // Modify PlayerEnteredProximity to reverse the door if it's closing
    public void PlayerEnteredProximity(VRCPlayerApi player)
    {
        if (player == null) return;
        
        bool isProximityMode = UseProximitySensor;
        
        if (debugLogging)
        {
            Debug.Log("[SlidingDoor] PlayerEnteredProximity: " + player.displayName + 
                      " (Local: " + player.isLocal + 
                      ") Proximity mode: " + isProximityMode + 
                      ", Current count: " + playerCount);
        }
        
        if (isProximityMode)
        {
            playerCount++;
            
            // If the door is closing or closed, start opening it
            if ((doorState == DoorState.Closing || doorState == DoorState.Closed) && 
                (!EnableLockingSystem || !DoorLocked))
            {
                if (doorState == DoorState.Closing && debugLogging)
                {
                    Debug.Log("[SlidingDoor] Reversing closing door - player entered proximity");
                }
                
                openingFromTrigger = true;
                StartOpening();
            }
        }
    }

    public void PlayerExitedProximity(VRCPlayerApi player)
    {
        if (player == null) return;
        
        bool isProximityMode = UseProximitySensor;
        
        if (debugLogging)
        {
            Debug.Log("[SlidingDoor] PlayerExitedProximity: " + player.displayName + 
                      " (Local: " + player.isLocal + 
                      ") Proximity mode: " + isProximityMode + 
                      ", Current count: " + playerCount);
        }
        
        if (!isProximityMode) return;
        
        playerCount = Mathf.Max(0, playerCount - 1);
        
        // Always log the player count change regardless of debug setting
        Debug.Log("[SlidingDoor] Player exited - player count now: " + playerCount);
        
        if (doorState == DoorState.Open && playerCount == 0)
        {
            // Add a small delay before closing to prevent issues with rapid enter/exit
            SendCustomEventDelayedSeconds(nameof(DelayedDoorClose), 0.5f);
            Debug.Log("[SlidingDoor] Scheduling delayed door close");
        }
    }

    [SerializeField]
    private bool debugLogging = false;
}