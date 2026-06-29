using System.Collections.Generic;
using UnityEngine;

namespace Lore_Entry_System
{
    [CreateAssetMenu(menuName = "Lore/Database", fileName = "LoreDatabase")]
    public class LoreDatabase : ScriptableObject
    {
        [Header("Catálogo")]
        [Tooltip("Todas las entradas de lore disponibles en el juego.")]
        public List<LoreEntry> entries = new List<LoreEntry>();
    }
}