#if UNITY_EDITOR
using UnityEngine;
using UnityEditor; // Still required for delayCall

public class AutoDisable : MonoBehaviour
{
    // Added option: enableDuringLightBake is true by default
    public bool enableDuringLightBake = true;

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
    #endif

    // Add a method to highlight the selected file
    void HighlightSelectedFile()
    {
        // Assuming there's a way to set the color or style of the file
        // This is a placeholder for the actual implementation
        // For example, changing the background color to indicate selection
        gameObject.GetComponent<Renderer>().material.color = Color.yellow;
    }
}
#endif
