#if UNITY_EDITOR
using UnityEditor;

namespace Puzzle_Elements.IK_OBJECT.Scripts
{
    [CustomEditor(typeof(IKObjectPreset))]
    public class IKObjectPresetEditor : Editor
    {
        SerializedProperty p_systemType;
        SerializedProperty p_boneCount;

        // Movement
        SerializedProperty p_movement_speed;
        SerializedProperty p_movement_distanceThreshold;

        // Rotation
        SerializedProperty p_rotation_remapLerp;
        SerializedProperty p_rotation_arcHeight;
        SerializedProperty p_rotation_moveDelay;
        SerializedProperty p_rotation_travelTime;
        SerializedProperty p_rotation_stopDuration;

        // Stairs
        SerializedProperty p_stairs_useRotationValues;
        SerializedProperty p_stairs_remapLerp;
        SerializedProperty p_stairs_arcHeight;
        SerializedProperty p_stairs_moveDelay;
        SerializedProperty p_stairs_travelTime;
        SerializedProperty p_stairs_stopDuration;

        void OnEnable()
        {
            p_systemType = serializedObject.FindProperty("systemType");
            p_boneCount = serializedObject.FindProperty("boneCount");

            p_movement_speed = serializedObject.FindProperty("movement_speed");
            p_movement_distanceThreshold = serializedObject.FindProperty("movement_distanceThreshold");

            p_rotation_remapLerp = serializedObject.FindProperty("rotation_remapLerp");
            p_rotation_arcHeight = serializedObject.FindProperty("rotation_arcHeight");
            p_rotation_moveDelay = serializedObject.FindProperty("rotation_moveDelay");
            p_rotation_travelTime = serializedObject.FindProperty("rotation_travelTime");
            p_rotation_stopDuration = serializedObject.FindProperty("rotation_stopDuration");

            p_stairs_useRotationValues = serializedObject.FindProperty("stairs_useRotationValues");
            p_stairs_remapLerp = serializedObject.FindProperty("stairs_remapLerp");
            p_stairs_arcHeight = serializedObject.FindProperty("stairs_arcHeight");
            p_stairs_moveDelay = serializedObject.FindProperty("stairs_moveDelay");
            p_stairs_travelTime = serializedObject.FindProperty("stairs_travelTime");
            p_stairs_stopDuration = serializedObject.FindProperty("stairs_stopDuration");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(p_systemType);
            EditorGUILayout.PropertyField(p_boneCount);

            var mode = (IKSystemType)p_systemType.enumValueIndex;
            EditorGUILayout.Space(6);

            switch (mode)
            {
                case IKSystemType.IK_Distance:
                case IKSystemType.IK_DistanceInverse:
                    // NO mostramos nada mas. Todo lo demas se setea en la jerarquia.
                    EditorGUILayout.HelpBox("Solo Bone Count es editable para Distance/Inverse. Las demas variables se configuran en la jerarquia.", MessageType.Info);
                    break;

                case IKSystemType.IK_PlatformMovement:
                    DrawMovementSection();
                    break;

                case IKSystemType.IK_PlatformRotation:
                    DrawRotationSection();
                    break;

                case IKSystemType.IK_PlatformRotation_Stairs:
                    DrawStairsSection();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawMovementSection()
        {
            EditorGUILayout.LabelField("Platform Movement", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_movement_speed);
            EditorGUILayout.PropertyField(p_movement_distanceThreshold);
        }

        void DrawRotationSection()
        {
            EditorGUILayout.LabelField("Platform Rotation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_rotation_remapLerp);
            EditorGUILayout.PropertyField(p_rotation_arcHeight);
            EditorGUILayout.PropertyField(p_rotation_moveDelay);
            EditorGUILayout.PropertyField(p_rotation_travelTime);
            EditorGUILayout.PropertyField(p_rotation_stopDuration);
        }

        void DrawStairsSection()
        {
            EditorGUILayout.LabelField("Platform Rotation Stairs", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(p_stairs_useRotationValues);
            if (!p_stairs_useRotationValues.boolValue)
            {
                EditorGUILayout.PropertyField(p_stairs_remapLerp);
                EditorGUILayout.PropertyField(p_stairs_arcHeight);
                EditorGUILayout.PropertyField(p_stairs_moveDelay);
                EditorGUILayout.PropertyField(p_stairs_travelTime);
                EditorGUILayout.PropertyField(p_stairs_stopDuration);
            }
            else
            {
                EditorGUILayout.HelpBox("Usando valores de Platform Rotation.", MessageType.None);
            }
        }
    }
}
#endif
