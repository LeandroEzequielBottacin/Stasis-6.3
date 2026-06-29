using UnityEngine;

namespace Puzzle_Elements.Fan.Scripts
{
    public class FanBox : MonoBehaviour
    {
        [Header("Encendido")]
        public bool startOn = true;

        [Header("Volumen (frontal)")]
        [Tooltip("Offset local del volumen de viento (origen en el objeto).")]
        public Vector3 offsetVolumen = Vector3.zero;

        [Tooltip("Longitud del volumen a lo largo del eje forward (Z).")]
        public float length = 8f;

        [Tooltip("Medio ancho (X) y medio alto (Y) del volumen.")]
        public Vector2 halfSizeXY = new Vector2(1.0f, 1.0f);

        [Header("Fuerza")]
        [Tooltip("Aceleraci�n m�xima aplicada en la direcci�n forward.")]
        public float maxAcceleration = 25f;

        [Tooltip("Fracci�n de fuerza que se aplica hacia arriba (levantar objetos).")]
        [Range(0f, 0.5f)] public float liftFraction = 0.1f;

        [Tooltip("Ca�da longitudinal a lo largo de la longitud (0 al inicio, 1 al final).")]
        public AnimationCurve longitudinalFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Tooltip("Ca�da lateral seg�n cercan�a a los bordes del box (0 centro, 1 borde).")]
        public AnimationCurve lateralFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Capas afectadas")]
        public LayerMask affectLayers = ~0;

        [Header("Aproximaci�n de rozamiento est�tico")]
        [Tooltip("� est�tico aproximado para vencer la inercia en objetos en reposo.")]
        public float approxMuStatic = 0.5f;

        [Header("L�nea de visi�n (opcional)")]
        public bool requireLineOfSight;
        public LayerMask occluderLayers = ~0;
        public float losOriginYOffset = 0.1f;
        public float losProbeRadius = 0.2f;

        [Header("Gizmos")]
        public bool drawGizmos = true;
        public Color gizmoColor = new Color(0f, 0.8f, 1f, 0.25f);
        public Color gizmoWireColor = new Color(0f, 0.8f, 1f, 0.9f);

        private bool _isRunning;

        private void Start()
        {
            SetRunning(startOn);
        }

        private void FixedUpdate()
        {
            if (!_isRunning || length <= 0f || halfSizeXY.x <= 0f || halfSizeXY.y <= 0f)
                return;

            ApplyBoxForces();
        }

        private void ApplyBoxForces()
        {
            Vector3 localCenter = offsetVolumen + Vector3.forward * (length * 0.5f);
            Vector3 worldCenter = transform.TransformPoint(localCenter);
            Vector3 halfExtents = new Vector3(halfSizeXY.x, halfSizeXY.y, length * 0.5f);

            var hits = Physics.OverlapBox(
                worldCenter,
                halfExtents,
                transform.rotation,
                affectLayers,
                QueryTriggerInteraction.Ignore
            );

            foreach (var col in hits)
            {
                var rb = col.attachedRigidbody;
                if (!rb || rb.isKinematic) continue;

                if (requireLineOfSight && !HasLineOfSight(worldCenter, rb.worldCenterOfMass))
                    continue;

                Vector3 localPoint = transform.InverseTransformPoint(rb.worldCenterOfMass) - offsetVolumen;

                if (localPoint.z < 0f || localPoint.z > length) continue;
                if (Mathf.Abs(localPoint.x) > halfSizeXY.x + 1e-4f) continue;
                if (Mathf.Abs(localPoint.y) > halfSizeXY.y + 1e-4f) continue;

                float tLong = (length > 0f) ? Mathf.Clamp01(localPoint.z / length) : 1f;
                float tx = (halfSizeXY.x > 0f) ? Mathf.Clamp01(Mathf.Abs(localPoint.x) / halfSizeXY.x) : 0f;
                float ty = (halfSizeXY.y > 0f) ? Mathf.Clamp01(Mathf.Abs(localPoint.y) / halfSizeXY.y) : 0f;

                float tLat = Mathf.Max(tx, ty);

                float intensity = Mathf.Clamp01(longitudinalFalloff.Evaluate(tLong)) *
                                  Mathf.Clamp01(lateralFalloff.Evaluate(tLat));
                if (intensity <= 0f) continue;

                // --- Dise�ado en aceleraci�n -> convertir a fuerza ---
                Vector3 accel = transform.forward * (maxAcceleration * intensity);

                // Vencer rozamiento est�tico m�nimo (en aceleraci�n)
                float g = Physics.gravity.magnitude;
                float minAccel = approxMuStatic * g;
                float mag = accel.magnitude;
                if (mag < minAccel && mag > 1e-4f)
                    accel = accel.normalized * Mathf.Min(minAccel, maxAcceleration);

                // Lift opcional (en aceleraci�n)
                Vector3 liftAccel = (liftFraction > 0f) ? Vector3.up * (accel.magnitude * liftFraction) : Vector3.zero;

                // Convertimos a fuerza (N): F = m * a
                Vector3 totalForce = (accel + liftAccel) * rb.mass;

                // Aplicamos SIEMPRE como ForceMode.Force (fuerza continua por FixedUpdate)
                rb.AddForce(totalForce, ForceMode.Force);
            }
        }

        private bool HasLineOfSight(Vector3 volumeWorldCenter, Vector3 targetPoint)
        {
            Vector3 origin = volumeWorldCenter + Vector3.up * losOriginYOffset;
            Vector3 dir = targetPoint - origin;
            float dist = dir.magnitude;
            if (dist <= 1e-3f) return true;
            dir /= dist;

            return !Physics.SphereCast(origin, losProbeRadius, dir, out _, dist, occluderLayers, QueryTriggerInteraction.Ignore);
        }

        // Control
        public void StartFan() => SetRunning(true);
        public void StopFan() => SetRunning(false);
        public void ToggleFan() => SetRunning(!_isRunning);

        private void SetRunning(bool running) => _isRunning = running;

        // Gizmos
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Vector3 localCenter = offsetVolumen + Vector3.forward * (length * 0.5f);
            Vector3 worldCenter = transform.TransformPoint(localCenter);
            Vector3 halfExtents = new Vector3(halfSizeXY.x, halfSizeXY.y, length * 0.5f);

            Matrix4x4 m = Matrix4x4.TRS(worldCenter, transform.rotation, Vector3.one);
            Gizmos.matrix = m;

            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);

            Gizmos.color = gizmoWireColor;
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);

            Gizmos.DrawRay(Vector3.zero, Vector3.forward * (halfExtents.z * 1.2f));
        }
    }
}
