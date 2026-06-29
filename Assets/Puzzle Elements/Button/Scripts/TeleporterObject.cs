using Puzzle_Elements.Hedron.VFX;
using UnityEngine;

namespace Puzzle_Elements.Button.Scripts
{
    public class TeleporterObject : MonoBehaviour
    {
        [SerializeField] private GameObject objectToTeleport;
        [SerializeField] private Transform pos;

        public void Teleport()
        {
            VFXHedro particlesHedro = objectToTeleport.GetComponent<VFXHedro>();
            if(particlesHedro != null)
            {
                //particlesHedro.DecreaseChildrenScale
            }
            objectToTeleport.transform.position = pos.position;
        }
    }
}
