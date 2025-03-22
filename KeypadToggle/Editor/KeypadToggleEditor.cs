using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(KeypadToggle))]
public class KeypadToggleEditor : Editor
{
    private SerializedProperty targetObjectsProperty;
    private SerializedProperty additionalPasscodeNumbersProperty;
    private SerializedProperty disableKeypadGrantedProperty;
    private SerializedProperty closeDelayProperty;

    private void OnEnable()
    {
        targetObjectsProperty = serializedObject.FindProperty("targetObjects");
        additionalPasscodeNumbersProperty = serializedObject.FindProperty("AdditionalPasscodeNumbers");
        disableKeypadGrantedProperty = serializedObject.FindProperty("DisableKeypadGranted");
        closeDelayProperty = serializedObject.FindProperty("closeDelay");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(targetObjectsProperty, 
            new GUIContent("Target Objects", "The objects to enable/disable when the keypad is triggered"), true);
        
        EditorGUILayout.PropertyField(additionalPasscodeNumbersProperty, 
            new GUIContent("Additional Passcode Numbers", "Additional passcodes to be recognized"), true);
        
        EditorGUILayout.PropertyField(disableKeypadGrantedProperty, 
            new GUIContent("Disable Keypad Granted", "If true, the keypad granted event will be ignored"));
        
        EditorGUILayout.PropertyField(closeDelayProperty,
            new GUIContent("Close Delay (seconds)", "Delay in seconds before disabling objects after keypad is closed (0 = immediate, -1 = never disable)"));
        
        serializedObject.ApplyModifiedProperties();
    }
}
