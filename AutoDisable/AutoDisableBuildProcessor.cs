#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

[InitializeOnLoad]
public class AutoDisableBuildProcessor
{
    private static Dictionary<string, bool> objectStates = new Dictionary<string, bool>();
    private static string stateCachePath = "Library/AutoDisableStates.json";
    private static bool initialized = false;
    private static bool wasBuilding = false;

    static AutoDisableBuildProcessor()
    {
        EditorApplication.delayCall += Initialize;
        EditorApplication.update += CheckBuildStatus;
    }

    static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        // Save states at the start of builds
        EditorApplication.delayCall += () => {
            SaveCurrentStates();
            Debug.Log("AutoDisableBuildProcessor: Initialized and saved initial states");
        };
    }

    // Check for build status changes
    static void CheckBuildStatus()
    {
        bool isCurrentlyBuilding = BuildPipeline.isBuildingPlayer;
        
        // If build just started
        if (isCurrentlyBuilding && !wasBuilding)
        {
            SaveCurrentStates();
            Debug.Log("AutoDisableBuildProcessor: Build started - states saved");
        }
        
        // If build just ended
        if (!isCurrentlyBuilding && wasBuilding)
        {
            RestoreStatesIfNeeded();
            Debug.Log("AutoDisableBuildProcessor: Build ended - states restored");
        }
        
        wasBuilding = isCurrentlyBuilding;
    }

    static void SaveCurrentStates()
    {
        objectStates.Clear();
        
        var scripts = Object.FindObjectsOfType<AutoDisable>(true); // Include inactive objects
        foreach (var script in scripts)
        {
            if (script && script.gameObject)
            {
                // Use a unique identifier combining scene path and object path
                string objectId = GetObjectId(script.gameObject);
                objectStates[objectId] = script.gameObject.activeSelf;
                Debug.Log($"Saved state for {script.gameObject.name}: {script.gameObject.activeSelf}");
            }
        }
        
        // Write states to disk so they survive domain reloads
        if (objectStates.Count > 0)
        {
            string json = JsonUtility.ToJson(new StateData { states = objectStates });
            File.WriteAllText(stateCachePath, json);
            Debug.Log($"Saved {objectStates.Count} object states to {stateCachePath}");
        }
    }

    static void RestoreStatesIfNeeded()
    {
        if (File.Exists(stateCachePath))
        {
            string json = File.ReadAllText(stateCachePath);
            try 
            {
                StateData data = JsonUtility.FromJson<StateData>(json);
                objectStates = data.states;
                
                var scripts = Object.FindObjectsOfType<AutoDisable>(true); // Include inactive objects
                foreach (var script in scripts)
                {
                    if (script && script.gameObject)
                    {
                        string objectId = GetObjectId(script.gameObject);
                        if (objectStates.TryGetValue(objectId, out bool wasActive))
                        {
                            script.gameObject.SetActive(wasActive);
                            Debug.Log($"Restored {script.gameObject.name} to {wasActive}");
                        }
                    }
                }
                
                // Clear the file after restoring
                File.Delete(stateCachePath);
                Debug.Log("Deleted state cache file after restoration");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error restoring AutoDisable states: {e.Message}");
            }
        }
    }

    static string GetObjectId(GameObject obj)
    {
        string scenePath = obj.scene.path;
        string objectPath = GetObjectPath(obj);
        return $"{scenePath}:{objectPath}";
    }

    static string GetObjectPath(GameObject obj)
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
}
#endif