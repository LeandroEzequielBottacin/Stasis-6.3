using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class LaunchPlate : MonoBehaviour
    {
        [Header("Trajectories (children are checkpoints)")]
        [Tooltip("Parent cuyo HIJO 0..N definen la ruta de trayectoria para el PLAYER.\nSe toman las posiciones de los hijos en orden.")]
        [SerializeField] private Transform playerTrajectoryParent;

        [Tooltip("Parent cuyo HIJO 0..N definen la ruta de trayectoria para OBJETOS no-player.\nSe toman las posiciones de los hijos en orden.")]
        [SerializeField] private Transform objectTrajectoryParent;

        [Header("Launch Origin")]
        [Tooltip("Si está activo, usa 'launchOrigin' (o el primer hijo del parent activo) como punto de partida.\nSi está desactivado, se usa la posición actual del rigidbody.")]
        [SerializeField] private bool useFixedLaunchOrigin = true;

        [Tooltip("Origen fijo opcional para el lanzamiento. Si no se asigna, usa el PRIMER hijo del parent de trayectoria activo, o el transform de esta placa.")]
        [SerializeField] private Transform launchOrigin;

        [Tooltip("Si está activo y se usa origen fijo, teletransporta el cuerpo al origen antes del lanzamiento y limpia su velocidad/angular.")]
        [SerializeField] private bool snapBodyToOrigin;

        [Header("Profile (curves & feel)")]
        [Tooltip("Perfil (ScriptableObject) con curvas y ajustes de 'feel'.\nSi es NULL, se usan los parámetros de 'Fallback' de abajo.")]
        [SerializeField] private LaunchPlateProfile profile;

        [Header("Fallback (si no hay perfil)")]
        [Tooltip("Velocidad mínima inicial (m/s) para el solver cuando NO hay perfil.")]
        [SerializeField] private float minInitialSpeed = 18f;

        [Tooltip("Límite superior del ángulo de lanzamiento (grados) cuando NO hay perfil.\nAyuda a mantener arcos más chatos/arcade.")]
        [SerializeField, Range(5f, 45f)] private float maxElevationDeg = 25f;

        [Tooltip("Tiempo mínimo y máximo de vuelo permitidos para la búsqueda del solver (segundos) cuando NO hay perfil.")]
        [SerializeField] private float tMin = 0.15f, tMax = 2.0f;

        [Tooltip("Iteraciones de la búsqueda 1D del tiempo T (más iteraciones = más precisión, más costo).")]
        [SerializeField, Range(8, 64)] private int tSearchIterations = 32;

        [Tooltip("Si hay dos soluciones similares (rápida/lenta), preferir la rama más lenta (más estable).")]
        [SerializeField] private bool preferSlowerBranch = true;

        [Header("Cooldown")]
        [Tooltip("Tiempo tras un lanzamiento durante el cual la placa queda ocupada y no relanza (segundos).")]
        [SerializeField] private float cooldown = 0.25f;

        [Tooltip("Si está activo, mientras la placa esté ocupada ignora nuevas entradas (un solo disparo activo).")]
        [SerializeField] private bool singleFireWhileActive = true;

        [Header("Gizmos (editor)")]
        [Tooltip("Dibuja gizmos de la ruta y los checkpoints en el editor.")]
        [SerializeField] private bool drawGizmos = true, alwaysDraw;

        [Tooltip("Detalle de la polilínea de la parábola dibujada (segmentos).")]
        [SerializeField, Range(8, 128)] private int gizmoDetail = 48;

        [Tooltip("Color de los checkpoints para la trayectoria del jugador.")]
        [SerializeField] private Color playerColor = new(0.25f, 0.9f, 0.35f);

        [Tooltip("Color de los checkpoints para la trayectoria de objetos.")]
        [SerializeField] private Color objectColor = new(0.2f, 0.55f, 1.0f);

        [Tooltip("Color de la curva balística planificada.")]
        [SerializeField] private Color ballisticColor = new(1.0f, 0.85f, 0.2f);

        [Tooltip("Color de los puntos de corrección (nudges) dibujados en la parábola.")]
        [SerializeField] private Color nudgeColor = new(1.0f, 0.8f, 0.2f);

        // ——— internos ———
        private bool _busy;
        private float _lastTriggerTime;

        void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void OnValidate()
        {
            tMin = Mathf.Max(0.05f, tMin);
            tMax = Mathf.Max(tMin + 0.05f, tMax);
            minInitialSpeed = Mathf.Max(0f, minInitialSpeed);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_busy && singleFireWhileActive) return;

            var rb = other.attachedRigidbody ?? other.GetComponentInParent<Rigidbody>();
            if (!rb) return;

            bool isPlayer = other.CompareTag("Player") || other.transform.root.CompareTag("Player");
            Transform parent = (isPlayer && playerTrajectoryParent) ? playerTrajectoryParent : objectTrajectoryParent;
            if (!parent || parent.childCount < 2) return;

            // Coyote time (desde perfil)
            if (Time.time - _lastTriggerTime < GetCoyote()) return;
            _lastTriggerTime = Time.time;

            var checkpoints = BuildCheckpoints(parent, GetOverrideCheckpointCount());
            Vector3 end = checkpoints[^1];

            // start: si snappeás, origen fijo; si no, desde la pos actual
            Vector3 start = (snapBodyToOrigin && useFixedLaunchOrigin)
                ? ResolveOrigin(parent)
                : (!snapBodyToOrigin ? rb.position : (useFixedLaunchOrigin ? ResolveOrigin(parent) : rb.position));

            // windup opcional (anticipación + slowmo leve)
            float wind = GetWindup();
            if (wind > 0f)
            {
                StartCoroutine(WindupAndLaunch(rb, isPlayer, parent, checkpoints, start, end, wind));
            }
            else
            {
                DoLaunch(rb, isPlayer, parent, checkpoints, start, end);
            }

            _busy = true;
            StartCoroutine(ClearBusyAfter(cooldown));
        }

        IEnumerator WindupAndLaunch(Rigidbody rb, bool isPlayer, Transform parent, List<Vector3> checkpoints, Vector3 start, Vector3 end, float wind)
        {
            float originalScale = Time.timeScale;
            float dil = GetTimeDilation();
            if (dil < 0.999f) Time.timeScale = dil;

            // pegadito y freeze leve si snappea
            if (snapBodyToOrigin && useFixedLaunchOrigin)
            {
                Vector3 s = ResolveOrigin(parent);
                rb.position = s;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            yield return new WaitForSeconds(wind * (dil < 0.999f ? dil : 1f));
            if (dil < 0.999f) Time.timeScale = originalScale;

            DoLaunch(rb, isPlayer, parent, checkpoints, start, end);
        }

        void DoLaunch(Rigidbody rb, bool isPlayer, Transform parent, List<Vector3> checkpoints, Vector3 start, Vector3 end)
        {
            // plan con T óptimo
            var plan = ComputeBallisticPlanFitToCheckpoints(
                start, end, checkpoints,
                GetMinInitialSpeed(start, end), GetTMin(), GetTMax(), GetIterations(), GetPreferSlow());
            if (plan.T <= 0f) return;

            // preservación horizontal del momentum entrante (feel)
            Vector3 v0 = plan.v0;
            float preserve = GetPreserveHoriz();
            if (preserve > 0f)
            {
                Vector3 inHoriz = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
                Vector3 outHoriz = Vector3.ProjectOnPlane(v0, Vector3.up);
                Vector3 mixedHoriz = Vector3.Lerp(outHoriz, inHoriz, preserve);
                v0 = new Vector3(mixedHoriz.x, v0.y, mixedHoriz.z);
            }

            // aplicar impulso (vertical exacto, horiz mezclado)
            Vector3 deltaV = v0 - rb.linearVelocity;
            rb.AddForce(deltaV, ForceMode.VelocityChange);

            // schedule de nudges con parámetros del perfil
            var schedule = BuildNudgeSchedule(checkpoints, start, new BallisticPlan { v0 = v0, T = plan.T });
            StartCoroutine(FlyWithDiscreteNudges(rb, new BallisticPlan { v0 = v0, T = plan.T }, schedule));

#if UNITY_EDITOR
            // opcional gizmo cache
            // CacheForGizmos(start, end, new BallisticPlan{ v0 = v0, T = plan.T }, checkpoints, schedule, isPlayer);
#endif
        }

        // ======== Solver original (con clamp de elevación) ========
        struct BallisticPlan { public Vector3 v0; public float T; }

        BallisticPlan ComputeBallisticPlanFitToCheckpoints(
            Vector3 start, Vector3 end, List<Vector3> checkpoints,
            float minSpeed, float Tmin, float Tmax, int iters, bool preferSlow)
        {
            Vector3 g = Physics.gravity;

            // alphas por arco
            int n = checkpoints.Count;
            var alphas = new float[n];
            float L = 0f; var cum = new float[n]; cum[0] = 0f;
            for (int i = 1; i < n; i++) { L += Vector3.Distance(checkpoints[i - 1], checkpoints[i]); cum[i] = L; }
            for (int i = 0; i < n; i++) alphas[i] = L > 1e-4f ? (cum[i] / L) : (i / (float)(n - 1));

            Vector3 V0OfT(float T) => (end - start - 0.5f * g * (T * T)) / Mathf.Max(T, 1e-4f);
            float SpeedOfT(float T) => V0OfT(T).magnitude;

            float Cost(float T)
            {
                float sp = SpeedOfT(T);
                float penalty = (minSpeed > 0f && sp < minSpeed) ? Mathf.Pow((minSpeed - sp) * 20f, 2f) : 0f;
                Vector3 v0 = V0OfT(T);
                float sse = 0f;
                for (int i = 1; i < n - 1; i++)
                {
                    float ti = alphas[i] * T;
                    Vector3 pi = start + v0 * ti + 0.5f * g * ti * ti;
                    float di2 = (pi - checkpoints[i]).sqrMagnitude;
                    sse += di2;
                }
                return sse + penalty;
            }

            float a = Tmin, b = Tmax;
            for (int i = 0; i < iters; i++)
            {
                float l = Mathf.Lerp(a, b, 1f / 3f);
                float r = Mathf.Lerp(a, b, 2f / 3f);
                if (Cost(l) < Cost(r)) b = r; else a = l;
            }
            float Tbest = 0.5f * (a + b);
            Vector3 v0best = V0OfT(Tbest);

            // clamp de elevación (desde perfil/fallback)
            float maxDeg = GetMaxElevationDeg();
            if (maxDeg > 0f)
            {
                float sp = v0best.magnitude;
                if (sp > 1e-4f)
                {
                    Vector3 dir = v0best.normalized;
                    float maxSin = Mathf.Sin(maxDeg * Mathf.Deg2Rad);
                    float vy = Mathf.Clamp(dir.y, -maxSin, maxSin);
                    float vxz = Mathf.Sqrt(Mathf.Max(0f, 1f - vy * vy));
                    Vector3 horiz = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
                    if (horiz.sqrMagnitude < 1e-6f) horiz = Vector3.forward;
                    dir = horiz * vxz + Vector3.up * vy;
                    v0best = dir * sp;
                }
            }
            return new BallisticPlan { v0 = v0best, T = Tbest };
        }

        // ======== Nudges (ajustados con deadzone suave) ========
        struct Nudge { public float t; public Vector3 point; public Vector3 vDesired; public bool fired; }

        List<Nudge> BuildNudgeSchedule(List<Vector3> checkpoints, Vector3 start, BallisticPlan plan)
        {
            int n = checkpoints.Count;
            var list = new List<Nudge>(n - 1);
            float pathLen = EstimatePolylineLength(checkpoints);

            for (int i = 1; i < n; i++)
            {
                Vector3 P = checkpoints[i];
                float tStar = ClosestTimeToPoint(start, plan.v0, Physics.gravity, plan.T, P, 18);

                Vector3 next = (i < n - 1) ? checkpoints[i + 1] : checkpoints[^1];
                Vector3 vDesDir = (next - P);
                vDesDir = vDesDir.sqrMagnitude > 1e-6f ? vDesDir.normalized : Vector3.forward;
                float vMag = pathLen / Mathf.Max(0.05f, plan.T);

                list.Add(new Nudge { t = tStar, point = P, vDesired = vDesDir * vMag, fired = false });
            }
            return list;
        }

        static float ClosestTimeToPoint(Vector3 s, Vector3 v0, Vector3 g, float T, Vector3 P, int iters)
        {
            float a = 0f, b = T;
            float F(float t) { Vector3 x = s + v0 * t + 0.5f * g * t * t; return (x - P).sqrMagnitude; }
            for (int i = 0; i < iters; i++)
            {
                float l = Mathf.Lerp(a, b, 1f / 3f);
                float r = Mathf.Lerp(a, b, 2f / 3f);
                if (F(l) < F(r)) b = r; else a = l;
            }
            return 0.5f * (a + b);
        }

        IEnumerator FlyWithDiscreteNudges(Rigidbody rb, BallisticPlan plan, List<Nudge> schedule)
        {
            float elapsed = 0f;
            float tw = GetNudgeTimeWindow();
            float prox = GetNudgeProximity();
            float kp = GetNudgeKp();
            float kd = GetNudgeKd();
            float maxF = GetMaxNudgeForce();
            bool lateralOnly = GetLateralOnly();

            while (elapsed < plan.T)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                for (int i = 0; i < schedule.Count; i++)
                {
                    var n = schedule[i];
                    if (n.fired) continue;

                    float dt = elapsed - n.t;
                    float timeWeight = Mathf.Exp(-(dt * dt) / (2f * tw * tw));
                    if (timeWeight < 0.03f) continue;

                    float dist = Vector3.Distance(rb.position, n.point);
                    float spaceWeight = Mathf.Exp(-(dist * dist) / (2f * prox * prox));

                    // deadzone suave
                    float w = Mathf.Clamp01(timeWeight * spaceWeight);
                    if (w < 0.15f) continue;

                    Vector3 posErr = (n.point - rb.position);
                    Vector3 velErr = (n.vDesired - rb.linearVelocity);
                    Vector3 force = (kp * posErr + kd * velErr) * rb.mass;

                    if (lateralOnly && rb.linearVelocity.sqrMagnitude > 1e-6f)
                    {
                        Vector3 vN = rb.linearVelocity.normalized;
                        force -= Vector3.Project(force, vN);
                    }

                    rb.AddForce(Vector3.ClampMagnitude(force, maxF) * w, ForceMode.Force);

                    if (elapsed > n.t + tw * 1.6f)
                    {
                        n.fired = true;
                        schedule[i] = n;
                    }
                }
            }
        }

        // ======== Utilidades ========
        static List<Vector3> BuildCheckpoints(Transform parent, int overrideCount)
        {
            var pts = new List<Vector3>(Mathf.Max(2, parent.childCount));
            for (int i = 0; i < parent.childCount; i++) pts.Add(parent.GetChild(i).position);
            if (overrideCount <= 0 || overrideCount >= pts.Count) return pts;

            float L = 0f; var cum = new List<float> { 0f };
            for (int i = 1; i < pts.Count; i++) { L += Vector3.Distance(pts[i - 1], pts[i]); cum.Add(L); }

            var outPts = new List<Vector3>(overrideCount);
            for (int k = 0; k < overrideCount; k++)
            {
                float s = (L * k) / (overrideCount - 1);
                int idx = 0; while (idx < cum.Count - 1 && cum[idx + 1] < s) idx++;
                float s0 = cum[idx], s1 = cum[Mathf.Min(idx + 1, cum.Count - 1)];
                float u = (s1 > s0) ? (s - s0) / (s1 - s0) : 0f;
                Vector3 p = Vector3.Lerp(pts[idx], pts[Mathf.Min(idx + 1, pts.Count - 1)], u);
                outPts.Add(p);
            }
            return outPts;
        }

        static float EstimatePolylineLength(List<Vector3> pts)
        {
            float L = 0f; for (int i = 1; i < pts.Count; i++) L += Vector3.Distance(pts[i - 1], pts[i]);
            return Mathf.Max(L, 0.01f);
        }

        Vector3 ResolveOrigin(Transform activeParent)
        {
            if (launchOrigin) return launchOrigin.position;
            if (activeParent && activeParent.childCount > 0) return activeParent.GetChild(0).position;
            return transform.position;
        }

        IEnumerator ClearBusyAfter(float t) { yield return new WaitForSeconds(t); _busy = false; }

        // ======== Getters que consideran perfil o fallback ========
        float GetRemap01(float d)
        {
            if (!profile) return Mathf.InverseLerp(2f, 25f, d);
            return Mathf.InverseLerp(profile.distanceRemapMeters.x, profile.distanceRemapMeters.y, d);
        }

        float GetMinInitialSpeed(Vector3 start, Vector3 end)
        {
            float baseSpd = profile ? profile.baseInitialSpeed : minInitialSpeed;
            float u = GetRemap01(Vector3.Distance(start, end));
            float mult = 1f;

            if (profile)
            {
                switch (profile.powerMode)
                {
                    case LaunchPlateProfile.PowerMode.ByDistance:
                        mult = profile.speedByDistance.Evaluate(u);
                        break;
                    case LaunchPlateProfile.PowerMode.ByHoldTime:
                        // Podés más adelante pasar un hold01 real; por ahora usamos la distancia como fallback
                        mult = Mathf.Max(0.1f, profile.powerByHold.Evaluate(u));
                        break;
                    default: mult = 1f; break;
                }
            }
            return baseSpd * mult;
        }

        float GetMaxElevationDeg()
        {
            return profile ? Mathf.Max(0f, profile.maxElevationDeg) : maxElevationDeg;
        }
        float GetTMin() => profile ? profile.tMin : tMin;
        float GetTMax() => profile ? profile.tMax : tMax;
        int GetIterations() => profile ? profile.tSearchIterations : tSearchIterations;
        bool GetPreferSlow() => profile ? profile.preferSlowerBranch : preferSlowerBranch;

        float GetPreserveHoriz() => profile ? profile.preserveIncomingHoriz : 0.35f;
        float GetWindup() => profile ? profile.windupSeconds : 0f;
        float GetCoyote() => profile ? profile.coyoteTime : 0.08f;
        float GetTimeDilation() => profile ? profile.timeDilationDuringWindup : 1f;

        int GetOverrideCheckpointCount() => profile ? profile.overrideCheckpointCount : 0;
        float GetNudgeTimeWindow() => profile ? profile.nudgeTimeWindow : 0.12f;
        float GetNudgeProximity() => profile ? profile.nudgeProximity : 1.25f;
        float GetNudgeKp() => profile ? profile.nudgeKp : 1.6f;
        float GetNudgeKd() => profile ? profile.nudgeKd : 0.42f;
        float GetMaxNudgeForce() => profile ? profile.maxNudgeForce : 100f;
        bool GetLateralOnly() => profile ? profile.lateralOnly : true;

        // ---------- Gizmos ----------
#if UNITY_EDITOR
        Vector3 _gStart, _gEnd;
        BallisticPlan _gPlan;
        List<Vector3> _gCheckpoints, _gBallistic;
        List<float> _gTimes;

        void CacheForGizmos(Vector3 start, Vector3 end, BallisticPlan plan, List<Vector3> checkpoints, List<Nudge> sched,
            bool isPlayer)
        {
            _gStart = start;
            _gEnd = end;
            _gPlan = plan;
            _gCheckpoints = checkpoints;
            _gTimes = new List<float>(sched.Count);
            foreach (var n in sched) _gTimes.Add(n.t);

            int N = Mathf.Max(8, gizmoDetail);
            _gBallistic = new List<Vector3>(N + 1);
            for (int i = 0; i <= N; i++)
            {
                float t = Mathf.Lerp(0f, plan.T, i / (float)N);
                Vector3 p = start + plan.v0 * t + 0.5f * Physics.gravity * t * t;
                _gBallistic.Add(p);
            }
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (!alwaysDraw && !Selection.Contains(gameObject)) return;

            DrawParent(playerTrajectoryParent, playerColor);
            DrawParent(objectTrajectoryParent, objectColor);

            if (_gBallistic != null && _gBallistic.Count > 1)
            {
                Gizmos.color = ballisticColor;
                for (int i = 0; i < _gBallistic.Count - 1; i++)
                    Gizmos.DrawLine(_gBallistic[i], _gBallistic[i + 1]);

                Gizmos.color = new Color(1f, 0.5f, 0.1f);
                Gizmos.DrawSphere(_gStart, 0.05f);
                Gizmos.color = new Color(1f, 0.3f, 0f);
                Gizmos.DrawSphere(_gEnd, 0.05f);

                // checkpoints + punto de parabola en t*
                for (int i = 0; i < _gCheckpoints.Count; i++)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(_gCheckpoints[i], 0.045f);

                    if (_gTimes != null && i > 0 && i - 1 < _gTimes.Count)
                    {
                        float t = _gTimes[i - 1];
                        Vector3 p = _gStart + _gPlan.v0 * t + 0.5f * Physics.gravity * t * t;
                        Gizmos.color = nudgeColor;
                        Gizmos.DrawWireSphere(p, 0.05f);
                    }
                }
            }
        }

        void DrawParent(Transform parent, Color c)
        {
            if (!parent || parent.childCount < 2) return;

            Gizmos.color = new Color(c.r, c.g, c.b, 0.9f);
            for (int i = 0; i < parent.childCount; i++)
            {
                var p = parent.GetChild(i).position;
                Gizmos.DrawSphere(p, 0.045f);
                if (i > 0) Gizmos.DrawLine(parent.GetChild(i - 1).position, p);
            }
        }
#endif
    }
}
