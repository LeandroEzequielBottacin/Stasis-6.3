using UnityEngine;

namespace Puzzle_Elements.IK.Scripts.SetPlayerParent
{
    [DefaultExecutionOrder(-200)]
    public class MovingPlatformDeltaPosition : MonoBehaviour
    {
        public Vector3 DeltaPosition { get; private set; }
        public Quaternion DeltaRotation { get; private set; }

        private Vector3 _lastPos;
        private Quaternion _lastRot;

        private bool firstFrame = true;



        public Rigidbody otherRb;
        private Vector3 _otherLastPosition;
        [SerializeField] private bool _otherIsMoving = true;

        private void Awake()
        {
            _lastPos = transform.position;
            _lastRot = transform.rotation;



            if (otherRb != null)
                _otherLastPosition = otherRb.position;
        }

        private void FixedUpdate()
        {
            if (_otherIsMoving)
            {
                Vector3 currentPos = transform.position;
                Quaternion currentRot = transform.rotation;

                DeltaPosition = currentPos - _lastPos;
                DeltaRotation = currentRot * Quaternion.Inverse(_lastRot);

                _lastPos = currentPos;
                _lastRot = currentRot;
            }
       





            // --- Delta del OTRO rigidbody ---
            if (otherRb != null)
            {
                Vector3 deltaOther = otherRb.position - _otherLastPosition;
                _otherIsMoving = deltaOther.sqrMagnitude > 0.01f;

                _otherLastPosition = otherRb.position;
            }
        }
    }
}
