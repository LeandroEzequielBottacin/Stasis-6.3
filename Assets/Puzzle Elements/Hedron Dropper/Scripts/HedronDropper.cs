using Puzzle_Elements.AllInterfaces;
using UnityEngine;

namespace Puzzle_Elements.Hedron_Dropper.Scripts
{
    public class HedronDropper : MonoBehaviour, IButtonActivator
    {
        public void OnPressed()
        {
            //destroys last hedron, drops hedron new, generates another one
        }
    }
}
