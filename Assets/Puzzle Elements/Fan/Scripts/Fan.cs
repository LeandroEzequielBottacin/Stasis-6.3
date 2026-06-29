using System;
using System.Collections.Generic;
using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.Fan.Scripts
{
    [RequireComponent(typeof(Rigidbody))]
    public class Fan : MonoBehaviour, IStasis
    {
        [Header("Rotación")]
        [Tooltip("Eje en el que girará el ventilador.")]
        public Vector3 ejeRotacion = new Vector3(0, 1, 0);

        [Tooltip("Velocidad de rotación del ventilador en grados por segundo.")]
        public float velocidadRotacion = 45f;

        [Tooltip("Si está activado, el ventilador comenzará encendido al iniciar.")]
        public bool startOn = true;

        private Rigidbody _rb;
        private bool _isRunning;
        private bool _isStasis;

        [Header("Offset Volumen")]
        [Tooltip("Offset del volumen de aire y gizmos en los 3 ejes.")]
        public Vector3 offsetVolumen = Vector3.zero;

        [Header("Volumen del aire (frontal)")]
        public float length = 10f;
        public float startRadius = 1.0f;
        public float endRadius = 1.0f;

        [Header("Fuerza (frontal)")]
        public float maxAcceleration = 30f;
        public AnimationCurve longitudinalFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public AnimationCurve radialFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Succión trasera (opcional)")]
        public bool enableBackSuction = true;
        public float backLength = 6f;
        public float backStartRadius = 1.0f;
        public float backEndRadius = 1.0f;

        [Header("Fuerza (trasera)")]
        public float backMaxAcceleration = 20f;
        public AnimationCurve backLongitudinalFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public AnimationCurve backRadialFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Capas afectadas")]
        public LayerMask affectLayers = ~0;

        [Header("Ajustes extra")]
        [Range(0f, 0.5f)] public float liftFraction = 0.15f;
        public float approxMuStatic = 0.5f;

        [Header("CharacterController")]
        public bool pushCharacterControllers = true;
        public float ccMaxExternalSpeed = 10f;
        [Range(0f, 1f)] public float ccDamping = 0.15f;

        [Header("Línea de visión")]
        public bool requireLineOfSight;
        public LayerMask occluderLayers = ~0;
        public float losOriginYOffset = 0.1f;
        public float losProbeRadius = 0.2f;

        private readonly Dictionary<CharacterController, Vector3> _ccExternalVel = new();

        [Header("Stasis VFX (Shader)")]
        public Renderer[] targetRenderers;
        public ParticleSystem windParticles;

        [Header("Gizmos")]
        public bool drawGizmos = true;
        public int gizmoRings = 6;
        public int gizmoRingSegments = 32;
        public Color gizmoColorFront = new Color(0f, 0.8f, 1f, 0.7f);
        public Color gizmoColorBack = new Color(1f, 0.6f, 0f, 0.7f);


        public Action OnPlayFan;
        public Action OnOffFan;
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = GetComponentsInChildren<Renderer>(true);
            
            StasisEffect = new StasisEffect(null, targetRenderers);
        }

        private void Start()
        {
            SetRunning(startOn);
        }

        private void FixedUpdate()
        {
          
            if (_isRunning && !_isStasis)
            {
                Quaternion deltaRotation = Quaternion.Euler(ejeRotacion * (velocidadRotacion * Time.fixedDeltaTime));
                foreach (Transform child in transform)
                {
                    child.rotation *= deltaRotation;
                }
            }

            if (_isRunning && !_isStasis)
            {
                ForwardForce();
                if (enableBackSuction) BackwardForce();
            }
        }

        public void StartFan() => SetRunning(true);
        public void StopFan() => SetRunning(false);
        public void ToggleFan() => SetRunning(!_isRunning);

        private void SetRunning(bool running)
        {
            // Si el estado no cambia, no dispares eventos otra vez
            if (_isRunning == running)
                return;

            _isRunning = running;

            if (_isRunning)
                OnPlayFan?.Invoke();
            else
                OnOffFan?.Invoke();

            if (!_isRunning)
                _rb.angularVelocity = Vector3.zero;

            if (windParticles)
            {
                if (_isRunning) windParticles.Play();
                else windParticles.Stop();
            }
        }

        public bool IsFreezed => _isStasis;
        public StasisEffect StasisEffect { get; private set; }
        
        public void StatisEffectActivate()
        {
            _isStasis = true;
            StopFan();
            StasisEffect.StasisEffectStart();
        }

        public void StatisEffectDeactivate()
        {
            _isStasis = false;
            StartFan();
            StasisEffect.StasisEffectStop();
        }
        
        private void ForwardForce()
        {
            if (length <= 0f) return;

            Vector3 origin = transform.position + offsetVolumen;
            Vector3 axis = transform.forward;

            float capRadius = Mathf.Max(startRadius, endRadius);
            Vector3 a = origin;
            Vector3 b = origin + axis * length;

            var hits = Physics.OverlapCapsule(a, b, capRadius, affectLayers, QueryTriggerInteraction.Ignore);

            foreach (var col in hits)
            {
                if (requireLineOfSight && !HasLineOfSight(origin, col)) continue;

                if (pushCharacterControllers)
                {
                    var cc = col.GetComponentInParent<CharacterController>();
                    if (cc)
                    {
                        if (ComputeAccelAtPoint(cc.bounds.center, origin, axis, out var accelCc))
                        {
                            float dt = Time.fixedDeltaTime;
                            Vector3 lift = Vector3.up * (accelCc.magnitude * liftFraction);
                            if (!_ccExternalVel.TryGetValue(cc, out var vel)) vel = Vector3.zero;

                            vel += (accelCc + lift) * dt;
                            vel = Vector3.ClampMagnitude(vel, ccMaxExternalSpeed);

                            cc.Move(vel * dt);

                            vel = Vector3.Lerp(vel, Vector3.zero, ccDamping);
                            _ccExternalVel[cc] = vel;
                        }
                        continue;
                    }
                }

                var rb = col.attachedRigidbody;
                if (!rb || rb.isKinematic) continue;
                if (!ComputeAccelAtPoint(rb.worldCenterOfMass, origin, axis, out var accelRb)) continue;

                float g = Physics.gravity.magnitude;
                float minAccel = approxMuStatic * g;
                float mag = accelRb.magnitude;
                if (mag < minAccel && mag > 1e-4f)
                    accelRb = accelRb.normalized * Mathf.Min(minAccel, maxAcceleration);

                if (liftFraction > 0f)
                    rb.AddForce(Vector3.up * (accelRb.magnitude * liftFraction), ForceMode.Acceleration);

                rb.AddForce(accelRb, ForceMode.Acceleration);
            }
        }

        private void BackwardForce()
        {
            if (backLength <= 0f) return;

            Vector3 origin = transform.position + offsetVolumen;
            Vector3 backAxis = -transform.forward;
            Vector3 pullDir = transform.forward;

            float capRadius = Mathf.Max(backStartRadius, backEndRadius);
            Vector3 a = origin;
            Vector3 b = origin + backAxis * backLength;

            var hits = Physics.OverlapCapsule(a, b, capRadius, affectLayers, QueryTriggerInteraction.Ignore);

            foreach (var col in hits)
            {
                if (requireLineOfSight && !HasLineOfSight(origin, col)) continue;

                if (pushCharacterControllers)
                {
                    var cc = col.GetComponentInParent<CharacterController>();
                    if (cc)
                    {
                        if (ComputeBackAccelAtPoint(cc.bounds.center, origin, backAxis, out Vector3 accelCc))
                        {
                            accelCc = pullDir * accelCc.magnitude;
                            float dt = Time.fixedDeltaTime;
                            Vector3 lift = Vector3.up * (accelCc.magnitude * liftFraction);
                            if (!_ccExternalVel.TryGetValue(cc, out var vel)) vel = Vector3.zero;

                            vel += (accelCc + lift) * dt;
                            vel = Vector3.ClampMagnitude(vel, ccMaxExternalSpeed);

                            cc.Move(vel * dt);

                            vel = Vector3.Lerp(vel, Vector3.zero, ccDamping);
                            _ccExternalVel[cc] = vel;
                        }
                        continue;
                    }
                }

                var rb = col.attachedRigidbody;
                if (!rb || rb.isKinematic) continue;

                if (ComputeBackAccelAtPoint(rb.worldCenterOfMass, origin, backAxis, out Vector3 accelRb))
                {
                    accelRb = pullDir * accelRb.magnitude;

                    float g = Physics.gravity.magnitude;
                    float minAccel = approxMuStatic * g;
                    float mag = accelRb.magnitude;
                    if (mag < minAccel && mag > 1e-4f)
                        accelRb = accelRb.normalized * Mathf.Min(minAccel, backMaxAcceleration);

                    if (liftFraction > 0f)
                        rb.AddForce(Vector3.up * (accelRb.magnitude * liftFraction), ForceMode.Acceleration);

                    rb.AddForce(accelRb, ForceMode.Acceleration);
                }
            }
        }

        private bool ComputeAccelAtPoint(Vector3 point, Vector3 origin, Vector3 forward, out Vector3 accel)
        {
            Vector3 to = point - origin;
            float z = Vector3.Dot(to, forward);
            if (z < 0f || z > length) { accel = default; return false; }

            Vector3 radial = to - forward * z;
            float r = radial.magnitude;

            float sectionRadius = Mathf.Lerp(Mathf.Max(0f, startRadius), Mathf.Max(0f, endRadius), length > 0f ? z / length : 1f);
            if (r > sectionRadius + 1e-4f) { accel = default; return false; }

            float longT = Mathf.Clamp01(length > 0f ? z / length : 1f);
            float radT = Mathf.Clamp01(sectionRadius > 0f ? r / sectionRadius : 0f);
            float intensity = Mathf.Clamp01(longitudinalFalloff.Evaluate(longT)) * Mathf.Clamp01(radialFalloff.Evaluate(radT));
            if (intensity <= 0f) { accel = default; return false; }

            accel = forward * (maxAcceleration * intensity);
            return true;
        }

        private bool ComputeBackAccelAtPoint(Vector3 point, Vector3 origin, Vector3 backAxis, out Vector3 accel)
        {
            Vector3 to = point - origin;
            float z = Vector3.Dot(to, backAxis);
            if (z < 0f || z > backLength) { accel = default; return false; }

            Vector3 radial = to - backAxis * z;
            float r = radial.magnitude;

            float sectionRadius = Mathf.Lerp(Mathf.Max(0f, backStartRadius), Mathf.Max(0f, backEndRadius), backLength > 0f ? z / backLength : 1f);
            if (r > sectionRadius + 1e-4f) { accel = default; return false; }

            float longT = Mathf.Clamp01(backLength > 0f ? z / backLength : 1f);
            float radT = Mathf.Clamp01(sectionRadius > 0f ? r / sectionRadius : 0f);
            float intensity = Mathf.Clamp01(backLongitudinalFalloff.Evaluate(longT)) * Mathf.Clamp01(backRadialFalloff.Evaluate(radT));
            if (intensity <= 0f) { accel = default; return false; }

            accel = backAxis * (backMaxAcceleration * intensity);
            return true;
        }

        private bool HasLineOfSight(Vector3 emitter, Collider target)
        {
            Vector3 origin = emitter + Vector3.up * losOriginYOffset;
            Vector3 targetPoint = target.bounds.center;
            Vector3 dir = (targetPoint - origin);
            float dist = dir.magnitude;
            if (dist <= 1e-3f) return true;
            dir /= dist;

            return !Physics.SphereCast(origin, losProbeRadius, dir, out _, dist, occluderLayers, QueryTriggerInteraction.Ignore);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || gizmoRings < 1 || gizmoRingSegments < 3) return;

            Vector3 origin = transform.position + offsetVolumen;
            Vector3 fwd = transform.forward;
            Vector3 right = transform.right;
            Vector3 up = transform.up;

            if (length > 0f)
            {
                Gizmos.color = gizmoColorFront;
                DrawFrustum(origin, fwd, right, up, length, startRadius, endRadius);
            }

            if (enableBackSuction && backLength > 0f)
            {
                Gizmos.color = gizmoColorBack;
                DrawFrustum(origin, -fwd, right, up, backLength, backStartRadius, backEndRadius);
            }
        }

        private void DrawFrustum(Vector3 origin, Vector3 axis, Vector3 right, Vector3 up, float segLen, float r0, float r1)
        {
            Vector3 prevCenter = origin;
            float prevRadius = Mathf.Max(0f, r0);
            DrawWireDisc(prevCenter, right, up, prevRadius, gizmoRingSegments);

            for (int i = 1; i <= gizmoRings; i++)
            {
                float t = i / (float)gizmoRings;
                float z = t * segLen;
                float radius = Mathf.Lerp(Mathf.Max(0f, r0), Mathf.Max(0f, r1), t);
                Vector3 center = origin + axis * z;

                DrawWireDisc(center, right, up, radius, gizmoRingSegments);
                Gizmos.DrawLine(prevCenter + right * prevRadius, center + right * radius);
                Gizmos.DrawLine(prevCenter - right * prevRadius, center - right * radius);
                Gizmos.DrawLine(prevCenter + up * prevRadius, center + up * radius);
                Gizmos.DrawLine(prevCenter - up * prevRadius, center - up * radius);

                prevCenter = center;
                prevRadius = radius;
            }
        }

        private void DrawWireDisc(Vector3 center, Vector3 axisX, Vector3 axisY, float radius, int segments)
        {
            if (radius <= 0f) return;
            float step = Mathf.PI * 2f / segments;
            Vector3 prev = center + axisX * radius;
            for (int i = 1; i <= segments; i++)
            {
                float a = i * step;
                Vector3 p = center + axisX * (Mathf.Cos(a) * radius) + axisY * (Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
