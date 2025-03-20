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

    [SerializeField]
    private bool syncLockState = false;

    [SerializeField]
    private GameObject proximitySensorObject;

    // Add debugging property near the top of the class
    [SerializeField]
    private bool debugLogging = true; // Default to true while troubleshooting

    [SerializeField]
    private AnimationCurve openCloseCurve = AnimationCurve.Linear(0, 0, 1, 1);

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
    // Modify _keypadClear to call ForceCloseDoor with bypass flag set to true
    public void _keypadClear()
    {
        Debug.Log("[SlidingDoor] _keypadClear: FORCING door to close with NO PROXIMITY CHECKS");
        
        // Force player count to zero FIRST, before any other operations
        int oldCount = playerCount;
        bool originalProximityMode = UseProximitySensor;
        playerCount = 0;
        Debug.Log("[SlidingDoor] _keypadClear: FORCED player count from " + oldCount + " to 0");
        // FORCE disable proximity mode FIRST - this ensures checks are bypassed
        UseProximitySensor = false;
        Debug.Log("[SlidingDoor] _keypadClear: FORCED proximity mode from " + originalProximityMode + " to FALSE");
        
        // Force player count to zero
        playerCount = 0;
        Debug.Log("[SlidingDoor] _keypadClear: FORCED player count from " + oldCount + " to 0");
        
        // Find and reset all proximity sensor scripts
        if (proximitySensorObject != null)
        {
            // Reset all sensors in the proximity sensor object
            DoorProximitySensor[] sensors = proximitySensorObject.GetComponentsInChildren<DoorProximitySensor>(true);
            foreach (DoorProximitySensor sensor in sensors)
            {
                if (sensor != null)
                {
                    sensor.ForceResetPlayerCount();
                    Debug.Log("[SlidingDoor] _keypadClear: Reset sensor " + sensor.name);
                }
            }
            
            // Deactivate the proximity sensor object IMMEDIATELY
            proximitySensorObject.SetActive(false);
            Debug.Log("[SlidingDoor] _keypadClear: Deactivated proximity sensor object");
        }
        
        // VERIFY state before closing
        Debug.Log("[SlidingDoor] _keypadClear: PRE-CLOSE STATE CHECK - Proximity mode: " + UseProximitySensor + ", Player count: " + playerCount);
        
        // First force close door with bypass flag
        ForceCloseDoor(true);
        
        // Then explicitly check state again to ensure nothing changed
        Debug.Log("[SlidingDoor] _keypadClear: MID-CLOSE STATE CHECK - Proximity mode: " + UseProximitySensor + ", Player count: " + playerCount);
        
        // Call StartClosing with bypass flag explicitly set and double-check it's being called correctly
        bool bypassFlag = true; // Explicitly define for clarity
        Debug.Log("[SlidingDoor] _keypadClear: Calling StartClosing with bypassFlag=" + bypassFlag);
        StartClosing(bypassFlag);
    }

    // Redesign ForceCloseDoor to close reliably without abrupt position changes
    // Update the ForceCloseDoor method signature to accept a bypass flag
    public void ForceCloseDoor(bool bypassProximityCheck = false)
    {
        Debug.Log("[SlidingDoor] ForceCloseDoor called - FORCING DOOR CLOSE" + (bypassProximityCheck ? " with bypass check" : ""));
        
        if (doorState == DoorState.Closed)
        {
            Debug.Log("[SlidingDoor] Door is already closed");
            return;
        }
        
        // Force player count to zero 
        int oldCount = playerCount;
        playerCount = 0;
        Debug.Log("[SlidingDoor] FORCED player count from " + oldCount + " to 0");
        
        // Set the door to closing state
        doorState = DoorState.Closing;
        
        // Play sound - replace with SafePlayAudio
        if (closeSound != null && audioSource != null)
        {
            if (PlayCloseSoundInReverse)
            {
                float startTime = closeSound.length - 0.1f; // Start from end
                SafePlayAudio(closeSound, startTime, -1.0f);
            }
            else
            {
                SafePlayAudio(closeSound, 0f, 1.0f);
            }
        }
        
        // Start the door closing animation
        if (doorState == DoorState.Opening)
        {
            // Instead of resetting timer to 0,
            // smoothly reverse via partial progress
            float openingProgress = Mathf.Clamp01(timer / openCloseTime);
            float newClosingProgress = 1f - openingProgress;
            timer = newClosingProgress * openCloseTime;
        }
        else
        {
            // For open doors, start closing from the beginning
            timer = 0;
        }
        
        // Removed scheduled position updates as the main Update() loop handles animation
        
        Debug.Log("[SlidingDoor] Started forced door closing sequence");
    }
   
    // Modified ForceClosePositionUpdate to ensure animation completes
    public void ForceClosePositionUpdate()
    {
        if (doorState == DoorState.Closing)
        {
            // Don't modify the timer, just ensure animation progresses
            UpdateDoorPosition(leftClosedPos + leftOpenOffset, leftClosedPos,
                              rightClosedPos + rightOpenOffset, rightClosedPos, DoorState.Closed);
            
            Debug.Log("[SlidingDoor] Force-updating door closing position");
            
            // If the door is almost closed, finish the animation
            if (timer >= openCloseTime * 0.9f)
            {
                leftDoor.localPosition = leftClosedPos;
                rightDoor.localPosition = rightClosedPos;
                doorState = DoorState.Closed;
                Debug.Log("[SlidingDoor] Door closing complete");
            }
        }
    }
    
    // Add a flag to the StartClosing method to optionally bypass proximity checks
    public void StartClosing(bool bypassProximityCheck = false)
    {
        Debug.Log("[SlidingDoor] StartClosing called - playerCount: " + playerCount + ", bypassCheck: " + bypassProximityCheck);
        
        // Remove or comment out the condition that prevents closing when players are still in proximity:
        // if (UseProximitySensor && playerCount > 0 && !bypassProximityCheck)
        // {
        //     Debug.Log("[SlidingDoor] Not closing door - players still in proximity: " + playerCount);
        //     return;
        // }
        
        if (doorState == DoorState.Closing || doorState == DoorState.Closed)
        {
            Debug.Log("[SlidingDoor] Door is already closing or closed");
            return;
        }
        
        // Reversal: if door is opening, reverse smoothly
        if (doorState == DoorState.Opening)
        {
            Debug.Log("[SlidingDoor] Reversing opening door to close");
            
            // Calculate current progress and convert it for closing direction
            float openingProgress = Mathf.Clamp01(timer / openCloseTime);
            float newClosingProgress = 1.0f - openingProgress; // Inverse progress
            
            doorState = DoorState.Closing;
            
            // Set timer to match current position
            timer = newClosingProgress * openCloseTime;
            
            // Replace direct audio manipulation with SafePlayAudio
            if (closeSound != null && audioSource != null)
            {
                float startTime = PlayCloseSoundInReverse ? 
                    Mathf.Min(closeSound.length - 0.1f, closeSound.length * newClosingProgress) :
                    Mathf.Max(0f, closeSound.length * (1 - newClosingProgress));
                    
                float pitch = PlayCloseSoundInReverse ? -1.0f : 1.0f;
                
                // Use SafePlayAudio for consistent pitch handling
                SafePlayAudio(closeSound, startTime, pitch);
            }
            return;
        }
        
        Debug.Log("[SlidingDoor] Starting to close door");
        
        doorState = DoorState.Closing;
        timer = 0;

        if (closeSound != null && audioSource != null)
        {
            if (PlayCloseSoundInReverse)
            {
                // Use the SafePlayAudio method with reverse pitch
                float safeTime = Mathf.Clamp(closeSound.length - 0.1f, 0f, closeSound.length);
                SafePlayAudio(closeSound, safeTime, -1.0f);
            }
            else
            {
                // Use the SafePlayAudio method with normal pitch
                SafePlayAudio(closeSound, 0f);
            }
        }
    }
    
    // Update the UpdateDoorPosition method to handle pending operations
    // Improve the animation update method with smoother interpolation
    // Completely redesigned UpdateDoorPosition for more reliable animation
    // Modify UpdateDoorPosition to use consistent timing for both opening and closing
    private void UpdateDoorPosition(Vector3 leftStart, Vector3 leftTarget, Vector3 rightStart, Vector3 rightTarget, DoorState finalState)
    {
        timer += Time.deltaTime;
        
        // Calculate progress with a limit
        float t = Mathf.Clamp01(timer / openCloseTime);
        
        // Use the provided curve to adjust interpolation
        float curveValue = openCloseCurve.Evaluate(t);
        
        // Apply the position changes
        leftDoor.localPosition = Vector3.Lerp(leftStart, leftTarget, curveValue);
        rightDoor.localPosition = Vector3.Lerp(rightStart, rightTarget, curveValue);
        
        // Sync audio playback with door's progress - without try/catch which isn't supported in UdonSharp
        if(audioSource != null && audioSource.isPlaying && audioSource.clip != null)
        {
            if(doorState == DoorState.Opening && audioSource.clip == openSound && openSound != null)
            {
                float safePosition = Mathf.Clamp(curveValue * openSound.length, 0f, openSound.length - 0.01f);
                if (safePosition >= 0f && safePosition < openSound.length)
                {
                    audioSource.time = safePosition;
                }
            }
            else if(doorState == DoorState.Closing && audioSource.clip == closeSound && closeSound != null)
            {
                // For closing, audio plays in reverse (simulate by time progress reversed)
                float safePosition = Mathf.Clamp(closeSound.length * (1 - curveValue), 0f, closeSound.length - 0.01f);
                if (safePosition >= 0f && safePosition < closeSound.length)
                {
                    audioSource.time = safePosition;
                }
            }
        }
        
        // Log door positions to help with debugging
        if (debugLogging && (t == 0f || t >= 0.99f)) 
        {        
            Debug.Log($"[SlidingDoor] Door position update: t={t:F2}, state={doorState}, Left={leftDoor.localPosition}, Right={rightDoor.localPosition}");
        }

        // If animation is complete
        if (t >= 1f)
        {
            doorState = finalState;
            Debug.Log($"[SlidingDoor] Door animation complete: new state={finalState}, elapsed time={timer:F2}s");
            
            // Set exact final positions to avoid floating point issues
            leftDoor.localPosition = leftTarget;
            rightDoor.localPosition = rightTarget;
            
            if(finalState == DoorState.Closed && audioSource != null && audioSource.clip == closeSound)
            {
                audioSource.Stop();
            }
            else if(finalState == DoorState.Open && audioSource != null && audioSource.clip == openSound)
            {
                audioSource.Stop();
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
        
        // Ensure lock UI or indicators are updated
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
        if (UseProximitySensor)
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
        if (debugLogging) Debug.Log("[SlidingDoor] Toggled proximity mode from " + currentMode + " to " + UseProximitySensor);
    }

    // Add this NEW method that combines all logic for sensor player entry
    public void TriggerSensorPlayerEntered(VRCPlayerApi player)
    {
        Debug.Log("[SlidingDoor] TriggerSensorPlayerEntered called for: " + (player != null ? player.displayName : "NULL"));
        
        if (player == null) return;
        
        // Check if the door is locked
        if (EnableLockingSystem && DoorLocked)
        {
            Debug.Log("[SlidingDoor] Door is locked - ignoring player entry");
            return;
        }
        
        // Force proximity mode
        UseProximitySensor = true;
        
        // Increment player count
        playerCount++;
        Debug.Log("[SlidingDoor] Player count now: " + playerCount);
        
        // If door is not already open, force it open with visual feedback
        if (doorState != DoorState.Open)
        {
            // Replace ForceOpenDoor with StartOpening
            StartOpening();
        }
    }
    
    // Make sure all animations start with a clean timer
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
        // Removed the check that prevented opening if not triggered
        // if (isProximityMode && !wasOpeningFromTrigger && doorState == DoorState.Closed)
        // {
        //     Debug.Log("[SlidingDoor] Door opening blocked - using proximity sensor mode and not triggered by player entry");
        //     return;
        // }
        
        if (doorState == DoorState.Opening || doorState == DoorState.Open)
        {
            Debug.Log("[SlidingDoor] Door is already opening or open");
            return;
        }
        if (doorState == DoorState.Closing)
        {
            Debug.Log("[SlidingDoor] Reversing closing door to opening");
            
            float closingProgress = Mathf.Clamp01(timer / openCloseTime);
            float newOpeningProgress = 1.0f - closingProgress;
            doorState = DoorState.Opening;
            timer = newOpeningProgress * openCloseTime;
            if (openSound != null && audioSource != null)
            {
                // Instead of PlayOneShot, set clip for sync
                float safeTime = Mathf.Clamp(newOpeningProgress * openSound.length, 0f, openSound.length - 0.1f);
                SafePlayAudio(openSound, safeTime);
            }
            return;
        }
        doorState = DoorState.Opening;
        // Before setting timer = 0, log the current timer value
        if (debugLogging) Debug.Log("[SlidingDoor] Opening door with timer reset from " + timer);
        // Always ensure timer is completely reset for consistent timing
        timer = 0;
        if (openSound != null && audioSource != null)
        {
            // Replace with safe play
            SafePlayAudio(openSound, 0f);
        }
        Debug.Log("[SlidingDoor] Door StartOpening called - Door is " + (DoorLocked ? "locked" : "unlocked") + ", Proximity: " + isProximityMode + ", FromTrigger: " + wasOpeningFromTrigger + ", State: " + doorState);
    }

    // Improved Update method to ensure animation processing
    void Update()
    {
        // Process door animations with higher priority
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
        
        // Auto-close logic for proximity sensor mode
        if (UseProximitySensor && doorState == DoorState.Open && playerCount == 0)
        {
            StartClosing();
        }
    }
    
    // Add this method to handle player exits through the proximity sensor
    // Modify TriggerSensorPlayerExited to ensure door close timing is consistent
    public void TriggerSensorPlayerExited(VRCPlayerApi player)
    {
        Debug.Log("[SlidingDoor] TriggerSensorPlayerExited called for: " + (player != null ? player.displayName : "NULL"));
        
        if (player == null) return;
        
        // Decrement player count (safely)
        playerCount = Mathf.Max(0, playerCount - 1);
        Debug.Log("[SlidingDoor] Player count now: " + playerCount);
        
        // If no players left, schedule closing - use consistent delay
        if (playerCount == 0 && (doorState == DoorState.Open || doorState == DoorState.Opening))
        {
            // Use a fixed 0.5 second delay before closing for consistency
            SendCustomEventDelayedSeconds(nameof(StartClosingFromTrigger), 0.5f);
            Debug.Log("[SlidingDoor] Scheduled door to close in 0.5s, animation will take " + openCloseTime + " seconds");
        }
    }
    
    // Add new StartClosingFromTrigger method that mirrors the smooth reversal behavior
    // Improve StartClosingFromTrigger for smoother reversals
    // Fix StartClosingFromTrigger to use SafePlayAudio with proper pitch handling
    public void StartClosingFromTrigger()
    {
        Debug.Log("[SlidingDoor] StartClosingFromTrigger called - playerCount: " + playerCount);
        
        // Always perform the smooth reversal for opening doors when called from trigger
        if (doorState == DoorState.Opening)
        {
            Debug.Log("[SlidingDoor] Smoothly reversing opening door to closing");
            
            // Calculate current progress and convert it for closing direction
            float openingProgress = Mathf.Clamp01(timer / openCloseTime);
            float newClosingProgress = 1.0f - openingProgress; // Inverse progress
            
            doorState = DoorState.Closing;
            
            // Set timer for new direction based on current position
            timer = newClosingProgress * openCloseTime;
            
            // Use SafePlayAudio instead of PlayOneShot for better pitch control
            if (closeSound != null && audioSource != null)
            {
                float volume = Mathf.Max(0.4f, newClosingProgress);
                float startTime = PlayCloseSoundInReverse ? 
                    Mathf.Min(closeSound.length - 0.1f, closeSound.length * newClosingProgress) :
                    Mathf.Max(0f, closeSound.length * (1 - newClosingProgress));
                    
                float pitch = PlayCloseSoundInReverse ? -1.0f : 1.0f;
                
                // Use our SafePlayAudio method with correct pitch
                SafePlayAudio(closeSound, startTime, pitch);
            }
            return;
        }
        
        // For all other cases, delegate to normal closing
        StartClosing(true);
    }
    
    public void ResetPlayerCount()
    {
        playerCount = 0;
        Debug.Log("[SlidingDoor] Player count manually reset to zero");
    }

    // Add a helper method to safely play audio and avoid seek position errors
    private void SafePlayAudio(AudioClip clip, float startTime = 0f, float pitch = 1.0f)
    {
        if (audioSource == null || clip == null) return;
        
        // Ensure clip is valid and has length
        if (clip.length <= 0.01f) return;
        
        // Safety bounds check
        float safeStartTime = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.1f));
        
        audioSource.clip = clip;
        audioSource.pitch = pitch;
        
        // Set time BEFORE playing
        audioSource.time = safeStartTime;
        
        // Stop any existing playback first
        audioSource.Stop();
        
        // Now safely play
        audioSource.Play();
    }
}