#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

// 1) Add this VRChat SDK namespace:
using VRC.SDKBase.Editor.BuildPipeline;

[InitializeOnLoad]
public static class AutoDisableManager  // RENAMED from AutoDisableEditor to AutoDisableManager
{
    // Option to enable/disable debug logs
    internal static bool debugLogsEnabled = true;

    private static Dictionary<AutoDisable, bool> previousStates = new Dictionary<AutoDisable, bool>();
    private static Dictionary<AutoDisable, bool> occlusionPreviousStates = new Dictionary<AutoDisable, bool>();
    private static bool wasOcclusionBaking = false;
    
    // Change access modifier to internal
    internal static Dictionary<AutoDisable, bool> vrcBuildPreviousStates = new Dictionary<AutoDisable, bool>();

    // Track previous active states to detect changes
    private static Dictionary<int, bool> previousActiveStates = new Dictionary<int, bool>();

    static AutoDisableManager()
    {
        Lightmapping.bakeStarted += OnBakeStarted;
        Lightmapping.bakeCompleted += OnBakeCompleted;
        EditorApplication.update += CheckBakeStatus;
        
        // Register for build events
        BuildPlayerWindow.RegisterBuildPlayerHandler(OnBuildPlayer);
        
        // Monitor for active state changes
        EditorApplication.hierarchyChanged += CheckForActiveStateChanges;
        
        // Load debug log preference
        debugLogsEnabled = EditorPrefs.GetBool("AutoDisable_DebugLogsEnabled", true);
        
        // Initialize previous states
        EditorApplication.delayCall += InitializePreviousStates;
    }
    
    private static void InitializePreviousStates()
    {
        previousActiveStates.Clear();
        var scripts = Resources.FindObjectsOfTypeAll<AutoDisable>();
        foreach (var script in scripts)
        {
            if (script && script.gameObject)
            {
                // Store initial state using instance ID as key
                previousActiveStates[script.gameObject.GetInstanceID()] = script.gameObject.activeSelf;
            }
        }
    }
    
    // Check specifically for active state changes
    private static void CheckForActiveStateChanges()
    {
        // Don't check if we're about to enter play mode or already in it or building
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            return;
            
        bool stateChanged = false;
        var scripts = Resources.FindObjectsOfTypeAll<AutoDisable>();
        
        foreach (var script in scripts)
        {
            if (script && script.gameObject)
            {
                int instanceID = script.gameObject.GetInstanceID();
                bool currentState = script.gameObject.activeSelf;
                
                // Check if we have a record of this object's previous state
                if (previousActiveStates.TryGetValue(instanceID, out bool previousState))
                {
                    // Check if the active state has changed
                    if (currentState != previousState)
                    {
                        if (debugLogsEnabled)
                            Debug.Log($"Object {script.gameObject.name} active state changed from {previousState} to {currentState}");
                        
                        stateChanged = true;
                    }
                }
                
                // Update the record with current state
                previousActiveStates[instanceID] = currentState;
            }
        }
        
        // If any object's active state changed, save all states
        if (stateChanged)
        {
            AutoSaveAllStates();
        }
    }
    
    // Save states for all AutoDisable components
    private static void AutoSaveAllStates() 
    {
        string stateCachePath = "Library/AutoDisableStates.json";
        Dictionary<string, bool> objectStates = new Dictionary<string, bool>();
        
        // Use Resources.FindObjectsOfTypeAll instead of Object.FindObjectsOfTypeAll
        var scripts = Resources.FindObjectsOfTypeAll<AutoDisable>();
        foreach (var script in scripts) 
        {
            if (script != null && script.gameObject != null) 
            {
                string objectId = GetObjectId(script.gameObject);
                objectStates[objectId] = script.gameObject.activeSelf;
            }
        }
        
        if (objectStates.Count > 0) 
        {
            try {
                string json = JsonUtility.ToJson(new StateData { states = objectStates });
                File.WriteAllText(stateCachePath, json);
                if (debugLogsEnabled) 
                    Debug.Log($"Auto-saved {objectStates.Count} object states");
            }
            catch (System.Exception e) {
                Debug.LogError($"Error saving states: {e.Message}");
            }
        }
    }
    
    // Helper method to get a unique ID for a GameObject
    private static string GetObjectId(GameObject obj)
    {
        string scenePath = obj.scene.path;
        string objectPath = GetObjectPath(obj);
        return $"{scenePath}:{objectPath}";
    }
    
    private static string GetObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = $"{parent.name}/{path}";
            parent = parent.parent;
        }
        
        return path;
    }
    
    [System.Serializable]
    private class StateData
    {
        public Dictionary<string, bool> states = new Dictionary<string, bool>();
    }

    private static void OnBakeStarted()
    {
        // Find all AutoDisable components, even on inactive objects
        var components = Resources.FindObjectsOfTypeAll<AutoDisable>();
        foreach (var comp in components)
        {
            if (comp.enableDuringLightBake)
            {
                previousStates[comp] = comp.gameObject.activeSelf;
                comp.gameObject.SetActive(true);
            }
        }
    }

    private static void OnBakeCompleted()
    {
        foreach (var kv in previousStates)
        {
            if (kv.Key != null)
            {
                kv.Key.gameObject.SetActive(kv.Value);
            }
        }
        previousStates.Clear();
    }

    // Occlusion culling bake started
    private static void OnOcclusionBakeStarted()
    {
        if (debugLogsEnabled) Debug.Log("Occlusion culling started - saving states and enabling objects");
        var components = Resources.FindObjectsOfTypeAll<AutoDisable>();
        foreach (var comp in components)
        {
            if (comp.enableDuringOcclusionBake)
            {
                occlusionPreviousStates[comp] = comp.gameObject.activeSelf;
                comp.gameObject.SetActive(true);
            }
        }
        
        // Remove the delay call and call ForceOcclusionRefresh immediately
        ForceOcclusionRefresh();
    }
    
    // Occlusion culling bake completed
    private static void OnOcclusionBakeCompleted()
    {
        if (debugLogsEnabled) Debug.Log("Occlusion culling completed - restoring previous states");
        RestoreOcclusionStates();
    }
    
    private static void RestoreOcclusionStates()
    {
        if (debugLogsEnabled) Debug.Log($"Restoring {occlusionPreviousStates.Count} object states after occlusion culling");
        foreach (var kv in occlusionPreviousStates)
        {
            if (kv.Key != null)
            {
                kv.Key.gameObject.SetActive(kv.Value);
                if (debugLogsEnabled) Debug.Log($"Restored {kv.Key.gameObject.name} to {kv.Value}");
            }
        }
        occlusionPreviousStates.Clear();
    }

    // New update callback to also check occlusion bake status
    private static void CheckBakeStatus()
    {
        if (!Lightmapping.isRunning && previousStates.Count > 0)
        {
            OnBakeCompleted();
        }
        
        // Check for occlusion culling state changes with enhanced logging
        bool isCurrentlyBaking = StaticOcclusionCulling.isRunning;
        
        if (isCurrentlyBaking != wasOcclusionBaking && debugLogsEnabled)
        {
            Debug.Log($"Occlusion culling state changed: {wasOcclusionBaking} -> {isCurrentlyBaking}");
        }
        
        // If occlusion culling just started
        if (isCurrentlyBaking && !wasOcclusionBaking)
        {
            OnOcclusionBakeStarted();
        }
        
        // If occlusion culling just finished
        if (!isCurrentlyBaking && wasOcclusionBaking)
        {
            OnOcclusionBakeCompleted();
        }
        
        // Update current state
        wasOcclusionBaking = isCurrentlyBaking;
    }
    
    // Handle VRChat world builds
    private static void OnBuildPlayer(BuildPlayerOptions options)
    {
        // Find all AutoDisable components before building and disable them
        var components = Resources.FindObjectsOfTypeAll<AutoDisable>();
        vrcBuildPreviousStates.Clear();
        foreach (var comp in components)
        {
            vrcBuildPreviousStates[comp] = comp.gameObject.activeSelf;
            comp.gameObject.SetActive(false);
        }
        
        // Delay the build call to ensure the disabled state is applied
        EditorApplication.delayCall += () =>
        {
            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        };
    }

    private static void ForceOcclusionRefresh()
    {
        if (StaticOcclusionCulling.isRunning)
        {
            StaticOcclusionCulling.Clear();
            StaticOcclusionCulling.GenerateInBackground();
        }
    }
    
    // Method to restore VRC build states
    public static void RestoreVRCBuildStates()
    {
        if (debugLogsEnabled) Debug.Log($"Restoring {vrcBuildPreviousStates.Count} object states after VRChat build");
        
        // Check if there are any states to restore
        if (vrcBuildPreviousStates.Count == 0)
        {
            // Try to load states from EditorPrefs if dictionary is empty (might happen after domain reload)
            LoadVRCStatesFromPrefs();
        }
        
        foreach (var kv in vrcBuildPreviousStates)
        {
            if (kv.Key != null)
            {
                if (kv.Key.gameObject != null) // Check if the game object still exists
                {
                    kv.Key.gameObject.SetActive(kv.Value);
                    if (debugLogsEnabled) Debug.Log($"Restored {kv.Key.gameObject.name} to {kv.Value}");
                }
                else
                {
                    if (debugLogsEnabled) Debug.LogWarning($"GameObject associated with {kv.Key.name} is null. Skipping.");
                }
            }
        }
        
        // Clear stored states and prefs
        vrcBuildPreviousStates.Clear();
        ClearVRCStatesFromPrefs();
    }
    
    // Save VRC states to EditorPrefs to survive domain reloads
    internal static void SaveVRCStatesToPrefs()
    {
        if (debugLogsEnabled) Debug.Log($"Saving {vrcBuildPreviousStates.Count} VRC states to EditorPrefs");
        
        // Record how many objects we have
        EditorPrefs.SetInt("AutoDisable_VRCStateCount", vrcBuildPreviousStates.Count);
        
        int index = 0;
        foreach (var kv in vrcBuildPreviousStates)
        {
            if (kv.Key != null && kv.Key.gameObject != null)
            {
                // Save the object path and active state
                string objectId = GetObjectId(kv.Key.gameObject);
                EditorPrefs.SetString($"AutoDisable_VRCState_{index}_Path", objectId);
                EditorPrefs.SetBool($"AutoDisable_VRCState_{index}_Value", kv.Value);
                index++;
            }
        }
    }
    
    // Load VRC states from EditorPrefs
    internal static void LoadVRCStatesFromPrefs()
    {
        int count = EditorPrefs.GetInt("AutoDisable_VRCStateCount", 0);
        if (count == 0) return;
        
        if (debugLogsEnabled) Debug.Log($"Loading {count} VRC states from EditorPrefs");
        
        // Clear existing states
        vrcBuildPreviousStates.Clear();
        
        // Find all AutoDisable components in the scene
        var allComponents = Resources.FindObjectsOfTypeAll<AutoDisable>();
        
        // Create a lookup dictionary for quick object finding
        Dictionary<string, AutoDisable> objectLookup = new Dictionary<string, AutoDisable>();
        foreach (var comp in allComponents)
        {
            if (comp != null && comp.gameObject != null)
            {
                string id = GetObjectId(comp.gameObject);
                objectLookup[id] = comp;
            }
        }
        
        // Load the saved states
        for (int i = 0; i < count; i++)
        {
            string path = EditorPrefs.GetString($"AutoDisable_VRCState_{i}_Path", "");
            bool value = EditorPrefs.GetBool($"AutoDisable_VRCState_{i}_Value", false);
            
            if (!string.IsNullOrEmpty(path) && objectLookup.TryGetValue(path, out AutoDisable comp))
            {
                vrcBuildPreviousStates[comp] = value;
                if (debugLogsEnabled) Debug.Log($"Loaded state for {comp.gameObject.name}: {value}");
            }
        }
    }
    
    // Clear VRC states from EditorPrefs
    internal static void ClearVRCStatesFromPrefs()
    {
        int count = EditorPrefs.GetInt("AutoDisable_VRCStateCount", 0);
        
        for (int i = 0; i < count; i++)
        {
            EditorPrefs.DeleteKey($"AutoDisable_VRCState_{i}_Path");
            EditorPrefs.DeleteKey($"AutoDisable_VRCState_{i}_Value");
        }
        
        EditorPrefs.DeleteKey("AutoDisable_VRCStateCount");
    }
    
    // Add menu items to toggle debug logs
    [MenuItem("izy/AutoDisable/Enable Debug Logs")]
    private static void EnableDebugLogs()
    {
        debugLogsEnabled = true;
        EditorPrefs.SetBool("AutoDisable_DebugLogsEnabled", true);
        Debug.Log("AutoDisable: Debug logs enabled");
    }
    
    [MenuItem("izy/AutoDisable/Enable Debug Logs", true)]
    private static bool ValidateEnableDebugLogs()
    {
        return !debugLogsEnabled;
    }
    
    [MenuItem("izy/AutoDisable/Disable Debug Logs")]
    private static void DisableDebugLogs()
    {
        debugLogsEnabled = false;
        EditorPrefs.SetBool("AutoDisable_DebugLogsEnabled", false);
        Debug.Log("AutoDisable: Debug logs disabled");
    }
    
    [MenuItem("izy/AutoDisable/Disable Debug Logs", true)]
    private static bool ValidateDisableDebugLogs()
    {
        return debugLogsEnabled;
    }
}

// 2) Implement IVRCSDKBuildRequestedCallback in a new class within the same file
public class AutoDisableVRCBuildCallbacks : IVRCSDKBuildRequestedCallback
{
    // 3) This order can stay 0 unless you need a specific callback order
    public int callbackOrder => 0;

    // This method is called by the VRChat SDK before building
    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (AutoDisableManager.debugLogsEnabled) 
            Debug.Log("VRChat build requested - saving states and disabling objects");
        
        // Store previous states and disable objects
        var components = Resources.FindObjectsOfTypeAll<AutoDisable>();
        AutoDisableManager.vrcBuildPreviousStates.Clear();  // Updated reference
        foreach (var comp in components)
        {
            // Save the current state before disabling
            AutoDisableManager.vrcBuildPreviousStates[comp] = comp.gameObject.activeSelf;  // Updated reference
            comp.gameObject.SetActive(false);
        }

        // Save states to EditorPrefs so they survive domain reloads
        AutoDisableManager.SaveVRCStatesToPrefs();

        // Register for build completion events to restore states later
        EditorApplication.update += CheckBuildCompletion;
        
        // Also register for the playmode state change as a backup restoration point
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        // Return true to continue the build
        return true;
    }
    
    // Making this static to ensure it persists
    private static bool wasBuildingPlayer = false;
    
    // Method to check if build has completed or been cancelled - made static
    private static void CheckBuildCompletion()
    {
        bool isCurrentlyBuilding = BuildPipeline.isBuildingPlayer;
        
        // If we were building but now we've stopped (completed, failed, or cancelled)
        if (wasBuildingPlayer && !isCurrentlyBuilding)
        {
            if (AutoDisableManager.debugLogsEnabled)
                Debug.Log("VRChat build ended detected - restoring states");
                
            EditorApplication.delayCall += () => {
                AutoDisableManager.RestoreVRCBuildStates();
            };
            
            // Unregister this update check
            EditorApplication.update -= CheckBuildCompletion;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
        
        wasBuildingPlayer = isCurrentlyBuilding;
    }
    
    // Additional restoration point if build triggers play mode changes
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (AutoDisableManager.debugLogsEnabled)
                Debug.Log("Entered edit mode after VRChat build - restoring states");
                
            EditorApplication.delayCall += () => {
                AutoDisableManager.RestoreVRCBuildStates();
            };
            
            // Unregister callbacks
            EditorApplication.update -= CheckBuildCompletion;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
    }
}

// Also implement post-processor to catch any other build completion scenarios
public class AutoDisableBuildPostprocessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 999; // Run after other post-processors
    
    public void OnPostprocessBuild(BuildReport report)
    {
        // Restore states after build is complete
        if (AutoDisableManager.debugLogsEnabled)
            Debug.Log("Build post-processor called - restoring states");
            
        EditorApplication.delayCall += () => {
            AutoDisableManager.RestoreVRCBuildStates();
        };
    }
}

// This class can keep its name as it's a proper CustomEditor
[CustomEditor(typeof(AutoDisable))]
public class AutoDisableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        // Button removed - state saving is now automatic
    }
}
#endif
