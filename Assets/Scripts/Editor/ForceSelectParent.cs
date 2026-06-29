using Tools;
using UnityEditor;
using UnityEngine;

// <- importa el namespace de tu tag

namespace Editor
{
    [InitializeOnLoad]
    public static class ForceSelectParentEditor
    {
        // Poner en true para ver logs en la consola durante la prueba
        const bool debug = true;

        static ForceSelectParentEditor()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private static void OnSelectionChanged()
        {
            if (Selection.activeGameObject == null)
            {
                if (debug) Debug.Log("[ForceSelect] nothing selected");
                return;
            }

            GameObject selected = Selection.activeGameObject;
            if (debug) Debug.Log($"[ForceSelect] selected: {selected.name}");

            Transform current = selected.transform;
            GameObject foundTaggedParent = null;

            // Subimos por la jerarqu�a hasta encontrar el objeto con ForceSelectParentTag
            while (current != null)
            {
                if (current.GetComponent<ForceSelectParentTag>() != null)
                {
                    foundTaggedParent = current.gameObject;
                    break;
                }
                current = current.parent;
            }

            if (foundTaggedParent != null)
            {
                if (Selection.activeGameObject != foundTaggedParent)
                {
                    if (debug) Debug.Log($"[ForceSelect] switching selection to: {foundTaggedParent.name}");
                    // Usamos delayCall para evitar problemas si Unity est� en medio de un cambio de selecci�n
                    EditorApplication.delayCall += () => Selection.activeGameObject = foundTaggedParent;
                }
            }
            else
            {
                if (debug) Debug.Log("[ForceSelect] no tagged parent found in hierarchy");
            }
        }
    }
}
