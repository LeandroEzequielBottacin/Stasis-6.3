using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SequenceController))]
public class SequenceControllerEditor : UnityEditor.Editor
{
    private SerializedProperty actions;
    private SerializedProperty playOnStart;
    private SerializedProperty loop;
    private SerializedProperty loopFromIndex;

    private void OnEnable()
    {
        actions = serializedObject.FindProperty("actions");
        playOnStart = serializedObject.FindProperty("playOnStart");
        loop = serializedObject.FindProperty("loop");
        loopFromIndex = serializedObject.FindProperty("loopFromIndex");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // =====================================================
        // SETTINGS
        // =====================================================

        EditorGUILayout.LabelField(
            "Sequence",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space(4);

        DrawActions();

        EditorGUILayout.Space(15);

        EditorGUILayout.LabelField(
            "Settings",
            EditorStyles.boldLabel
        );

        EditorGUILayout.PropertyField(playOnStart);
        EditorGUILayout.PropertyField(loop);

        if (loop.boolValue)
        {
            EditorGUILayout.PropertyField(loopFromIndex);
        }

        serializedObject.ApplyModifiedProperties();
    }


    private void DrawActions()
    {
        for (int i = 0; i < actions.arraySize; i++)
        {
            SerializedProperty element =
                actions.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            string actionName = GetActionName(element);

            element.isExpanded = EditorGUILayout.Foldout(
                element.isExpanded,
                $"{i} - {actionName}",
                true
            );

            if (GUILayout.Button("^", GUILayout.Width(30)))
            {
                if (i > 0)
                {
                    actions.MoveArrayElement(i, i - 1);
                    break;
                }
            }

            if (GUILayout.Button("v", GUILayout.Width(30)))
            {
                if (i < actions.arraySize - 1)
                {
                    actions.MoveArrayElement(i, i + 1);
                    break;
                }
            }

            // BORRAR
            if (GUILayout.Button("X", GUILayout.Width(30)))
            {
                actions.DeleteArrayElementAtIndex(i);
                break;
            }

            EditorGUILayout.EndHorizontal();

            if (element.isExpanded)
            {
                EditorGUILayout.Space(5);

                DrawChildren(element);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("+ Add Action", GUILayout.Height(30)))
        {
            ShowAddMenu();
        }
    }

    // =========================================================
    // DRAW ACTION CONTENT
    // =========================================================

    private void DrawChildren(SerializedProperty property)
    {
        SerializedProperty iterator = property.Copy();

        SerializedProperty endProperty =
            iterator.GetEndProperty();

        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            if (SerializedProperty.EqualContents(
                iterator,
                endProperty))
            {
                break;
            }

            EditorGUILayout.PropertyField(
                iterator,
                true
            );

            enterChildren = false;
        }
    }

    // =========================================================
    // ADD MENU
    // =========================================================

    private void ShowAddMenu()
    {
        GenericMenu menu = new GenericMenu();

        menu.AddItem(
            new GUIContent("Wait"),
            false,
            () => AddAction(
                new SequenceController.WaitAction()
            )
        );

        menu.AddItem(
            new GUIContent("Grab"),
            false,
            () => AddAction(
                new SequenceController.GrabAction()
            )
        );

        menu.AddItem(
            new GUIContent("Move"),
            false,
            () => AddAction(
                new SequenceController.MoveAction()
            )
        );

        menu.AddItem(
            new GUIContent("Look At"),
            false,
            () => AddAction(
                new SequenceController.LookAtAction()
            )
        );

        menu.AddItem(
            new GUIContent("Set Active"),
            false,
            () => AddAction(
                new SequenceController.SetActiveAction()
            )
        );

        menu.AddItem(
            new GUIContent("Invoke Event"),
            false,
            () => AddAction(
                new SequenceController.InvokeEventAction()
            )
        );

        menu.AddSeparator("");

        menu.AddItem(
            new GUIContent("Shoot"),
            false,
            () => AddAction(
                new SequenceController.ShootAction()
            )
        );

        menu.AddItem(
            new GUIContent("Claw"),
            false,
            () => AddAction(
                new SequenceController.ClawAction()
            )
        );

        // SIEMPRE AL FINAL
        menu.ShowAsContext();
    }
    // =========================================================
    // ADD ACTION
    // =========================================================

    private void AddAction(
        SequenceController.SequenceAction action)
    {
        serializedObject.Update();

        int index = actions.arraySize;

        actions.InsertArrayElementAtIndex(index);

        SerializedProperty element =
            actions.GetArrayElementAtIndex(index);

        element.managedReferenceValue = action;

        element.isExpanded = true;

        serializedObject.ApplyModifiedProperties();
    }

    // =========================================================
    // NAME
    // =========================================================

    private string GetActionName(
        SerializedProperty property)
    {
        if (property.managedReferenceValue == null)
            return "NULL";

        string name =
            property.managedReferenceValue
                .GetType()
                .Name;

        name = name.Replace("Action", "");

        return name;
    }
}