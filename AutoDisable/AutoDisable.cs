using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("izy/AutoDisable")]
public class AutoDisable : MonoBehaviour
{
    // Properties that need to be available to AutoDisableEditor
    public bool enableDuringLightBake = true;
    public bool enableDuringOcclusionBake = true;
    public bool startEnabled = false; // Controls initial state in the built game
    
    void Awake()
    {
        // Set the object's active state based on the startEnabled property
        // But only in builds, not in play mode
        #if !UNITY_EDITOR
        gameObject.SetActive(startEnabled);
        Destroy(this);
        #else
        // In editor play mode, we want to disable the object
        gameObject.SetActive(false);
        #endif
    }

    #if UNITY_EDITOR
    void OnValidate()
    {
        // Original behavior: disable object when entering play mode
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null && gameObject != null)
                {
                    gameObject.SetActive(false);
                }
            };
        }
        // When building, the state will be handled by AutoDisableBuildProcessor
    }
    #endif
}
