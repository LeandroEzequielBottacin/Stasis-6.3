using _Ian.VFX.Smoke;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HazardController))]
public class HazardControllerEditor : UnityEditor.Editor
{
    private SerializedProperty hazards;

    private void OnEnable()
    {
        hazards = serializedObject.FindProperty("hazards");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField(
            "Hazards",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space(5);

        DrawHazards();

        serializedObject.ApplyModifiedProperties();
    }

    // =========================================================
    // HAZARD LIST
    // =========================================================

    private void DrawHazards()
    {
        for (int i = 0; i < hazards.arraySize; i++)
        {
            SerializedProperty element =
                hazards.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            string hazardName = GetHazardName(element);

            element.isExpanded = EditorGUILayout.Foldout(
                element.isExpanded,
                $"{i} - {hazardName}",
                true
            );

            if (GUILayout.Button("^", GUILayout.Width(30)))
            {
                if (i > 0)
                {
                    hazards.MoveArrayElement(i, i - 1);
                    break;
                }
            }

            if (GUILayout.Button("v", GUILayout.Width(30)))
            {
                if (i < hazards.arraySize - 1)
                {
                    hazards.MoveArrayElement(i, i + 1);
                    break;
                }
            }

            if (GUILayout.Button("X", GUILayout.Width(30)))
            {
                hazards.DeleteArrayElementAtIndex(i);
                break;
            }

            EditorGUILayout.EndHorizontal();

            if (element.isExpanded)
            {
                EditorGUILayout.Space(5);

                DrawHazardContent(element);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("+ Add Hazard", GUILayout.Height(32)))
        {
            AddHazard();
        }
    }

    // =========================================================
    // HAZARD CONTENT
    // =========================================================

    private void DrawHazardContent(SerializedProperty hazard)
    {
        SerializedProperty iterator = hazard.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();

        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            if (SerializedProperty.EqualContents(iterator, endProperty))
                break;

            if (iterator.name == "hazardModule")
            {
                DrawHazardModule(iterator);
            }
            else
            {
                EditorGUILayout.PropertyField(iterator, true);
            }

            enterChildren = false;
        }
    }

    // =========================================================
    // HAZARD MODULE
    // =========================================================

    private void DrawHazardModule(SerializedProperty module)
    {
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField(
            "Hazard Module",
            EditorStyles.boldLabel
        );

        if (module.managedReferenceValue != null)
        {
            EditorGUILayout.PropertyField(
                module,
                true
            );
        }

        string currentName = GetModuleName(module);

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            $"Type: {currentName}"
        );

        if (GUILayout.Button("Select", GUILayout.Width(80)))
        {
            ShowHazardModuleMenu(module);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
    }

    // =========================================================
    // MODULE MENU
    // =========================================================

    private void ShowHazardModuleMenu(SerializedProperty module)
    {
        string propertyPath = module.propertyPath;

        GenericMenu menu = new GenericMenu();

        menu.AddItem(
            new GUIContent("Slow"),
            false,
            () =>
            {
                serializedObject.Update();

                SerializedProperty freshModule =
                    serializedObject.FindProperty(propertyPath);

                if (freshModule == null)
                {
                    Debug.LogError(
                        $"No se pudo encontrar HazardModule en: {propertyPath}"
                    );

                    return;
                }

                freshModule.managedReferenceValue =
                    new SlowHazardModule();

                serializedObject.ApplyModifiedProperties();
                Repaint();
            }
        );

        menu.AddSeparator("");

        menu.AddItem(
            new GUIContent("None"),
            false,
            () =>
            {
                serializedObject.Update();

                SerializedProperty freshModule =
                    serializedObject.FindProperty(propertyPath);

                if (freshModule == null)
                    return;

                freshModule.managedReferenceValue = null;

                serializedObject.ApplyModifiedProperties();
                Repaint();
            }
        );

        menu.ShowAsContext();
    }

    // =========================================================
    // ADD HAZARD
    // =========================================================

    private void AddHazard()
    {
        serializedObject.Update();

        int index = hazards.arraySize;

        hazards.InsertArrayElementAtIndex(index);

        SerializedProperty element =
            hazards.GetArrayElementAtIndex(index);

        element.managedReferenceValue =
            new HazardController.HazardEntry();

        element.isExpanded = true;

        serializedObject.ApplyModifiedProperties();
    }

    // =========================================================
    // NAMES
    // =========================================================

    private string GetHazardName(SerializedProperty property)
    {
        if (property.managedReferenceValue == null)
            return "NULL";

        SerializedProperty nameProperty =
            property.FindPropertyRelative("name");

        if (nameProperty == null ||
            string.IsNullOrWhiteSpace(nameProperty.stringValue))
        {
            return "Hazard";
        }

        return nameProperty.stringValue;
    }

    private string GetModuleName(SerializedProperty module)
    {
        if (module.managedReferenceValue == null)
            return "None";

        return module.managedReferenceValue
            .GetType()
            .Name
            .Replace("HazardModule", "");
    }
}