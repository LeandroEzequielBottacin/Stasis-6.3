using UnityEngine;

namespace UIScripts.UI_Scan
{
    [CreateAssetMenu(menuName = "Scan/Descriptor", fileName = "ScanDescriptor")]
    public class ScanDescriptor : ScriptableObject
    {
        [Header("Contenido")]
        public string displayName = "Interactable";
        [TextArea] public string hint = "Press [E] to interact";

        [Tooltip("Texto secundario opcional (p.ej. acción alternativa, estado, requisito).")]
        [TextArea] public string extraHint = "";       // <-- NUEVO

        public Sprite icon;
        public Color color = new Color(0f, 1f, 0.7f);

        [Header("Comportamiento")]
        [Tooltip("Máxima distancia a la que aparece el rótulo (0 = ilimitado)")]
        public float maxShowDistance = 18f;
    }
}