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

[AddComponentMenu("izy/SlidingDoor/SlidingDoor")]
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
    private bool closingFromTrigger = false; // New flag to track closings triggered by sensor exit

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
            bool previousState = UseProximitySensor;
            
            if (value) {
                operationModeValue = (int)DoorOperationMode.ProximitySensor;
            } else {
                operationModeValue = (int)DoorOperationMode.Normal;
                // Reset player count when turning off proximity mode
                playerCount = 0;
                if (debugLogging) Debug.Log("[SlidingDoor] Proximity mode disabled - player count reset to 0");
            }
            Debug.Log("[SlidingDoor] Setting operationModeValue to " + operationModeValue + " (" + (value ? "ProximitySensor" : "Normal") + ")");
            
            // If proximity sensor object exists, and we're turning off proximity sensing
            if (proximitySensorObject != null && previousState && !value) {
                proximitySensorObject.SetActive(false);
            }
        } 
    }

    [SerializeField]
    private int doorLockedValue = 0;
    public bool DoorLocked 
    { 
        get { return doorLockedValue != 0; } 
        set { 
            bool previousState = doorLockedValue != 0;
            doorLockedValue = value ? 1 : 0;
            
            // Log the state change if it actually changed
            if (previousState != value && debugLogging)
            {
                Debug.Log("[SlidingDoor] DoorLocked property changed from " + previousState + " to " + value);
            }
        } 
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
    private bool pendingOpenFromTrigger = false; // New flag to remember if pending open was from trigger

    [SerializeField]
    private GameObject proximitySensorObject;

    // Add new force close flag:
    private bool forceCloseFlag = false;

    // Add debugging property near the top of the class
    [SerializeField]
    private bool debugLogging = true; // Default to true while troubleshooting

    // Add a new flag to force close even when locked
    private bool bypassLockForClose = false;

    // Add new player check safety variables and timer
    private float playerCheckTimer = 0f;
    private float playerCheckInterval = 2.0f; // Check every 2 seconds
    private int lastKnownPlayerCount = 0;
    private bool playerDetectedThisFrame = false;

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
        
        // Initialize our last known player count
        lastKnownPlayerCount = playerCount;

        // Manually check for local player already inside the collider
        CheckForLocalPlayerAlreadyInside();
    }

    // Change method to public to allow access from DoorProximitySensor
    public void CheckForLocalPlayerAlreadyInside()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null) return;

        Collider col = proximitySensorObject.GetComponent<Collider>();
        if (col == null) return;

        if (col.bounds.Contains(localPlayer.GetPosition()))
        {
            if (debugLogging)
            {
                Debug.Log($"[SlidingDoor] Local player already inside");
            }
            TriggerSensorPlayerEntered(localPlayer);
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
        
        Debug.Log("[SlidingDoor] _keypadGranted: Unlocking door. Current state: " + doorState + 
                  ", Currently locked: " + DoorLocked);
        
        // Change from: DoorLocked = false
        // To: Use the UnlockDoor method to properly handle sensor state
        UnlockDoor();
        
        // If the door is already unlocked but we called _keypadGranted again,
        // let's ensure we handle that case correctly
        if (doorState == DoorState.Closed && !isProximityMode)
        {
            Debug.Log("[SlidingDoor] _keypadGranted: Door is closed and not in proximity mode, opening door");
            StartOpening();
        }
        
        if (isProximityMode)
        {
            Debug.Log("[SlidingDoor] _keypadGranted: In proximity mode, reset player detection");
            // Reset player detection to ensure sensors work properly
            if (proximitySensorObject != null)
            {
                proximitySensorObject.SetActive(false);
                proximitySensorObject.SetActive(true);
            }
        }
    }
    
    public void _keypadClosed()
    {
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

    // Add the _keypadClear method to close the door
    // Modify _keypadClear to set forceCloseFlag before closing
    // Modify _keypadClear to bypass lock and always close
    // Modify _keypadClear to reset player count and ensure door closes
    // Modify _keypadClear to directly set the door state and position
    // Fix the _keypadClear method to properly force close the door
    public void _keypadClear()
    {
        Debug.Log("[SlidingDoor] _keypadClear: Force closing door immediately");

        // Stop any animations and reset flags
        forceCloseFlag = true;
        bypassLockForClose = true;
        pendingCloseAfterOpen = false;
        pendingOpenAfterClose = false;
        
        // Reset player count to prevent proximity sensor from keeping door open
        playerCount = 0;
        
        // Force door to closed state
        doorState = DoorState.Closed;
        
        // Directly set door position to closed
        leftDoor.localPosition = leftClosedPos;
        rightDoor.localPosition = rightClosedPos;
        
        // Reset timer to ensure animations start from beginning if door is used again
        timer = 0;
        
        if (debugLogging) Debug.Log("[SlidingDoor] _keypadClear: Door forced to closed position. Player count reset to 0.");
    }

    void Update()
    {
        // Add door state debug log every few seconds
        if (debugLogging && Time.frameCount % 300 == 0) // Log approximately every 5 seconds at 60 fps
        {
            Debug.Log($"[SlidingDoor] Current state: {doorState}, Player count: {playerCount}, Timer: {timer:F2}");
        }

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

        // Only auto-close if in proximity sensor mode and no players are detected
        if (UseProximitySensor && doorState == DoorState.Open && playerCount == 0)
        {
            // Don't close immediately - double check that player count is accurate
            // instead of calling StartClosing() directly
            ValidateAndHandleEmptySensor();
        }
        
        // Run periodic validation of player count
        if (UseProximitySensor) {
            playerCheckTimer += Time.deltaTime;
            if (playerCheckTimer >= playerCheckInterval) {
                playerCheckTimer = 0f;
                ValidatePlayerCount();
            }
        }
        
        // Reset per-frame detection flag
        playerDetectedThisFrame = false;
    }
    
    // Add method to validate player count
    private void ValidatePlayerCount()
    {
        if (!UseProximitySensor) return;
        
        // If player count changed drastically, log it
        if (lastKnownPlayerCount > 0 && playerCount == 0 && !playerDetectedThisFrame) {
            Debug.Log("[SlidingDoor] WARNING: Player count dropped from " + lastKnownPlayerCount + 
                      " to 0. This might be a sensor glitch if players are still present.");
            
            // Force a direct player check
            CheckForPlayersInTrigger();
        }
        
        // Remember current count for next time
        lastKnownPlayerCount = playerCount;
    }
    
    // Add method to handle empty sensor state with extra validation
    private void ValidateAndHandleEmptySensor()
    {
        // If we just detected we're empty (since we were non-empty before)
        if (lastKnownPlayerCount > 0 && playerCount == 0) {
            Debug.Log("[SlidingDoor] Sensor appears empty, double-checking before closing...");
            
            // Attempt to detect any players in the trigger
            CheckForPlayersInTrigger();
            
            // If we're still showing 0 after the check
            if (playerCount == 0) {
                Debug.Log("[SlidingDoor] Auto-closing door after validation - no players detected");
                StartClosing();
            } else {
                Debug.Log("[SlidingDoor] Found players after validation, keeping door open");
            }
        } else {
            // Normal case - just close
            Debug.Log("[SlidingDoor] Auto-closing door from Update loop - no players detected");
            StartClosing();
        }
    }
    
    // Add method to directly check for players in trigger
    // Modify CheckForPlayersInTrigger to only check local player
    private void CheckForPlayersInTrigger()
    {
        // Only works if we have a sensor object
        if (proximitySensorObject == null) return;
        
        Debug.Log("[SlidingDoor] Performing manual check for local player in sensor volume");
        
        // Only check local player instead of all players
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null) return;
        
        // Get the collider from proximity sensor to check against
        Collider sensorCollider = proximitySensorObject.GetComponent<Collider>();
        
        if (sensorCollider == null) {
            Debug.LogError("[SlidingDoor] Proximity sensor has no collider!");
            return;
        }
        
        // Check if local player is in the collider bounds
        if (sensorCollider.bounds.Contains(localPlayer.GetPosition())) {
            Debug.Log("[SlidingDoor] Found local player in sensor bounds during manual check");
            // Call our enter function directly
            playerDetectedThisFrame = true;
            TriggerSensorPlayerEntered(localPlayer);
        }
        else if (playerCount > 0) {
            Debug.Log("[SlidingDoor] Local player not found in sensor during manual check, but count is " + 
                      playerCount + ". Will reset to 0.");
            playerCount = 0;
        }
    }

    // Fix the StartOpening method to properly handle trigger-based opening
    // Fix the StartOpening method to immediately reverse closing doors
    // Fix the StartOpening method to sync audio with position
    // Improve StartOpening method for smoother reversals
    public void StartOpening()
    {
        // Store flag value before resetting it
        bool wasOpeningFromTrigger = openingFromTrigger;
        bool isProximityMode = UseProximitySensor;
        
        // Check locked state first
        if (EnableLockingSystem && DoorLocked)
        {
            Debug.Log("[SlidingDoor] Cannot open door: Door is locked");
            openingFromTrigger = false; // Reset flag only after using it
            return;
        }
        
        // Check proximity mode restrictions
        if (isProximityMode && !wasOpeningFromTrigger && doorState == DoorState.Closed)
        {
            Debug.Log("[SlidingDoor] Door opening blocked - when in proximity mode, door can only be opened by player entry");
            openingFromTrigger = false; // Reset flag only after using it
            return;
        }
        
        // Check if door is already open/opening
        if (doorState == DoorState.Opening || doorState == DoorState.Open)
        {
            Debug.Log("[SlidingDoor] Door is already opening or open");
            openingFromTrigger = false; // Reset flag only after using it
            return;
        }
        
        // Handle door in closing state
        if (doorState == DoorState.Closing)
        {
            pendingOpenAfterClose = true;
            pendingOpenFromTrigger = wasOpeningFromTrigger; // Save trigger state for later
            Debug.Log("[SlidingDoor] Door is closing - queued open after close (fromTrigger: " + wasOpeningFromTrigger + ")");
            openingFromTrigger = false; // Reset flag only after using it
            return;
        }
        
        // Now we can reset the flag as we're actually opening the door
        openingFromTrigger = false;
        
        // Clear any pending close operations
        pendingCloseAfterOpen = false;
        
        // Start opening the door
        doorState = DoorState.Opening;
        timer = 0;
        
        // Play sound if available
        if (openSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(openSound);
        }
        
        Debug.Log("[SlidingDoor] Door StartOpening called - Door is " + (DoorLocked ? "locked" : "unlocked") + 
                  ", Proximity: " + isProximityMode + 
                  ", FromTrigger: " + wasOpeningFromTrigger + 
                  ", State: " + doorState);
    }

    // Update StartClosing for consistency
    // Update StartClosing to not close if players are present in proximity sensor
    // Update StartClosing to reverse the opening door animation smoothly
    // Update StartClosing to sync audio with position
    // Improve StartClosing for smoother reversals
    // Modify StartClosing to check for bypassing lock
    // Modify StartClosing to log whether the force close flag is working
    public void StartClosing()
    {
        Debug.Log("[SlidingDoor] StartClosing called - playerCount: " + playerCount + ", forceCloseFlag: " + forceCloseFlag);
        
        // Skip the player count check if we're forcing a close
        if (UseProximitySensor && playerCount > 0 && !forceCloseFlag)
        {
            Debug.Log("[SlidingDoor] Not closing door - players still in proximity: " + playerCount);
            return;
        }
        else if (forceCloseFlag)
        {
            Debug.Log("[SlidingDoor] Force closing door regardless of player count: " + playerCount);
        }
        
        // Reset bypass flag after sending network event
        bool wasBypassingLock = bypassLockForClose;
        bypassLockForClose = false;
        
        // Check if force close is active for logging
        bool wasForceClose = forceCloseFlag;
        
        if (doorState == DoorState.Closing || doorState == DoorState.Closed)
        {
            Debug.Log("[SlidingDoor] Door is already closing or closed (force: " + wasForceClose + ")");
            // Reset the force flag even in this case
            forceCloseFlag = false;
            return;
        }
        
        if (UseProximitySensor && !DoorLocked && !forceCloseFlag && playerCount > 0)
        {
            Debug.Log("[SlidingDoor] Canceling close - players inside and door is not locked");
            return;
        }

        if (doorState == DoorState.Opening)
        {
            pendingCloseAfterOpen = true;
            Debug.Log("[SlidingDoor] Door is opening - queued close after open");
            return;
        }
        pendingOpenAfterClose = false;
        if (debugLogging) Debug.Log("[SlidingDoor] Starting to close door" + (wasBypassingLock ? " (bypassing lock)" : ""));
        
        doorState = DoorState.Closing;
        timer = 0;
        
        // Reset the force flag
        forceCloseFlag = false;
        
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

    // Fix the OnPlayerTriggerEnter to better handle proximity detection and ignore remote players
    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        // Set the detection flag
        playerDetectedThisFrame = true;

        // Always log this regardless of debug setting
        Debug.Log("[SlidingDoor] OnPlayerTriggerEnter called! Player: " + (player != null ? player.displayName : "NULL"));
        
        if (player == null)
        {
            Debug.LogError("[SlidingDoor] OnPlayerTriggerEnter called with null player");
            return;
        }
        
        // Only process for local player
        if (!player.isLocal)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] Ignoring remote player: " + player.displayName);
            return;
        }
        
        // Important: Log the GameObject that owns this collider
        Debug.Log("[SlidingDoor] Trigger source GameObject: " + gameObject.name);
        
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
            else if (doorState == DoorState.Closing)
            {
                openingFromTrigger = true; // Set flag before queueing
                Debug.Log("[SlidingDoor] Door is closing, queueing open after close (from trigger)");
                pendingOpenAfterClose = true;
                pendingOpenFromTrigger = true; // Mark this pending open as triggered by player
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
        
        // Only process for local player
        if (!player.isLocal)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] Ignoring remote player exit: " + player.displayName);
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
        
        // Only close if no players are left
        if (playerCount == 0)
        {
            if (doorState == DoorState.Open)
            {
                Debug.Log("[SlidingDoor] Exit detected, door is fully open - closing now.");
                StartClosingFromTrigger();
            }
            else if (doorState == DoorState.Opening)
            {
                Debug.Log("[SlidingDoor] Exit detected while door is opening - queue close after open finishes.");
                pendingCloseAfterOpen = true;
            }
        }
        else
        {
            Debug.Log("[SlidingDoor] Not closing - " + playerCount + " player(s) still in sensor area");
        }
    }

    // Fix the UpdateDoorPosition method to handle the player counts correctly
    private void UpdateDoorPosition(Vector3 leftStart, Vector3 leftTarget, Vector3 rightStart, Vector3 rightTarget, DoorState finalState)
    {
        timer += Time.deltaTime;
        
        // Use a smoother easing function instead of linear
        float t = Mathf.Clamp01(timer / openCloseTime);
        float smoothT = SmoothStep(t);
        
        // Use the smooth interpolation for door movement
        leftDoor.localPosition = Vector3.Lerp(leftStart, leftTarget, smoothT);
        rightDoor.localPosition = Vector3.Lerp(rightStart, rightTarget, smoothT);

        // If we've reached the end of the animation
        if (t >= 1f)
        {
            doorState = finalState;
            
            // Check for pending operations
            if (doorState == DoorState.Open && pendingCloseAfterOpen)
            {
                if (debugLogging) Debug.Log("[SlidingDoor] Door finished opening - checking if we should execute pending close operation");
                
                // Don't close if players are still in proximity sensor
                if (!UseProximitySensor || playerCount == 0 || forceCloseFlag)
                {
                    if (debugLogging) Debug.Log("[SlidingDoor] Executing pending close operation");
                    pendingCloseAfterOpen = false;
                    StartClosing();
                }
                else
                {
                    if (debugLogging) Debug.Log("[SlidingDoor] Canceling pending close - players still present in proximity sensor: " + playerCount);
                    pendingCloseAfterOpen = false; // Clear the pending operation
                }
            }
            else if (doorState == DoorState.Closed && pendingOpenAfterClose)
            {
                if (debugLogging) Debug.Log("[SlidingDoor] Door finished closing - executing pending open operation (fromTrigger: " + pendingOpenFromTrigger + ")");
                pendingOpenAfterClose = false;
                openingFromTrigger = pendingOpenFromTrigger; // Restore the trigger state
                pendingOpenFromTrigger = false; // Reset for next time
                StartOpening();
            }
        }
    }

    // Add a custom smoothing function for better animation
    private float SmoothStep(float t)
    {
        // Smoother than linear interpolation: 3t² - 2t³
        return t * t * (3f - 2f * t);
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

    // Add debug logging to UnlockDoor and improve state handling
    public void UnlockDoor()
    {
        bool wasLocked = DoorLocked;
        DoorLocked = false;
        
        Debug.Log("[SlidingDoor] UnlockDoor: Door unlocked. Was previously locked: " + wasLocked + 
                  ", Current state: " + doorState);
        
        // Reset player count to ensure proper behavior after unlocking
        if (UseProximitySensor)
        {
            playerCount = 0;
            Debug.Log("[SlidingDoor] UnlockDoor: Reset player count to 0");
        }
        
        if (proximitySensorObject != null)
        {
            bool shouldBeActive = UseProximitySensor && !DoorLocked;
            Debug.Log("[SlidingDoor] UnlockDoor: Setting proximity sensor active state to " + shouldBeActive);
            
            // If it should be active and we're toggling it from locked,
            // deactivate and reactivate to ensure clean state
            if (shouldBeActive && wasLocked)
            {
                proximitySensorObject.SetActive(false);
                proximitySensorObject.SetActive(true);
                Debug.Log("[SlidingDoor] UnlockDoor: Reset proximity sensor by toggling active state");
            }
            else
            {
                proximitySensorObject.SetActive(shouldBeActive);
            }
        }
    }

    // Add debug logging to LockDoor
    public void LockDoor()
    {
        DoorLocked = true;
        
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
        if (debugLogging) Debug.Log("[SlidingDoor] OpenDoor method called directly");
        
        if (EnableLockingSystem && DoorLocked)
        {
            Debug.Log("[SlidingDoor] Cannot open - door is locked");
            return;
        }

        if (doorState == DoorState.Closed || doorState == DoorState.Closing)
        {
            doorState = DoorState.Opening;
            timer = 0;
            
            if (openSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(openSound);
            }
            
            Debug.Log("[SlidingDoor] Starting door open sequence");
        }
    }
    
    // Update the CloseDoor method to be more definitive
    // Modify CloseDoor to check forceCloseFlag
    public void CloseDoor()
    {
        if (debugLogging) Debug.Log("CloseDoor method called");
        // Only check player count if not forcing a close
        if (UseProximitySensor && !forceCloseFlag)
        {
            if (proximitySensorObject != null)
            {
                // Force the proximity sensor to update player count
                UdonBehaviour[] behaviours = proximitySensorObject.GetComponents<UdonBehaviour>();
                foreach (UdonBehaviour behaviour in behaviours)
                {
                    behaviour.SendCustomEvent("ForceCheckPlayers");
                }
            }
            
            if (playerCount > 0)
            {
                Debug.Log("[SlidingDoor] Not closing door - players still in proximity: " + playerCount);
                return;
            }
        }
        
        // Reset the force flag after attempting to close
        forceCloseFlag = false;
        
        // Force the door to close no matter what state it's in (unless already closed)
        if (doorState != DoorState.Closed)
        {
            StartClosing();
        }
    }

    // Add method to toggle proximity mode for debugging
    public void ToggleProximityMode()
    {
        bool currentMode = UseProximitySensor;
        UseProximitySensor = !currentMode;
        
        // If we just turned off proximity mode, make sure player count is reset
        if (currentMode && !UseProximitySensor) {
            playerCount = 0;
            if (debugLogging) Debug.Log("[SlidingDoor] Toggled proximity mode OFF - player count reset to 0");
        }
        
        if (debugLogging) Debug.Log("[SlidingDoor] Toggled proximity mode from " + currentMode + " to " + UseProximitySensor);
    }

    // Add these new public methods for the proximity sensor to call
    // Modify PlayerEnteredProximity to only count local players
    public void PlayerEnteredProximity(VRCPlayerApi player)
    {
        if (player == null)
        {
            Debug.LogError("[SlidingDoor] Null player passed to PlayerEnteredProximity");
            return;
        }
        
        // Only process for local player
        if (!player.isLocal)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] Ignoring remote player in PlayerEnteredProximity: " + player.displayName);
            return;
        }
        
        bool isProximityMode = UseProximitySensor;
        if (debugLogging)
        {
            Debug.Log("[SlidingDoor] PlayerEnteredProximity: " + player.displayName +
                      ", Proximity mode: " + isProximityMode + ", Current count: " + playerCount);
        }
        if (isProximityMode)
        {
            playerCount++;
            if ((doorState == DoorState.Closing || doorState == DoorState.Closed) &&
                (!EnableLockingSystem || !DoorLocked))
            {
                openingFromTrigger = true;
                if (doorState == DoorState.Closing) {
                    pendingOpenAfterClose = true;
                    pendingOpenFromTrigger = true; // Mark pending open as trigger-based
                    Debug.Log("[SlidingDoor] Door is closing - queueing open after close from player proximity");
                } else {
                    StartOpening();
                }
            }
        }
        else
        {
            Debug.Log("[SlidingDoor] PlayerEnteredProximity called, but proximity mode is disabled.");
        }
    }

    // Modify PlayerExitedProximity to only count local players
    public void PlayerExitedProximity(VRCPlayerApi player)
    {
        if (player == null)
        {
            Debug.LogError("[SlidingDoor] Null player passed to PlayerExitedProximity");
            return;
        }
        
        // Only process for local player
        if (!player.isLocal)
        {
            if (debugLogging) Debug.Log("[SlidingDoor] Ignoring remote player exit in PlayerExitedProximity: " + player.displayName);
            return;
        }
        
        bool isProximityMode = UseProximitySensor;
        if (debugLogging)
        {
            Debug.Log("[SlidingDoor] PlayerExitedProximity: " + player.displayName +
                      ", Proximity mode: " + isProximityMode + ", Current count: " + playerCount);
        }
        if (!isProximityMode)
        {
            Debug.Log("[SlidingDoor] PlayerExitedProximity called, but proximity mode is disabled.");
            return;
        }
        playerCount = Mathf.Max(0, playerCount - 1);
        Debug.Log("[SlidingDoor] Player count after exit: " + playerCount);
        
        // Only attempt to close if no players remain
        if (playerCount == 0)
        {
            if (doorState == DoorState.Open)
            {
                Debug.Log("[SlidingDoor] No more players nearby - door fully open, close now.");
                ForceCloseNow();
            }
            else if (doorState == DoorState.Opening)
            {
                Debug.Log("[SlidingDoor] No more players nearby - queue close after open finishes.");
                pendingCloseAfterOpen = true;
            }
        }
        else
        {
            Debug.Log("[SlidingDoor] Not closing - " + playerCount + " player(s) still in sensor area");
        }
    }

    // Add a new method to force close the door immediately
    public void ForceCloseNow()
    {
        Debug.Log("[SlidingDoor] ForceCloseNow called - forcing door to close");
        
        // Set force close flag to override player count checks
        forceCloseFlag = true;
        
        if (doorState == DoorState.Open)
        {
            Debug.Log("[SlidingDoor] Door is open, calling StartClosing");
            StartClosing();
        }
        else if (doorState == DoorState.Opening)
        {
            Debug.Log("[SlidingDoor] Door is opening, queueing close after open finishes");
            pendingCloseAfterOpen = true;
        }
        else if (doorState == DoorState.Closing)
        {
            Debug.Log("[SlidingDoor] Door is already closing");
        }
        else
        {
            Debug.Log("[SlidingDoor] Door already closed, current state: " + doorState);
        }
        
        // Reset force flag after attempting to close
        forceCloseFlag = false;
    }

    // Add new StartClosingFromTrigger method that mirrors the smooth reversal behavior
    // Improve StartClosingFromTrigger for smoother reversals
    // Fix StartClosingFromTrigger to better handle closings
    public void StartClosingFromTrigger()
    {
        closingFromTrigger = true;
        Debug.Log("[SlidingDoor] StartClosingFromTrigger called - playerCount: " + playerCount);

        // Always set forceCloseFlag to true to bypass player count checks
        forceCloseFlag = true;

        if (doorState == DoorState.Opening)
        {
            Debug.Log("[SlidingDoor] Door is opening - queue close after open finishes");
            pendingCloseAfterOpen = true;
        }
        else if (doorState == DoorState.Open)
        {
            Debug.Log("[SlidingDoor] Door is open - start closing");
            StartClosing();
        }
        else
        {
            Debug.Log("[SlidingDoor] Door is already closing/closed, current state: " + doorState);
        }

        closingFromTrigger = false;
        forceCloseFlag = false;
    }

    public void ForceCloseIfEmpty()
    {
        // Double-check player count to avoid false closes
        if (playerCount == 0)
        {
            Debug.Log("[SlidingDoor] Forcing door to close after player exit delay");
            
            if (doorState == DoorState.Open || doorState == DoorState.Opening)
            {
                StartClosingFromTrigger();
            }
        }
        else
        {
            Debug.Log("[SlidingDoor] Skipping forced close - players present: " + playerCount);
        }
    }

    public void ResetPlayerCount()
    {
        playerCount = 0;
        Debug.Log("[SlidingDoor] Player count manually reset to zero");
    }

    // Add this NEW method that combines all logic for sensor player entry
    // Modify TriggerSensorPlayerEntered to only handle local players
    public void TriggerSensorPlayerEntered(VRCPlayerApi player)
    {
        // Always log this call
        Debug.Log("[SlidingDoor] TriggerSensorPlayerEntered called for: " + (player != null ? player.displayName : "NULL"));
        
        if (player == null) return;
        
        // Only process for local player
        if (!player.isLocal)
        {
            Debug.Log("[SlidingDoor] Ignoring remote player in TriggerSensorPlayerEntered: " + player.displayName);
            return;
        }
        
        // Check if the door is locked
        if (EnableLockingSystem && DoorLocked)
        {
            Debug.Log("[SlidingDoor] Door is locked - ignoring player entry");
            return;
        }
        
        // Force proximity mode to be true (for safety)
        UseProximitySensor = true;
        
        // Increment player count
        playerCount++;
        Debug.Log("[SlidingDoor] Player count now: " + playerCount);
        
        if (doorState == DoorState.Closed)
        {
            openingFromTrigger = true;
            doorState = DoorState.Opening;
            timer = 0;

            if (openSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(openSound);
            }

            Debug.Log("[SlidingDoor] DIRECTLY opening door for player: " + player.displayName);
        }
        else if (doorState == DoorState.Closing)
        {
            openingFromTrigger = true;
            pendingOpenAfterClose = true;
            pendingOpenFromTrigger = true; // Mark pending open as trigger-based
            Debug.Log("[SlidingDoor] Door is closing, queueing open after close (from TriggerSensor)");
        }
        else if (doorState == DoorState.Open || doorState == DoorState.Opening)
        {
            Debug.Log("[SlidingDoor] Door is already open/opening, doing nothing");
        }
    }

    // Add this NEW method that combines all logic for sensor player exit
    // Fix the TriggerSensorPlayerExited method to ensure the door closes
    public void TriggerSensorPlayerExited(VRCPlayerApi player)
    {
        // Always log this call
        Debug.Log("[SlidingDoor] TriggerSensorPlayerExited called for: " + (player != null ? player.displayName : "NULL"));
        
        if (player == null) return;
        
        // Only process for local player
        if (!player.isLocal)
        {
            Debug.Log("[SlidingDoor] Ignoring remote player exit in TriggerSensorPlayerExited: " + player.displayName);
            return;
        }
        
        // Decrement player count (safely)
        playerCount = Mathf.Max(0, playerCount - 1);
        Debug.Log("[SlidingDoor] Player count now: " + playerCount);
        
        // If no players left, close the door immediately or with slight delay
        if (playerCount == 0 && (doorState == DoorState.Open || doorState == DoorState.Opening))
        {
            // Log that we're scheduling a close
            Debug.Log("[SlidingDoor] No players detected - scheduling door close");
            
            // Use a very short delay for more reliable operation
            SendCustomEventDelayedSeconds(nameof(ForceCloseNow), 0.2f);
        }
    }

    // Add a handler for when the proximity sensor object is deactivated
    public void OnProximitySensorDeactivated()
    {
        // Reset player count when the sensor is disabled
        playerCount = 0;
        if (debugLogging) Debug.Log("[SlidingDoor] Proximity sensor deactivated - player count reset to 0");
        
        // If door is open, close it
        if (doorState == DoorState.Open || doorState == DoorState.Opening) {
            ForceCloseNow();
        }
    }
}