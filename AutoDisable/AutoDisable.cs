using UnityEngine;
#if UNITY_EDITOR
using UnityEditor; // Required for delayCall
#endif

[AddComponentMenu("izy/AutoDisable")]
public class AutoDisable : MonoBehaviour
{
    // Properties that need to be available to AutoDisableEditor
    public bool enableDuringLightBake = true;
    public bool enableDuringOcclusionBake = true;

    // Add a boolean to control the initial active state
    public bool startEnabled = false;

    void Awake()
    {
        // Set the object's active state based on the startEnabled property
        gameObject.SetActive(startEnabled);
    }

    #if UNITY_EDITOR
    void OnValidate()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !BuildPipeline.isBuildingPlayer)
        {
            return;
        }
        EditorApplication.delayCall += () =>
        {
            if (this) // ensure object still exists
            {
                gameObject.SetActive(false);
                DestroyImmediate(this);
            }
        };
    }

    // Add a method to highlight the selected file
    void HighlightSelectedFile()
    {
        // Assuming there's a way to set the color or style of the file
        // This is a placeholder for the actual implementation
        // For example, changing the background color to indicate selection
        gameObject.GetComponent<Renderer>().material.color = Color.yellow;
    }
    #endif
}
