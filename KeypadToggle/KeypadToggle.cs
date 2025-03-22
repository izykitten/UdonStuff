using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class KeypadToggle : UdonSharpBehaviour
{
    [SerializeField] private GameObject[] targetObjects;
    [SerializeField] private int[] AdditionalPasscodeNumbers;
    [SerializeField] private bool DisableKeypadGranted = false;

    [Tooltip("Delay in seconds before disabling objects after keypad is closed (0 = immediate, -1 = never disable)")]
    public float closeDelay = 0f;

    private void Start()
    {
        Debug.Log("[KeypadToggle] Initialized on " + gameObject.name);
    }
    
    // Enable objects
    public void EnableObjects()
    {
        Debug.Log("[KeypadToggle] Enabling objects");
        
        // If no targets specified, activate self
        if (targetObjects == null || targetObjects.Length == 0)
        {
            Debug.Log("[KeypadToggle] No targets, activating self");
            gameObject.SetActive(true);
            return;
        }
        
        // Otherwise enable all specified targets
        Debug.Log("[KeypadToggle] Enabling " + targetObjects.Length + " target objects");
        for (int i = 0; i < targetObjects.Length; i++)
        {
            if (targetObjects[i] != null)
            {
                Debug.Log("[KeypadToggle] Enabling target[" + i + "]");
                targetObjects[i].SetActive(true);
            }
            else
            {
                Debug.LogWarning("[KeypadToggle] Target[" + i + "] is null");
            }
        }
    }
    
    // Disable objects
    private void DisableObjects()
    {
        Debug.Log("[KeypadToggle] Disabling objects");
        
        // If no targets specified, do nothing (never disable self)
        if (targetObjects == null || targetObjects.Length == 0)
        {
            Debug.Log("[KeypadToggle] No targets to disable");
            return;
        }
        
        // Otherwise disable all specified targets
        Debug.Log("[KeypadToggle] Disabling " + targetObjects.Length + " target objects");
        for (int i = 0; i < targetObjects.Length; i++)
        {
            if (targetObjects[i] != null)
            {
                Debug.Log("[KeypadToggle] Disabling target[" + i + "]");
                targetObjects[i].SetActive(false);
            }
            else
            {
                Debug.LogWarning("[KeypadToggle] Target[" + i + "] is null");
            }
        }
    }
    
    // Direct and external event methods
    public void _keypadGranted()
    {
        Debug.Log("[KeypadToggle] Keypad granted");
        
        if (!DisableKeypadGranted)
        {
            EnableObjects();
        }
        else
        {
            Debug.Log("[KeypadToggle] Keypad granted ignored - DisableKeypadGranted is true");
        }
    }
    
    public void ExternalKeypadGranted()
    {
        _keypadGranted();
    }
    
    public void _keypadAdditionalPasscode(int number)
    {
        Debug.Log("[KeypadToggle] _keypadAdditionalPasscode called with number: " + number);
        
        // Log current AdditionalPasscodeNumbers array details
        if(AdditionalPasscodeNumbers == null || AdditionalPasscodeNumbers.Length == 0)
        {
            Debug.Log("[KeypadToggle] No additional passcodes set, granting keypad");
            EnableObjects();
            return;
        }
        
        Debug.Log("[KeypadToggle] AdditionalPasscodeNumbers.Length = " + AdditionalPasscodeNumbers.Length);
        for (int i = 0; i < AdditionalPasscodeNumbers.Length; i++)
        {
            Debug.Log("[KeypadToggle] AdditionalPasscodeNumbers[" + i + "] = " + AdditionalPasscodeNumbers[i]);
        }
        
        int matchedIndex = -1;
        for (int i = 0; i < AdditionalPasscodeNumbers.Length; i++)
        {
            if (AdditionalPasscodeNumbers[i] == number)
            {
                matchedIndex = i;
                Debug.Log("[KeypadToggle] Exact match found for value " + number + " at index: " + i);
                break;
            }
        }
        
        // Fallback: if no match found, force a fallback activation – this helps diagnose if the issue is matching
        if (matchedIndex < 0)
        {
            Debug.LogWarning("[KeypadToggle] No match found for passcode " + number + ". Forcing fallback activation.");
            EnableObjects();
            return;
        }
        
        // If matched index is found, check targetObjects array
        if (targetObjects != null && matchedIndex < targetObjects.Length && targetObjects[matchedIndex] != null)
        {
            Debug.Log("[KeypadToggle] Activating specific target: " + targetObjects[matchedIndex].name);
            targetObjects[matchedIndex].SetActive(true);
        }
        else
        {
            Debug.LogWarning("[KeypadToggle] Matched index " + matchedIndex + " has no valid target. Activating all targets.");
            EnableObjects();
        }
    }
    
    public void ExternalKeypadAdditionalPasscode(int passcode)
    {
        Debug.Log("[KeypadToggle] ExternalKeypadAdditionalPasscode called with passcode: " + passcode);
        _keypadAdditionalPasscode(passcode);
    }
    
    public void _keypadClosed()
    {
        Debug.Log("[KeypadToggle] Keypad closed");
        if (closeDelay >= 0f)
        {
            if (closeDelay > 0f)
            {
                Debug.Log("[KeypadToggle] Will disable objects after " + closeDelay + " seconds");
                SendCustomEventDelayedSeconds("_DelayedDisableObjects", closeDelay);
            }
            else
            {
                Debug.Log("[KeypadToggle] Disabling objects immediately");
                DisableObjects();
            }
        }
        else
        {
            Debug.Log("[KeypadToggle] closeDelay is negative, not disabling objects");
        }
    }
    
    public void ExternalKeypadClosed()
    {
        _keypadClosed();
    }

    public void _DelayedDisableObjects()
    {
        Debug.Log("[KeypadToggle] Executing delayed disable");
        
        // Extra safety check to avoid NullReferenceException
        if (targetObjects == null)
        {
            Debug.LogError("[KeypadToggle] targetObjects array is null in _DelayedDisableObjects");
            return;
        }
        
        // Call the disable implementation
        DisableObjects();
    }
}