using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Puzzle_Elements.Fan.Scripts
{
    public class ColliderDetectPlayer : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
        private void OnTriggerStay(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if(player != null)
            {
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if(rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                }
            }
        }
    }
}
