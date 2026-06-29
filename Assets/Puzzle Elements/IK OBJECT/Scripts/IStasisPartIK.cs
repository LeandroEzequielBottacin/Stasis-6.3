using UnityEngine;

namespace Puzzle_Elements.IK_OBJECT.Scripts
{
    // Tus scripts de bone pueden implementar esta interfaz
    // para recibir la referencia del "Stasis Tip Controller".
    public interface IStasisPartIK
    {
        void SetTipController(Component tipController);
    }
}