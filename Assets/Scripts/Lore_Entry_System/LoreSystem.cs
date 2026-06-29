using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lore_Entry_System
{
    [DisallowMultipleComponent]
    public class LoreSystem : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Base de datos con TODOS los entries posibles.")]
        public LoreDatabase database;

        [Header("Guardado")]
        [Tooltip("Clave base para PlayerPrefs.")]
        [SerializeField] private string prefsKey = "Lore.Unlocked";

        // Eventos (para UI/VFX) — NO usar [Header] en eventos
        public event Action<LoreEntry> OnEntryUnlocked = delegate { };

        // Runtime
        private readonly HashSet<string> _unlockedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, LoreEntry> _byId = new Dictionary<string, LoreEntry>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> UnlockedIds => _unlockedIds;

        void Awake()
        {
            BuildIndex();
            Load();
        }

        private void BuildIndex()
        {
            _byId.Clear();
            if (!database) return;
            foreach (var e in database.entries)
            {
                if (!e || string.IsNullOrWhiteSpace(e.id)) continue;
                if (!_byId.ContainsKey(e.id)) _byId.Add(e.id, e);
            }
        }

        [ContextMenu("Debug/Print Unlocked")]
        private void DebugPrint()
        {
            foreach (var id in _unlockedIds) Debug.Log($"Unlocked: {id}", this);
        }

        public bool IsUnlocked(string id) => _unlockedIds.Contains(id);

        public bool TryGetEntry(string id, out LoreEntry entry) => _byId.TryGetValue(id, out entry);

        [ContextMenu("Debug/Clear Save")]
        public void ClearSave()
        {
            _unlockedIds.Clear();
            PlayerPrefs.DeleteKey(prefsKey);
            PlayerPrefs.Save();
            Debug.Log("Lore save cleared.", this);
        }

        public bool Unlock(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (_unlockedIds.Contains(id)) return false;

            if (!_byId.TryGetValue(id, out var entry))
            {
                Debug.LogWarning($"LoreSystem: ID '{id}' no existe en Database.", this);
                return false;
            }

            _unlockedIds.Add(id);
            Save();
            OnEntryUnlocked(entry);
            return true;
        }

        public void Save()
        {
            var json = JsonUtility.ToJson(new SaveBlob { ids = new List<string>(_unlockedIds) });
            PlayerPrefs.SetString(prefsKey, json);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            _unlockedIds.Clear();
            if (!PlayerPrefs.HasKey(prefsKey)) return;
            var json = PlayerPrefs.GetString(prefsKey);
            var blob = JsonUtility.FromJson<SaveBlob>(json);
            if (blob?.ids != null)
                foreach (var id in blob.ids) _unlockedIds.Add(id);
        }

        [Serializable]
        private class SaveBlob { public List<string> ids; }
    }
}
