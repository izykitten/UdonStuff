#if UNITY_EDITOR
using UnityEngine;
using UnityEditor; // Still required for delayCall

[ExecuteAlways]
public class AutoDisable : MonoBehaviour
{
    // Added option: enableDuringLightBake is true by default
    public bool enableDuringLightBake = true;

    void Start()
    {
        gameObject.SetActive(false);
    }

    #if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () =>
            {
                if (this) // ensure object still exists
                {
                    gameObject.SetActive(false);
                }
            };
        }
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
