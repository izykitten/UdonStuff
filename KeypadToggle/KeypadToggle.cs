using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
[AddComponentMenu("izy/KeypadToggle")]
public class KeypadToggle : UdonSharpBehaviour
{
    [Tooltip("Objects to activate/deactivate via keypad")]
    public GameObject[] targetObjects;
    
    [Header("Behavior Settings")]
    [Tooltip("Delay in seconds before disabling objects after keypad is closed (0 = immediate, -1 = never disable)")]
    public float closeDelay = 0f;

    // Timer variables for manual delay implementation
    private float deactivationTimer = -1f;
    private bool timerActive = false;

    private void Start()
    {
        Debug.Log($"[KeypadToggle] Initialized with {(targetObjects != null ? targetObjects.Length : 0)} targets");
    }

    private void Update()
    {
        // Only process the timer if it's active
        if (timerActive)
        {
            deactivationTimer -= Time.deltaTime;
            
            if (deactivationTimer <= 0f)
            {
                timerActive = false;
                ForceDeactivate();
            }
        }
    }

    // Standard VRChat Keypad callback - called when keypad grants access
    public void _keypadGranted()
    {
        ForceActivate();
    }
    
    // Standard VRChat Keypad callback - called when keypad is closed
    public void _keypadClosed()
    {
        // Never deactivate if closeDelay < 0
        if (closeDelay < 0) return;
        
        if (closeDelay == 0)
        {
            // Immediate deactivation
            ForceDeactivate();
        }
        else
        {
            // Start timer for delayed deactivation
            deactivationTimer = closeDelay;
            timerActive = true;
        }
    }

    // Activates all target objects
    public void ForceActivate()
    {
        if (targetObjects == null || targetObjects.Length == 0) return;
        
        foreach (GameObject obj in targetObjects)
        {
            if (obj != null) obj.SetActive(true);
        }
    }
    
    // Deactivates all target objects
    public void ForceDeactivate()
    {
        if (targetObjects == null || targetObjects.Length == 0) return;
        
        foreach (GameObject obj in targetObjects)
        {
            if (obj != null) obj.SetActive(false);
        }
    }
}