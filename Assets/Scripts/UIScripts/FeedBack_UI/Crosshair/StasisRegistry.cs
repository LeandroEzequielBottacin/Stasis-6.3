using System.Collections.Generic;
using Player.Stasis;
using UnityEngine;

namespace UIScripts.FeedBack_UI.Crosshair
{
    /// <summary>
    /// Registro global que mapea Collider → IStasis (lookup O(1)).
    /// </summary>
    public static class StasisRegistry
    {
        private static readonly Dictionary<Collider, IStasis> _byCollider = new Dictionary<Collider, IStasis>(256);

        public static void Register(Collider col, IStasis owner)
        {
            if (col == null || owner == null) return;
            _byCollider[col] = owner;
        }

        public static void Unregister(Collider col, IStasis owner)
        {
            if (col == null) return;
            if (_byCollider.TryGetValue(col, out var o) && ReferenceEquals(o, owner))
                _byCollider.Remove(col);
        }

        public static bool TryGet(Collider col, out IStasis s)
        {
            return _byCollider.TryGetValue(col, out s);
        }
    }
}