using UnityEngine;

namespace Player.Scripts
{
    public class PlayerRagdoll : MonoBehaviour
    {
        [Header("<color=yellow>Main Components</color>")]
        public Animator animator;
        public Rigidbody mainRigidbody;
        public Collider mainCollider;

        [Header("<color=green>Ragdoll</color>")]
        public Rigidbody[] ragdollRigidbodies;
        public Collider[] ragdollColliders;

        [Header("<color=red>Activation Switches</color>")]
        public bool activateRagdoll;
        public bool deactivateRagdoll;

        private bool isRagdollActive;

        void Awake()
        {
            ForceDisableRagdollAtStart();
        }

        void Start()
        {

        }

        void Update()
        {
            if (activateRagdoll)
            {
                activateRagdoll = false;
                SetRagdollActive(true);
            }

            if (deactivateRagdoll)
            {
                deactivateRagdoll = false;
                SetRagdollActive(false);
            }
        }

        private void ForceDisableRagdollAtStart()
        {
            isRagdollActive = false;
            animator.enabled = true;

            foreach (var rb in ragdollRigidbodies)
            {
                rb.isKinematic = true;
            }

            foreach (var col in ragdollColliders)
            {
                col.enabled = false;
            }

            mainRigidbody.isKinematic = false;
            mainCollider.enabled = true;
        }

        public void SetRagdollActive(bool isActive)
        {
            if (isRagdollActive == isActive) return;

            isRagdollActive = isActive;
            animator.enabled = !isActive;

            foreach (var rb in ragdollRigidbodies)
            {
                rb.isKinematic = !isActive;

                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            foreach (var col in ragdollColliders)
            {
                col.enabled = isActive;
            }

            mainRigidbody.isKinematic = isActive;
            mainCollider.enabled = !isActive;
        }
    }
}



