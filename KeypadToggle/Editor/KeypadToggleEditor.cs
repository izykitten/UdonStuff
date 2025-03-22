using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(KeypadToggle))]
public class KeypadToggleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        KeypadToggle script = (KeypadToggle)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("KeypadToggle", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This component activates objects when a keypad is used and deactivates them when closed.\n\n" +
            "• Target Objects: Objects to toggle on/off\n" +
            "• Close Delay: Time before disabling objects (0=immediate, -1=never)", 
            MessageType.Info);
        
        EditorGUILayout.Space();
        DrawDefaultInspector();
        
        // Testing section (only available in play mode)
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);
        
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Open Keypad (Activate)"))
        {
            script._keypadGranted();
        }
        if (GUILayout.Button("Close Keypad"))
        {
            script._keypadClosed();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Activate"))
        {
            script.ForceActivate();
        }
        if (GUILayout.Button("Force Deactivate"))
        {
            script.ForceDeactivate();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.EndDisabledGroup();
    }
}
