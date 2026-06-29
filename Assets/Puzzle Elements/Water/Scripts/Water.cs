using Checkpoint;
using Managers.Game;
using Player.Scripts.Interactor;
using Player.Scripts.MovementFSM.MVC;
using Puzzle_Elements.Hedron.Scripts;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Water.Scripts
{
    public class Water : MonoBehaviour
    {

        public UnityEvent OnFallInWater;
        Model player;

        [SerializeField] private CheckpointSequence _checkpoint;

        
        private void OnTriggerEnter(Collider other)
        {
            player = other.GetComponent<Model>();
            if (player != null)
            {
                PlayerInteractor playerInteractor = player.GetComponentInChildren<PlayerInteractor>();
                if (playerInteractor._objectGrabbable != null)
                {
                    PhysicsBox box = playerInteractor._objectGrabbable.GetComponent<PhysicsBox>();
                    if (box != null)
                    {
                        playerInteractor.TryDropObject();
                        box.transform.position = box.posInitial;
                        Rigidbody rbBox = box.GetComponent<Rigidbody>();
                        if (rbBox != null)
                        {
                            rbBox.linearVelocity = Vector3.zero;
                            rbBox.useGravity = false;
                            rbBox.isKinematic = true;
                            rbBox.isKinematic = false;
                        }
                    }
                }
                GameManager.Instance.PlayerDeath();
                // player.transform.position = _checkpoint.CurrentCheckpointPos();
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                }


            }

            PhysicsBox hedro = other.GetComponent<PhysicsBox>();
            if (hedro != null)
            {
                hedro.transform.position = hedro.posInitial;
                Rigidbody rb = hedro.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.useGravity = false;
                    rb.isKinematic = true;
                    rb.isKinematic = false;
                }
            }
        }
        //private void OnCollisionEnter(Collision collision)
        //{
        //    player = collision.gameObject.GetComponent<Model>();
        //    if (player != null)
        //    {
        //        OnFallInWater?.Invoke();
        //        //player.transform.position = _checkpoint.CurrentCheckpointPos();
        //        Rigidbody rb = player.GetComponent<Rigidbody>();
        //        if (rb != null)
        //        {
        //            rb.velocity = Vector3.zero;
        //        }
        //    }

        //    PhysicsBox hedro = collision.gameObject.GetComponent<PhysicsBox>();
        //    if (hedro != null)
        //    {
        //        hedro.transform.position = hedro.posInitial;

        //        Rigidbody rb = hedro.GetComponent<Rigidbody>();
        //        if (rb != null)
        //        {
        //            rb.velocity = Vector3.zero;
        //        }
        //    }
        //}
        public void PlayerDeath()
        {
            //player.transform.position = _checkpoint.CurrentCheckpointPos();
        }

    }
}
