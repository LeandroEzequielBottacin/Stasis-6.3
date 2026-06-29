using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Player.Scripts.Opening_Cinematic
{
    public class IKGroundAvoider : MonoBehaviour
    {
        [Header("Referencias de IK (Animation Rigging)")]
        public TwoBoneIKConstraint leftArmIK;
        public TwoBoneIKConstraint rightArmIK;

        [Header("Configuración del suelo")]
        public LayerMask groundLayer;
        public float rayDistance = 0.5f;
        public float offsetAboveGround = 0.02f;

        [Header("Debug")]
        public bool showDebug = true;

        void LateUpdate()
        {
            if (leftArmIK) CheckAndAdjustIK(leftArmIK);
            if (rightArmIK) CheckAndAdjustIK(rightArmIK);
        }

        void CheckAndAdjustIK(TwoBoneIKConstraint armIK)
        {
            if (!armIK.data.target)
                return;

            Transform hand = armIK.data.target;
            Vector3 origin = hand.position + Vector3.up * 0.05f;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, groundLayer))
            {
                float groundY = hit.point.y + offsetAboveGround;

                if (hand.position.y < groundY)
                {
                    Vector3 correctedPos = new Vector3(hand.position.x, groundY, hand.position.z);
                    hand.position = correctedPos;
                }

                if (showDebug)
                {
                    Debug.DrawLine(origin, hit.point, Color.green);
                    Debug.DrawRay(hit.point, Vector3.up * 0.05f, Color.cyan);
                }
            }
            else if (showDebug)
            {
                Debug.DrawLine(origin, origin + Vector3.down * rayDistance, Color.red);
            }
        }
    }
}

