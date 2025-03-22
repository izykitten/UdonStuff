using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class KeypadToggle : UdonSharpBehaviour
{
    [SerializeField] private GameObject[] targetObjects;
    [SerializeField] private int[] AdditionalPasscodeNumbers;
    [SerializeField] private bool DisableKeypadGranted = false;
    
    [Header("Toggle Settings")]
    [Tooltip("If true, objects will be disabled when the correct code is entered")]
    public bool disableOnCorrect = false;
    [Tooltip("Delay in seconds before disabling objects (0 = immediate)")]
    public float disableDelay = 0f;

    private void Start()
    {
        Debug.Log("[KeypadToggle] Initialized on " + gameObject.name);
    }
    
    // Core function to toggle target objects
    private void SetTargetsActive(bool active)
    {
        Debug.Log("[KeypadToggle] Setting targets active: " + active);
        
        // If no targets specified, activate self but never deactivate
        if (targetObjects == null || targetObjects.Length == 0)
        {
            if (active) 
            {
                Debug.Log("[KeypadToggle] No targets, activating self");
                gameObject.SetActive(true);
            }
            return;
        }
        
        // Otherwise toggle all specified targets
        Debug.Log("[KeypadToggle] Toggling " + targetObjects.Length + " target objects");
        for (int i = 0; i < targetObjects.Length; i++)
        {
            if (targetObjects[i] != null)
            {
                Debug.Log("[KeypadToggle] Setting target[" + i + "] to " + active);
                targetObjects[i].SetActive(active);
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
            SetTargetsActive(true);
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
        Debug.Log("[KeypadToggle] Additional passcode received: " + number);
        
        // If no passcodes are set, behave like _keypadGranted
        if (AdditionalPasscodeNumbers == null || AdditionalPasscodeNumbers.Length == 0)
        {
            _keypadGranted();
            return;
        }
        
        // First, check if it matches any value in the AdditionalPasscodeNumbers array
        int matchedIndex = -1;
        for (int i = 0; i < AdditionalPasscodeNumbers.Length; i++)
        {
            if (AdditionalPasscodeNumbers[i] == number)
            {
                matchedIndex = i;
                Debug.Log("[KeypadToggle] Matched value " + number + " at index: " + matchedIndex);
                break;
            }
        }
        
        // If no match found by value, try to use the number as a direct index
        if (matchedIndex < 0 && number >= 0 && number < AdditionalPasscodeNumbers.Length)
        {
            matchedIndex = number;
            Debug.Log("[KeypadToggle] Using number as direct index: " + matchedIndex);
        }
        
        // Activate target based on the matched index
        if (matchedIndex >= 0)
        {
            // If we have a matching target, activate just that one
            if (targetObjects != null && matchedIndex < targetObjects.Length && targetObjects[matchedIndex] != null)
            {
                Debug.Log("[KeypadToggle] Activating specific target: " + targetObjects[matchedIndex].name);
                targetObjects[matchedIndex].SetActive(true);
            }
            // Otherwise activate all targets
            else if (targetObjects != null && targetObjects.Length > 0) 
            {
                Debug.Log("[KeypadToggle] No specific target for index, activating all targets");
                SetTargetsActive(true);
            }
            // If no targets at all, activate self
            else
            {
                Debug.Log("[KeypadToggle] No targets, activating self");
                gameObject.SetActive(true);
            }
        }
    }
    
    public void ExternalKeypadAdditionalPasscode(int passcode)
    {
        _keypadAdditionalPasscode(passcode);
    }
    
    public void _keypadClosed()
    {
        Debug.Log("[KeypadToggle] Keypad closed");
        SetTargetsActive(false);
    }
    
    public void ExternalKeypadClosed()
    {
        _keypadClosed();
    }

    private void HandleCorrectCode()
    {
        if (disableOnCorrect)
        {
            if (disableDelay > 0f)
            {
                SendCustomEventDelayedSeconds("_DelayedDisableObjects", disableDelay);
            }
            else
            {
                DisableObjects();
            }
        }
        else
        {
            EnableObjects();
        }
    }
    
    public void _DelayedDisableObjects()
    {
        DisableObjects();
    }
    
    private void DisableObjects()
    {
        foreach (GameObject obj in targetObjects)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }
    
    private void EnableObjects()
    {
        foreach (GameObject obj in targetObjects)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
    }
}