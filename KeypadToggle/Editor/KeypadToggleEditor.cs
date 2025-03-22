using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(KeypadToggle))]
public class KeypadToggleEditor : Editor
{
    private SerializedProperty targetObjectsProperty;
    private SerializedProperty additionalPasscodeNumbersProperty;
    private SerializedProperty disableKeypadGrantedProperty;
    private SerializedProperty disableOnCorrectProperty;
    private SerializedProperty disableDelayProperty;

    private void OnEnable()
    {
        targetObjectsProperty = serializedObject.FindProperty("targetObjects");
        additionalPasscodeNumbersProperty = serializedObject.FindProperty("AdditionalPasscodeNumbers");
        disableKeypadGrantedProperty = serializedObject.FindProperty("DisableKeypadGranted");
        disableOnCorrectProperty = serializedObject.FindProperty("disableOnCorrect");
        disableDelayProperty = serializedObject.FindProperty("disableDelay");
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
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Toggle Settings", EditorStyles.boldLabel);
        
        EditorGUILayout.PropertyField(disableOnCorrectProperty, 
            new GUIContent("Disable On Correct", "If true, objects will be disabled when the correct code is entered"));
            
        // Only show delay field if disableOnCorrect is true
        if (disableOnCorrectProperty.boolValue)
        {
            EditorGUILayout.PropertyField(disableDelayProperty,
                new GUIContent("Disable Delay (seconds)", "Delay in seconds before disabling objects (0 = immediate)"));
                
            // Ensure delay is not negative
            if (disableDelayProperty.floatValue < 0f)
                disableDelayProperty.floatValue = 0f;
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}
