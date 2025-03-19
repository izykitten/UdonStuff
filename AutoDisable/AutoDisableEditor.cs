#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class AutoDisableEditor
{
    private static Dictionary<AutoDisable, bool> previousStates = new Dictionary<AutoDisable, bool>();

    static AutoDisableEditor()
    {
        Lightmapping.bakeStarted += OnBakeStarted;
        Lightmapping.bakeCompleted += OnBakeCompleted;
        EditorApplication.update += CheckBakeStatus;
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

    // New update callback to detect bake cancellation/error and restore state if needed
    private static void CheckBakeStatus()
    {
        if (!Lightmapping.isRunning && previousStates.Count > 0)
        {
            OnBakeCompleted();
        }
    }
}
#endif
