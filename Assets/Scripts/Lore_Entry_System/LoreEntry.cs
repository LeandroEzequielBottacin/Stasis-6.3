using UnityEngine;

namespace Lore_Entry_System
{
    [CreateAssetMenu(menuName = "Lore/Entry", fileName = "LoreEntry_")]
    public class LoreEntry : ScriptableObject
    {
        [Header("Identidad")]
        [Tooltip("ID única y estable. Usá algo legible (p.ej. 'Logs/Prologo_01').")]
        public string id = "Lore/Example_01";

        [Header("Contenido")]
        [Tooltip("Título que se muestra en el lector.")]
        public string title = "New Entry";
        [Tooltip("Imagen principal (placeholder válido).")]
        public Sprite image;
        [Tooltip("Texto/lore. Acepta saltos de línea.")]
        [TextArea(5, 12)] public string body = "Lorem ipsum dolor sit amet...";

        [Header("Opcional")]
        [Tooltip("Audio para reproducir al abrir (voz, sfx).")]
        public AudioClip openSfx;
    }
}