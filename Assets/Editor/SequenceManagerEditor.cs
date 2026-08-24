using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SequenceManager))]
public class SequenceManagerEditor : UnityEditor.Editor
{
    private SequenceManager manager;
    private readonly List<bool> foldouts = new();

    private void OnEnable()
    {
        manager = (SequenceManager)target;
        SynchronizeFoldouts();
    }

    public override void OnInspectorGUI()
    {
        if (manager == null)
            return;

        SynchronizeFoldouts();

        EditorGUILayout.LabelField("Sequence Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        for (int i = 0; i < manager.SequenceGroups.Count; i++)
        {
            if (DrawGroup(i))
                break;

            EditorGUILayout.Space(4);
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("+ ADD", GUILayout.Height(30)))
            AddGroup();
    }

    private bool DrawGroup(int index)
    {
        SequenceManager.SequenceGroup group = manager.SequenceGroups[index];

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        string title = string.IsNullOrWhiteSpace(group.name)
            ? $"{index} - Sequence Group"
            : $"{index} - {group.name}";

        foldouts[index] = EditorGUILayout.Foldout(
            foldouts[index],
            title,
            true
        );

        if (GUILayout.Button("^", GUILayout.Width(30)) && index > 0)
        {
            MoveGroup(index, index - 1);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }

        if (GUILayout.Button("v", GUILayout.Width(30)) &&
            index < manager.SequenceGroups.Count - 1)
        {
            MoveGroup(index, index + 1);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }

        if (GUILayout.Button("X", GUILayout.Width(30)))
        {
            RemoveGroup(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return true;
        }

        EditorGUILayout.EndHorizontal();

        if (foldouts[index])
        {
            EditorGUILayout.Space(5);
            DrawGroupContent(group);
        }

        EditorGUILayout.EndVertical();
        return false;
    }

    private void DrawGroupContent(SequenceManager.SequenceGroup group)
    {
        EditorGUI.BeginChangeCheck();

        string newName = EditorGUILayout.TextField("Name", group.name);

        Collider newEntryCollider = (Collider)EditorGUILayout.ObjectField(
            "Entry Collider",
            group.entryCollider,
            typeof(Collider),
            true
        );

        Collider newExitCollider = (Collider)EditorGUILayout.ObjectField(
            "Exit Collider",
            group.exitCollider,
            typeof(Collider),
            true
        );

        bool newIsActive = EditorGUILayout.Toggle(
            "Is Active",
            group.isActive
        );

        if (EditorGUI.EndChangeCheck())
        {
            RecordChange("Modify Sequence Group");
            group.name = newName;
            group.entryCollider = newEntryCollider;
            group.exitCollider = newExitCollider;
            group.isActive = newIsActive;
        }

        EditorGUILayout.Space(5);
        DrawControllers(group);

        DrawColliderWarning(group.entryCollider, "Entry Collider");
        DrawColliderWarning(group.exitCollider, "Exit Collider");
    }

    private void DrawControllers(SequenceManager.SequenceGroup group)
    {
        EditorGUILayout.LabelField(
            "Sequence Controllers",
            EditorStyles.boldLabel
        );

        if (group.sequenceControllers == null)
        {
            RecordChange("Create Sequence Controller List");
            group.sequenceControllers = new List<SequenceController>();
        }

        for (int i = 0; i < group.sequenceControllers.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();

            SequenceController newController =
                (SequenceController)EditorGUILayout.ObjectField(
                    $"Element {i}",
                    group.sequenceControllers[i],
                    typeof(SequenceController),
                    true
                );

            if (EditorGUI.EndChangeCheck())
            {
                RecordChange("Modify Sequence Controller");
                group.sequenceControllers[i] = newController;
            }

            if (GUILayout.Button("X", GUILayout.Width(30)))
            {
                RecordChange("Remove Sequence Controller");
                group.sequenceControllers.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add Sequence Controller"))
        {
            RecordChange("Add Sequence Controller");
            group.sequenceControllers.Add(null);
        }
    }

    private void AddGroup()
    {
        RecordChange("Add Sequence Group");

        manager.SequenceGroups.Add(
            new SequenceManager.SequenceGroup
            {
                name = $"Sequence Group {manager.SequenceGroups.Count + 1}"
            }
        );

        foldouts.Add(true);
    }

    private void RemoveGroup(int index)
    {
        RecordChange("Remove Sequence Group");
        manager.SequenceGroups.RemoveAt(index);
        foldouts.RemoveAt(index);
    }

    private void MoveGroup(int from, int to)
    {
        RecordChange("Move Sequence Group");

        SequenceManager.SequenceGroup group = manager.SequenceGroups[from];
        manager.SequenceGroups.RemoveAt(from);
        manager.SequenceGroups.Insert(to, group);

        bool foldout = foldouts[from];
        foldouts.RemoveAt(from);
        foldouts.Insert(to, foldout);
    }

    private void RecordChange(string actionName)
    {
        Undo.RecordObject(manager, actionName);
        EditorUtility.SetDirty(manager);

        if (PrefabUtility.IsPartOfPrefabInstance(manager))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        }
    }

    private void SynchronizeFoldouts()
    {
        while (foldouts.Count < manager.SequenceGroups.Count)
            foldouts.Add(true);

        while (foldouts.Count > manager.SequenceGroups.Count)
            foldouts.RemoveAt(foldouts.Count - 1);
    }

    private static void DrawColliderWarning(Collider collider, string label)
    {
        if (collider != null && !collider.isTrigger)
        {
            EditorGUILayout.HelpBox(
                $"{label} debe tener Is Trigger activado.",
                MessageType.Warning
            );
        }
    }
}