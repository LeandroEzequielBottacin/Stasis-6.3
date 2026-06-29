using UnityEngine;

namespace Puzzle_Elements.Spider
{
    [DefaultExecutionOrder(80)]
    public class SpiderBodyClearanceController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Transform body;
        [SerializeField] private CapsuleCollider sensorA;  // c�psula izquierda/derecha bajo la panza
        [SerializeField] private CapsuleCollider sensorB;

        [Header("Detecci�n de superficie")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float castUp = 0.30f;
        [SerializeField] private float castDown = 1.50f;

        [Header("Clearance")]
        [SerializeField] private float targetClearance = 0.35f;
        [SerializeField] private float heightSmoothTime = 0.08f;
        [SerializeField] private float maxStepPerFrame = 0.60f;

        [Header("Evitar penetraci�n")]
        [SerializeField] private bool collisionClearance = true;
        [SerializeField] private float clearanceRadius = 0.25f;
        [SerializeField] private float minSeparation = 0.03f;

        [Header("Rotaci�n (para trepar)")]
        [SerializeField] private bool enableTiltAlignment = true;
        [Range(0f, 1f)] [SerializeField] private float tiltWeight = 0.6f;
        [SerializeField] private float tiltSlerp = 12f;

        [Header("Debug")]
        [SerializeField] private bool debugGizmos;

        float _heightVel;

        void Reset()
        {
            if (!body) body = transform;
        }

        void LateUpdate()
        {
            if (!body || !sensorA || !sensorB) return;

            Vector3 up = body.up;

            bool aHit = SampleSensor(sensorA, up, out float aDist, out Vector3 aNormal, out Vector3 aPoint, out Vector3 aOrigin);
            bool bHit = SampleSensor(sensorB, up, out float bDist, out Vector3 bNormal, out Vector3 bPoint, out Vector3 bOrigin);
            if (!aHit && !bHit) return;

            float avgDist = 0f; int count = 0;
            Vector3 avgNormal = Vector3.zero;
            if (aHit) { avgDist += aDist; avgNormal += aNormal; count++; }
            if (bHit) { avgDist += bDist; avgNormal += bNormal; count++; }
            avgDist /= Mathf.Max(1, count);
            avgNormal = (count > 0 ? (avgNormal / count).normalized : up);

            float error = targetClearance - avgDist;
            float maxStep = maxStepPerFrame * Time.deltaTime;
            error = Mathf.Clamp(error, -maxStep, maxStep);
            float smoothed = Mathf.SmoothDamp(0f, error, ref _heightVel, heightSmoothTime);
            Vector3 newPos = body.position + up * smoothed;

            if (collisionClearance)
                newPos = ApplyCollisionClearance(newPos, up);

            body.position = newPos;

            if (enableTiltAlignment)
            {
                Vector3 targetUp = Vector3.Slerp(up, avgNormal, Mathf.Clamp01(tiltWeight));
                Vector3 fwd = Vector3.ProjectOnPlane(body.forward, targetUp);
                if (fwd.sqrMagnitude < 1e-6f)
                {
                    fwd = Vector3.Cross(targetUp, Vector3.right);
                    if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(targetUp, Vector3.forward);
                }
                body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(fwd.normalized, targetUp), 1f - Mathf.Exp(-tiltSlerp * Time.deltaTime));
            }

            if (debugGizmos)
            {
                if (aHit) { Debug.DrawLine(aOrigin, aPoint, Color.cyan); Debug.DrawRay(aPoint, aNormal * 0.3f, Color.yellow); }
                if (bHit) { Debug.DrawLine(bOrigin, bPoint, Color.cyan); Debug.DrawRay(bPoint, bNormal * 0.3f, Color.yellow); }
            }
        }

        bool SampleSensor(CapsuleCollider col, Vector3 bodyUp, out float distance, out Vector3 normal, out Vector3 hitPoint, out Vector3 castOrigin)
        {
            Vector3 centerW = col.transform.TransformPoint(col.center);
            Vector3 axisW = GetCapsuleAxis(col);
            float halfLine = Mathf.Max(0f, col.height * 0.5f - col.radius);
            Vector3 bottom = centerW - axisW * halfLine;

            castOrigin = bottom + bodyUp * castUp;
            Vector3 dir = -bodyUp;
            float maxDist = castUp + castDown;

            if (Physics.Raycast(castOrigin, dir, out RaycastHit hit, maxDist, groundMask, QueryTriggerInteraction.Ignore))
            {
                distance = hit.distance - castUp;
                normal = hit.normal;
                hitPoint = hit.point;
                return true;
            }

            distance = 0f;
            normal = bodyUp;
            hitPoint = bottom;
            return false;
        }

        Vector3 GetCapsuleAxis(CapsuleCollider c)
        {
            Vector3 localAxis =
                c.direction == 0 ? Vector3.right :
                c.direction == 1 ? Vector3.up :
                Vector3.forward;
            return c.transform.TransformDirection(localAxis).normalized;
        }

        Vector3 ApplyCollisionClearance(Vector3 pos, Vector3 up)
        {
            Vector3 origin = pos - up * (clearanceRadius * 0.5f);
            float castDist = clearanceRadius + minSeparation;

            if (Physics.SphereCast(origin, clearanceRadius, -up, out var hit, castDist, groundMask, QueryTriggerInteraction.Ignore))
            {
                float pen = (clearanceRadius + minSeparation) - hit.distance;
                if (pen > 0f) pos += up * pen;
            }
            return pos;
        }
    }
}
