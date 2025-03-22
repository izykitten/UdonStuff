using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(SlidingDoor))]
public class SlidingDoorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Update serialized object
        serializedObject.Update();

        // Get the current operation mode
        SerializedProperty operationModeProp = serializedObject.FindProperty("operationModeValue");
        int operationMode = operationModeProp.intValue;

        // Draw default properties (except our int-backed fields)
        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            // Skip our int-backed fields
            if (prop.name == "operationModeValue" || 
                prop.name == "doorLockedValue" || prop.name == "enableLockingSystemValue" ||
                prop.name == "playCloseSoundInReverse") // Skip the duplicate checkbox
                continue;
                
            // Skip the proximitySensorCollider field if not using proximity sensor
            if (prop.name == "proximitySensorCollider" && operationMode != 2)
                continue;
                
            EditorGUILayout.PropertyField(prop, true);
            enterChildren = false;
        }

        // Draw operation mode as an enum dropdown
        operationModeProp.intValue = EditorGUILayout.Popup("Operation Mode", operationModeProp.intValue, 
            new string[] { "Normal", "Proximity Sensor" });

        // Draw proximitySensorCollider field when using Proximity Sensor mode
        if (operationMode == 2) // Proximity Sensor
        {
            EditorGUILayout.HelpBox("Configure your collider's size and shape in the Inspector for that GameObject. Make sure the collider is set as a Trigger.", MessageType.Info);
        }

        // Draw custom checkbox for Enable Locking System
        SerializedProperty enableLockingProp = serializedObject.FindProperty("enableLockingSystemValue");
        bool enableLockingBool = enableLockingProp.intValue != 0;
        enableLockingBool = EditorGUILayout.Toggle("Enable Locking System", enableLockingBool);
        enableLockingProp.intValue = enableLockingBool ? 1 : 0;

        // Draw custom checkbox for Door Locked
        SerializedProperty doorLockedProp = serializedObject.FindProperty("doorLockedValue");
        bool doorLockedBool = doorLockedProp.intValue != 0;
        doorLockedBool = EditorGUILayout.Toggle("Door Locked", doorLockedBool);
        doorLockedProp.intValue = doorLockedBool ? 1 : 0;

        // Draw custom checkbox for Play Close Sound In Reverse
        SerializedProperty playCloseSoundInReverseProp = serializedObject.FindProperty("playCloseSoundInReverse");
        bool playCloseSoundInReverseBool = playCloseSoundInReverseProp.boolValue;
        playCloseSoundInReverseBool = EditorGUILayout.Toggle("Play Close Sound In Reverse", playCloseSoundInReverseBool);
        playCloseSoundInReverseProp.boolValue = playCloseSoundInReverseBool;

        // Apply changes
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
