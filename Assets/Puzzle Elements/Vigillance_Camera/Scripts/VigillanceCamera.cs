using UnityEngine;

namespace Puzzle_Elements.Vigillance_Camera.Scripts
{
    [ExecuteAlways]
    public class VigillanceCamera : MonoBehaviour
    {
        public Transform target;
        [HideInInspector] public Transform targetRootOverride;

        [Header("Ejes locales")]
        public Vector3 localLookAxis = Vector3.right;
        public Vector3 localUpAxis = Vector3.up;

        [Header("Clamps (�)")]
        public Vector2 yawMinMax = new Vector2(-90f, 90f);
        public Vector2 pitchMinMax = new Vector2(-20f, 20f);
        public bool invertPitch;

        [Header("Rotaci�n")]
        public float turnSpeed = 8f;
        public bool lockRollX = true;

        [Header("Oclusi�n")]
        public float occlusionRadius = 0.08f;
        public float minOccluderSize = 0.3f;

        [Tooltip("S�lo capas que pueden ocluir. Dejalo en s�lidos/escenario; exclu� part�culas, triggers finos, etc.")]
        public LayerMask occluderMask = ~0;

        Transform _parent;
        Quaternion _lastValidRotation;

        // Buffer est�tico para evitar allocs en runtime
        static readonly RaycastHit[] _hitsBuf = new RaycastHit[32];

        void Awake()
        {
            _parent = transform.parent;
            _lastValidRotation = transform.localRotation;
        }

        void LateUpdate()
        {
            if (!target) return;

            Vector3 toWorld = target.position - transform.position;
            float dist = toWorld.magnitude;
            if (dist < 1e-6f) return;

            if (IsOccluded(toWorld, dist))
            {
                ApplyRotation(_lastValidRotation);
                return;
            }

            Vector3 toLocal = _parent ? _parent.InverseTransformDirection(toWorld) : toWorld;
            Quaternion desiredLocal = FromToRotationLocal(localLookAxis, toLocal, localUpAxis);
            Vector3 eDesired = NormalizeEuler(desiredLocal.eulerAngles);

            // Yaw
            float yaw = eDesired.y;
            if (yaw < yawMinMax.x || yaw > yawMinMax.y)
            {
                ApplyRotation(_lastValidRotation);
                return;
            }
            eDesired.y = Mathf.Clamp(eDesired.y, yawMinMax.x, yawMinMax.y);

            // Pitch (en Z seg�n convenci�n de este rig)
            float p = eDesired.z;
            if (invertPitch) p = -p;
            p = Mathf.Clamp(p, pitchMinMax.x, pitchMinMax.y);
            if (invertPitch) p = -p;
            eDesired.z = p;

            if (lockRollX) eDesired.x = 0f;

            Quaternion clamped = Quaternion.Euler(eDesired);
            _lastValidRotation = clamped;
            ApplyRotation(clamped);
        }

        bool IsOccluded(Vector3 toWorld, float dist)
        {
            Vector3 dir = toWorld / dist;

            // NonAlloc: sin GC ni LINQ
            int n = Physics.SphereCastNonAlloc(
                transform.position,
                occlusionRadius,
                dir,
                _hitsBuf,
                dist,
                occluderMask,
                QueryTriggerInteraction.Ignore
            );

            if (n <= 0) return false;

            Transform targetRoot = targetRootOverride ? targetRootOverride : target;

            float bestDist = float.MaxValue;
            Collider bestCol = null;

            for (int i = 0; i < n; i++)
            {
                var col = _hitsBuf[i].collider;
                if (!col) continue;

                Transform t = col.transform;

                // Si golpea el target (o un hijo), no hay oclusi�n
                if (t == target || (targetRoot && t.IsChildOf(targetRoot)))
                    return false;

                float d = _hitsBuf[i].distance;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestCol = col;
                }
            }

            if (!bestCol) return false;

            // Tama�o del m�s cercano: �nico chequeo
            Vector3 s = bestCol.bounds.size;
            float maxSize = (s.x > s.y ? (s.x > s.z ? s.x : s.z) : (s.y > s.z ? s.y : s.z));
            return maxSize >= minOccluderSize;
        }

        void ApplyRotation(Quaternion targetRot)
        {
            if (turnSpeed <= 0f) transform.localRotation = targetRot;
            else
            {
                float a = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, a);
            }
        }

        static Quaternion FromToRotationLocal(Vector3 fromLocalAxis, Vector3 toLocalDir, Vector3 upHint)
        {
            if (toLocalDir.sqrMagnitude < 1e-6f) return Quaternion.identity;
            Quaternion lookZ = Quaternion.LookRotation(toLocalDir.normalized, upHint.sqrMagnitude > 1e-6f ? upHint.normalized : Vector3.up);
            Quaternion corr = Quaternion.FromToRotation(fromLocalAxis.normalized, Vector3.forward);
            return lookZ * corr;
        }

        static Vector3 NormalizeEuler(Vector3 e)
        {
            e.x = Wrap180(e.x);
            e.y = Wrap180(e.y);
            e.z = Wrap180(e.z);
            return e;
        }

        static float Wrap180(float a) => Mathf.Repeat(a + 180f, 360f) - 180f;
    }
}
