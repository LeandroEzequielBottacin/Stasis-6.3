using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Puzzle_Elements.PlataformasCorregidas
{
    [DefaultExecutionOrder(100)]
    public class TrainMovePlayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Rigidbody trainRb;

        [Header("Debug")]
        [SerializeField]
        private Vector3 trainVelocity;

        [SerializeField]private Model currentPlayer;

        private Vector3 lastTrainPosition;

        private void Start()
        {
            if (trainRb != null)
            {
                lastTrainPosition =
                    trainRb.position;
            }
        }
        private void FixedUpdate()
        {
            if (trainRb == null)
                return;

            CalculateTrainVelocity();

            if (currentPlayer != null)
            {
                currentPlayer.externalVelocity =
                    trainVelocity;
            }
        }

        private void CalculateTrainVelocity()
        {
            Vector3 currentPosition =
                trainRb.position;

            Vector3 delta =
                currentPosition -
                lastTrainPosition;

            trainVelocity =
                delta / Time.fixedDeltaTime;

            lastTrainPosition =
                currentPosition;
        }

        private void OnTriggerEnter(Collider other)
        {
            Model player =
                other.GetComponent<Model>();

            if (player == null)
                return;

            currentPlayer = player;

            currentPlayer.externalVelocity =
                trainVelocity;
        }

        private void OnTriggerExit(Collider other)
        {
            Model player =
                other.GetComponent<Model>();

            if (player == null)
                return;

            if (player != currentPlayer)
                return;

            currentPlayer.externalVelocity =
                Vector3.zero;

            currentPlayer = null;
        }
    }
}