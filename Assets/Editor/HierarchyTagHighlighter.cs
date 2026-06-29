// Assets/Editor/HierarchyTagHighlighter.cs

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [InitializeOnLoad]
    public static class HierarchyTagHighlighter
    {
        // Colores por Tag (pod�s editar/a�adir los que quieras)
        // bg = color de fondo (usa alpha bajo), text = color del texto
        static readonly Dictionary<string, (Color bg, Color text)> tagColors = new()
        {
            //           R    G    B    A                  R    G    B    A
            { "Player", (new Color(1f, 0f, 0f, 0.4f), new Color(0.85f, 0.95f, 1.00f, 1f)) },
            { "Conteiner", (new Color(0f, 1f, 0f, 0.4f), new Color(1.00f, 0.85f, 0.85f, 1f)) },
            { "Manager", (new Color(1f, 1f, 0f, 0.3f),       new Color(1f, 1f, 0.6f, 1f)) },
            { "Level", (new Color(0f, 0f, 1f, 0.4f), new Color(1.00f, 0.85f, 0.85f, 1f)) },
            //{ "UI", (new Color(0.20f, 1.00f, 0.70f, 0.12f), new Color(0.85f, 1.00f, 0.95f, 1f)) },
            //{ "Props", (new Color(1.00f, 0.85f, 0.20f, 0.10f), new Color(0.20f, 0.20f, 0.20f, 1f)) },
            // { "TuTag", (new Color(...),                 new Color(...)) },
        };

        // Opciones
        static bool overrideTextColor = false;   // si quer�s SOLO fondo, ponelo en false
        static bool boldText = true;   // texto en negrita
        static float leftIndent = 32f;    // deja espacio para el foldout/icono

        static HierarchyTagHighlighter()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            Object obj = EditorUtility.InstanceIDToObject(instanceID);
            if (obj is not GameObject go) return;

            // �Hay configuraci�n de color para este tag?
            if (!tagColors.TryGetValue(go.tag, out var colors)) return;

            // Si el item est� seleccionado, atenuamos o variamos para que no pelee con el highlight de Unity
            bool isSelected = Selection.instanceIDs != null && System.Array.IndexOf(Selection.instanceIDs, instanceID) >= 0;
            Color bg = colors.bg;
            Color tx = colors.text;

            if (isSelected)
            {
                // Suaviza el fondo al estar seleccionado (para no �tapar� el azul/gris de selecci�n)
                bg.a *= 0.5f;
            }

            // Dibuja el fondo (rect�ngulo a lo ancho de la fila)
            EditorGUI.DrawRect(selectionRect, bg);

            // Opcional: pintar texto con color propio encima del label original
            if (overrideTextColor)
            {
                // Dejamos margen para foldout/iconos del prefab, etc.
                var labelRect = new Rect(selectionRect.x + leftIndent, selectionRect.y, selectionRect.width - leftIndent, selectionRect.height);

                // Estilo de texto
                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = tx;
                if (boldText) style.fontStyle = FontStyle.Bold;

                // Sombra sutil para legibilidad en skins claros/oscuros
                var shadow = new GUIStyle(style);
                shadow.normal.textColor = EditorGUIUtility.isProSkin
                    ? new Color(0f, 0f, 0f, 0.6f)
                    : new Color(0f, 0f, 0f, 0.3f);

                // Dibujamos una sombrita y luego el texto encima (solo el nombre, no reicona/etiquetas)
                var shadowRect = labelRect; shadowRect.x += 1f; shadowRect.y += 1f;
                EditorGUI.LabelField(shadowRect, go.name, shadow);
                EditorGUI.LabelField(labelRect, go.name, style);
            }

            // Tip: si quer�s tambi�n colorear hijos cuando el padre tiene un tag,
            // pod�s recorrer go.transform.parent y aplicar un color distinto por jerarqu�a.
        }
    }
}
