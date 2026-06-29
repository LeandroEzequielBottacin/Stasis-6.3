using System.Collections;
using Managers.Events;
using UnityEngine;

namespace Player.Stasis
{
    public class StasisBeam : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private float beamSpeed = 30f;

        [Header("VFX (Partículas)")]
        [Tooltip("Prefab world-space que se instancia en el muzzle (se queda atrás). Debe tener PortalTrailPSController.")]
        [SerializeField] private TrailPS muzzleScribblesPrefab;

        [Tooltip("Controlador del emisor que viaja con el rayo (hijo de este GO).")]
        [SerializeField] private TrailPS travellingScribbles;

        [Header("Linger/Fade")]
        [SerializeField] private float lingerTime = 0.35f;
        [SerializeField] private float fadeDuration = 0.25f;

        [Header("Luz")]
        [SerializeField] private float lightOffDelay = .3f;
        [SerializeField] private Light lightStasis;

        [Header("Sonido")]
        public string failEventName    = "StasisFail";
        public string successEventName = "StasisSuccess";

        private Coroutine _beamRoutine;

        public void SetBeam(Vector3 start, Vector3 end, bool hit)
        {
            if (_beamRoutine != null) StopCoroutine(_beamRoutine);

            transform.position = start;
            Vector3 dir = (end - start).normalized;
            float   dist = Vector3.Distance(start, end);

            // 1) MUZZLE (world-space, se queda atrás desde el primer frame)
            if (muzzleScribblesPrefab)
            {
                var muzzle = Instantiate(muzzleScribblesPrefab, start, Quaternion.LookRotation(dir));
                ForceWorldSpacePreset(muzzle);          // asegura World y clamp forward
                RandomizeNoiseSeed(muzzle);             // pequeñas variaciones por disparo
                muzzle.lingerTime   = lingerTime;
                muzzle.fadeDuration = fadeDuration;
                muzzle.PlayOnce();
                // Se autodestruye en StopWithLinger()
                muzzle.StopWithLinger();                // programamos su fade sin esperar
            }

            // 2) HEAD / TRAVELLING (va con el rayo)
            if (travellingScribbles)
            {
                ForceHeadPreset(travellingScribbles);   // asegura Local + clamp Z
                RandomizeNoiseSeed(travellingScribbles);
                travellingScribbles.lingerTime   = lingerTime;
                travellingScribbles.fadeDuration = fadeDuration;
                travellingScribbles.PlayOnce();
            }

            if (lightStasis) lightStasis.enabled = true;
            gameObject.SetActive(true);

            _beamRoutine = StartCoroutine(MoveForward(dir, dist, hit));
        }

        private IEnumerator MoveForward(Vector3 direction, float maxDistance, bool hit)
        {
            float travelled = 0f;
            while (travelled < maxDistance)
            {
                float step = beamSpeed * Time.deltaTime;

                if (Physics.Raycast(transform.position, direction, out var info, step, LayerMask.GetMask("Ground", "Wall", "Physics Objects")))
                {
                    transform.position = info.point;
                    break;
                }

                transform.position += direction * step;
                travelled += step;
                yield return null;
            }

            if (lightStasis) lightStasis.enabled = false;

            // apagado ordenado del emisor que viaja
            if (travellingScribbles)
            {
                bool done = false;
                travellingScribbles.StopWithLinger(() => done = true);
                yield return new WaitUntil(() => done);
            }

            // SFX
            yield return new WaitForSeconds(0.1f);
            EventManager.TriggerEvent(hit ? successEventName : failEventName, gameObject);

            if (lightOffDelay > 0f) yield return new WaitForSeconds(lightOffDelay);
            Destroy(gameObject);
        }

        // ---------- Helpers de preset (evitan que el PS esté mal seteado) ----------

        // ReSharper disable Unity.PerformanceAnalysis
        private static void ForceWorldSpacePreset(TrailPS ctrl)
        {
            var ps = ctrl.ps ? ctrl.ps : ctrl.GetComponentInChildren<ParticleSystem>(true);
            if (!ps) return;

            var main   = ps.main;   main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em     = ps.emission; em.enabled = true;        // deja tus bursts + rate-over-distance
            var trails = ps.trails;  trails.enabled = true; trails.mode = ParticleSystemTrailMode.PerParticle; trails.worldSpace = true;

            // ruido y deriva lateral visibles desde el frame 0
            var inh = ps.inheritVelocity; inh.mode = ParticleSystemInheritVelocityMode.Current; inh.curveMultiplier = 0.4f;
            var vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.Local;
            vol.x = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            vol.y = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            vol.z = new ParticleSystem.MinMaxCurve(0f, 0.4f);

            var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.separateAxes = true; limit.space = ParticleSystemSimulationSpace.Local;
            limit.limitX = 100f; limit.limitY = 100f; limit.limitZ = 8f; limit.dampen = 0.25f;

            var noise = ps.noise; noise.enabled = true; noise.separateAxes = true;
            noise.strengthX = 4f; noise.strengthY = 4f; noise.strengthZ = 0.4f;
            noise.frequency = 0.35f; noise.scrollSpeed = 14f; noise.damping = false; noise.octaveCount = 2;
        }

        private static void ForceHeadPreset(TrailPS ctrl)
        {
            var ps = ctrl.ps ? ctrl.ps : ctrl.GetComponentInChildren<ParticleSystem>(true);
            if (!ps) return;

            var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var trails = ps.trails; trails.enabled = true; trails.mode = ParticleSystemTrailMode.PerParticle; trails.worldSpace = true;

            var inh = ps.inheritVelocity; inh.mode = ParticleSystemInheritVelocityMode.Current; inh.curveMultiplier = 0.7f;

            var vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.Local;
            vol.x = new ParticleSystem.MinMaxCurve(-2.0f, 2.0f);
            vol.y = new ParticleSystem.MinMaxCurve(-2.0f, 2.0f);
            vol.z = new ParticleSystem.MinMaxCurve(0f, 0.6f);

            var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.separateAxes = true; limit.space = ParticleSystemSimulationSpace.Local;
            limit.limitX = 100f; limit.limitY = 100f; limit.limitZ = 10f; limit.dampen = 0.25f;

            var noise = ps.noise; noise.enabled = true; noise.separateAxes = true;
            noise.strengthX = 3.5f; noise.strengthY = 3.5f; noise.strengthZ = 0.5f;
            noise.frequency = 0.35f; noise.scrollSpeed = 13f; noise.damping = false; noise.octaveCount = 2;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private static void RandomizeNoiseSeed(TrailPS ctrl)
        {
            var ps = ctrl.ps ? ctrl.ps : ctrl.GetComponentInChildren<ParticleSystem>(true);
            if (!ps) return;
            // “truco” para variar el campo de noise por instancia:
            var noise = ps.noise;
            noise.scrollSpeed = noise.scrollSpeed.constant + Random.Range(-2f, 2f);
        }
    }
}