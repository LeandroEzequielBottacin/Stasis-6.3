// Assets/Scripts/IKSuite/BoneEnd.cs

using UnityEngine;

namespace Puzzle_Elements.IK_OBJECT.Scripts
{
    // Ponlo en el prefab de cada bone y arrastra el hijo "end".
    public class BoneEnd : MonoBehaviour
    {
        public Transform end; // obligatorio

        public Transform GetEnd()
        {
            if (!end) Debug.LogError($"[BoneEnd] Falta asignar 'end' en {name}", this);
            return end;
        }
    }
}
