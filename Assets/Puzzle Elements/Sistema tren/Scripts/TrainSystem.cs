using System;
using System.Collections;
using System.Collections.Generic;
using Managers.Game;
using Puzzle_Elements.Hedro_conteiner.Scripts;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Sistema_tren.Scripts
{
    [DefaultExecutionOrder(-300)]
    public class TrainSystem : MonoBehaviour
    {

        public Vector3 TrainVelocity { get; private set; }


        [Header("Container")]
        public HedronContainer container;

        [Header("Elevator (Rigidbody)")]
        public Rigidbody elevatorRb;
        public Transform elevatorWP1;
        public Transform elevatorWP2;
        public float elevatorSpeed = 2f;
        public float elevatorAcceleration = 20f;
        public float elevatorWaitSeconds = 2f;
        public float elevatorArriveThreshold = 0.02f;

        [Header("Barricade (Rigidbody)")]
        public Rigidbody barricadeRb;
        public Transform barricadeWP1;
        public Transform barricadeWP2;
        public float barricadeSpeed = 2.5f;
        public float barricadeArriveThreshold = 0.02f;

        public enum TrainPathMode { Loop, PingPong, Once }

        [Header("Train (Rigidbody kinematic + Waypoints)")]
        [Tooltip("Rigidbody del tren (debe estar en Kinematic = true).")]
        public Rigidbody trainRb;
        public List<Transform> trainWaypoints = new List<Transform>();
        public Transform departure;
        public float trainSpeed = 4f;
        [Tooltip("Aceleración del tren para el perfil trapezoidal.")]
        public float trainAcceleration = 6f;
        public float trainArriveThreshold = 0.03f;
        [Tooltip("Tiempo de espera en el extremo. En Loop también se usa antes del teletransporte al primero.")]
        public float trainTeleportWaitSeconds = 2f;
        [Tooltip("Loop = salta del último al primero; PingPong = va y vuelve sin teletransportar; Once = va del [0] al [1] y se queda.")]
        public TrainPathMode trainPathMode = TrainPathMode.Loop;

        private Transform initialPoint;
        private Transform finalPoint;

        [Header("Events")]
        public UnityEvent onTrainStarted;
        public UnityEvent onTrainStoppedAtDeparture;

        private bool systemEnabled;
        private bool trainRunRequested;
        private bool trainHaltAtDeparture;

        private Transform elevatorLow;
        private Transform elevatorHigh;
        private Transform barricadeDown;
        private Transform barricadeUp;

        private enum ElevatorState { Idle, ToA, WaitAtA, ToB, WaitAtB }
        private ElevatorState elevState = ElevatorState.Idle;
        private Vector3 elevTarget;
        private float elevCurrentSpeed;
        private float elevWaitUntil;
        private bool elevConfigured;

        private enum BarricadeState { Idle, ToDown, ToUp, HoldUp, HoldDown }
        private BarricadeState barrState = BarricadeState.Idle;
        private Vector3 barrTarget;
        private bool barrConfigured;
        private bool barricadeForceDown;

        // --- Tren (Loop / PingPong / Once) ---
        private bool trainConfigured;
        private int trainFirstIdx;
        private int trainLastIdx;
        private int trainDepIdx;
        private int trainCurrentIdx;
        private int trainDir = +1; // +1 adelante, -1 atrás
        private float trainWaitUntil;
        private enum TrainState { IdleAtDeparture, Running, PauseAtEnd, HoldAtEnd }
        private TrainState trainState = TrainState.IdleAtDeparture;
        private int trainPauseNextIdx;
        private bool trainPauseDoTeleport;

        // --- Perfil trapezoidal para el tren (por segmento) ---
        private bool trainSegmentActive;
        private Vector3 trainSegmentFrom;
        private Vector3 trainSegmentTo;
        private Vector3 trainSegmentDirN;
        private float trainSegmentDistance;
        private float trainSegmentTravelled;
        private float trainCurrentSpeed;

        private enum TrainProfilePhase { Accel, Cruise, Decel }
        private TrainProfilePhase trainProfilePhase;

        public UnityEvent eventsPlayerDeath;

        public Action OnTrenStartEngine;
        public Action OnTrenEndEngine;
        public Action OnTrenStartMovement;
        public Action OnTrenEndMovement;


        private bool _alreadyActivate;

        void Awake()
        {
            ResolveHeights();
            HookContainer(true);
            PrepareSystems();



            if (trainRb != null)
            {
                TrainVelocity = Vector3.zero;
            }
        }

        private void Start()
        {
            GameManager.Instance.OnDeathPlayer += TeleportTrainToStart;
        }

        void OnEnable()
        {
            HookContainer(true);
            PrepareSystems();
        }

        void OnDisable()
        {
            HookContainer(false);
            systemEnabled = false;
        }

        // API
        public void StartAllMovement()
        {
            systemEnabled = true;
            trainHaltAtDeparture = false;
            barricadeForceDown = false;
            ResolveHeights();
            PrepareSystems(true);
        }

        public void EnableSystem() => StartAllMovement();

        public void StartEngineTrain()
        {
            if (_alreadyActivate) return;
            OnTrenStartEngine?.Invoke();
            _alreadyActivate = true;
        }

        public void EndEngineTrain()
        {
            OnTrenEndEngine?.Invoke();
            _alreadyActivate = false;
        }

        public void RequestTrainHaltAtDeparture()
        {
            if (!trainConfigured) return;
            trainHaltAtDeparture = true;
            if (trainState == TrainState.IdleAtDeparture)
                trainRunRequested = true;
        }

        public void DisableSystem()
        {
            RequestTrainHaltAtDeparture();
            barricadeForceDown = true;
            if (barrConfigured && barricadeRb != null && barricadeDown != null)
            {
                barrState = BarricadeState.ToDown;
                barrTarget = barricadeDown.position;
            }
        }

        public void HardStopAll()
        {
            systemEnabled = false;
        }

        /// <summary>
        /// Teletransporta el tren al trainWaypoints[0] y resetea su estado a IdleAtDeparture.
        /// Útil si el jugador cayó y querés reiniciar el recorrido.
        /// </summary>
        public void TeleportTrainToStart()
        {
            if (!trainConfigured || trainWaypoints.Count == 0 || trainRb == null) return;

            // Siempre al índice 0 para el "start" del sistema
            TeleportRbTo(trainRb, trainWaypoints[0].position);
            ResetTrainSegmentProfile();

            barricadeRb.gameObject.transform.position = barricadeWP1.transform.position;
            // Reset de estado coherente con cualquier modo
            trainRunRequested = false;
            trainHaltAtDeparture = false;
            trainDir = +1;

            // Para ONCE forzamos la salida desde [0] hacia [1]
            if (trainPathMode == TrainPathMode.Once)
            {
                trainFirstIdx = 0;
                trainLastIdx = Mathf.Min(1, trainWaypoints.Count - 1);
                trainDepIdx = 0;
                trainCurrentIdx = 1; // próximo objetivo cuando se dispare la marcha
            }
            else
            {
                // Para Loop/PingPong mantenemos el departure configurado
                trainDepIdx = Mathf.Clamp(trainWaypoints.IndexOf(departure), 0, trainWaypoints.Count - 1);
                trainCurrentIdx = NextIndexFrom(trainDepIdx, +1);
            }

            trainState = TrainState.IdleAtDeparture;

            eventsPlayerDeath?.Invoke();
            OnTrenEndMovement?.Invoke();
        }

        IEnumerator OpenHedronConteiners()
        {
            yield return new WaitForSeconds(3f);
        }

        void FixedUpdate()
        {
            elevConfigured = IsElevatorConfigured();
            barrConfigured = IsBarricadeConfigured();
            trainConfigured = ValidateTrainSetup();

            TickElevator();
            TickBarricade();
            TickTrain();
        }

        // Setup
        private void HookContainer(bool add)
        {
            if (container == null) return;
            if (add)
            {
                container.onPlaced.AddListener(EnableSystem);
                container.onRemoved.AddListener(RequestTrainHaltAtDeparture);
            }
            else
            {
                container.onPlaced.RemoveListener(EnableSystem);
                container.onRemoved.RemoveListener(RequestTrainHaltAtDeparture);
            }
        }

        private void PrepareSystems(bool resetPositions = false)
        {
            // Elevator
            elevCurrentSpeed = 0f;
            if (elevConfigured)
            {
                Vector3 p = elevatorRb.position;
                float dLow = Vector3.Distance(p, elevatorLow.position);
                float dHigh = Vector3.Distance(p, elevatorHigh.position);
                Transform first = (dLow <= dHigh) ? elevatorHigh : elevatorLow;
                elevTarget = first.position;
                elevState = ElevatorState.ToA;
                elevWaitUntil = 0f;
            }
            else elevState = ElevatorState.Idle;

            // Barricade
            if (barrConfigured)
            {
                if (barricadeForceDown)
                {
                    barrState = BarricadeState.ToDown;
                    barrTarget = barricadeDown.position;
                }
                else
                {
                    barrState = BarricadeState.ToUp;
                    barrTarget = barricadeUp.position;
                }
            }
            else barrState = BarricadeState.Idle;

            // Train (Rigidbody Kinematic)
            ResetTrainSegmentProfile();

            if (ValidateTrainSetup())
            {
                // Índices por defecto
                trainFirstIdx = 0;
                trainLastIdx = trainWaypoints.Count - 1;

                if (trainPathMode == TrainPathMode.Once)
                {
                    // Para ONCE, definimos explícitamente 0 -> 1
                    trainDepIdx = 0;
                    trainLastIdx = Mathf.Max(1, trainLastIdx); // asegurar que al menos haya 1
                    if (resetPositions && !IsNear(trainRb.position, trainWaypoints[0].position, trainArriveThreshold))
                        TeleportRbTo(trainRb, trainWaypoints[0].position);

                    ResetTrainSegmentProfile();

                    trainState = TrainState.IdleAtDeparture;
                    trainRunRequested = false;
                    trainDir = +1;
                    trainCurrentIdx = 1; // siguiente objetivo será el [1]
                }
                else
                {
                    // Loop / PingPong conservan departure asignado
                    trainDepIdx = trainWaypoints.IndexOf(departure);
                    if (resetPositions && !IsNear(trainRb.position, trainWaypoints[trainDepIdx].position, trainArriveThreshold))
                        TeleportRbTo(trainRb, trainWaypoints[trainDepIdx].position);

                    ResetTrainSegmentProfile();

                    trainState = TrainState.IdleAtDeparture;
                    trainRunRequested = false;
                    trainDir = +1;
                    trainCurrentIdx = NextIndexFrom(trainDepIdx, +1);
                }
            }
            else trainState = TrainState.IdleAtDeparture;
        }

        private void ResolveHeights()
        {
            if (elevatorWP1 != null && elevatorWP2 != null)
            {
                if (elevatorWP1.position.y <= elevatorWP2.position.y) { elevatorLow = elevatorWP1; elevatorHigh = elevatorWP2; }
                else { elevatorLow = elevatorWP2; elevatorHigh = elevatorWP1; }
            }
            else { elevatorLow = null; elevatorHigh = null; }

            if (barricadeWP1 != null && barricadeWP2 != null)
            {
                if (barricadeWP1.position.y <= barricadeWP2.position.y) { barricadeDown = barricadeWP1; barricadeUp = barricadeWP2; }
                else { barricadeDown = barricadeWP2; barricadeUp = barricadeWP1; }
            }
            else { barricadeDown = null; barricadeUp = null; }
        }

        private bool IsElevatorConfigured() =>
            elevatorRb != null && elevatorLow != null && elevatorHigh != null;

        private bool IsBarricadeConfigured() =>
            barricadeRb != null && barricadeDown != null && barricadeUp != null;

        private bool ValidateTrainSetup()
        {
            if (trainRb == null) return false;
            if (!trainRb.isKinematic)
            {
                // Debug.LogWarning("[TrainSystem] trainRb debe ser Kinematic = true.", this);
            }
            if (trainWaypoints == null || trainWaypoints.Count < 2) return false;

            // Para ONCE no exigimos 'departure' (forzamos [0])
            if (trainPathMode == TrainPathMode.Once) return true;

            if (departure == null) return false;
            int dep = trainWaypoints.IndexOf(departure);
            return dep >= 0 && dep < trainWaypoints.Count;
        }

        // Elevator
        private void TickElevator()
        {
            if (!elevConfigured) return;

            switch (elevState)
            {
                case ElevatorState.ToA:
                {
                    bool reached = MoveElevatorAcc(elevTarget);
                    if (reached)
                    {
                        elevCurrentSpeed = 0f;
                        elevWaitUntil = Time.fixedUnscaledTime + elevatorWaitSeconds;
                        elevState = ElevatorState.WaitAtA;
                    }
                    break;
                }
                case ElevatorState.WaitAtA:
                {
                    if (Time.fixedUnscaledTime >= elevWaitUntil)
                    {
                        elevTarget = (IsNear(elevTarget, elevatorHigh.position, 1e-4f)) ? elevatorLow.position : elevatorHigh.position;
                        elevState = ElevatorState.ToB;
                    }
                    break;
                }
                case ElevatorState.ToB:
                {
                    bool reached = MoveElevatorAcc(elevTarget);
                    if (reached)
                    {
                        elevCurrentSpeed = 0f;
                        elevWaitUntil = Time.fixedUnscaledTime + elevatorWaitSeconds;
                        elevState = ElevatorState.WaitAtB;
                    }
                    break;
                }
                case ElevatorState.WaitAtB:
                {
                    if (Time.fixedUnscaledTime >= elevWaitUntil)
                    {
                        elevTarget = (IsNear(elevTarget, elevatorHigh.position, 1e-4f)) ? elevatorLow.position : elevatorHigh.position;
                        elevState = ElevatorState.ToA;
                    }
                    break;
                }
            }
        }

        private bool MoveElevatorAcc(Vector3 targetPos)
        {
            Vector3 toTarget = targetPos - elevatorRb.position;
            float distance = toTarget.magnitude;
            if (distance <= elevatorArriveThreshold)
            {
                elevatorRb.MovePosition(targetPos);
                return true;
            }

            float maxSpeed = Mathf.Max(0f, elevatorSpeed);
            float accel = Mathf.Max(0f, elevatorAcceleration);
            float maxSpeedForStop = Mathf.Sqrt(Mathf.Max(0f, 2f * accel * distance));
            float desiredSpeed = Mathf.Min(maxSpeed, maxSpeedForStop);
            elevCurrentSpeed = Mathf.MoveTowards(elevCurrentSpeed, desiredSpeed, accel * Time.fixedDeltaTime);

            float step = elevCurrentSpeed * Time.fixedDeltaTime;
            Vector3 dir = (distance > 1e-5f) ? (toTarget / distance) : Vector3.zero;
            Vector3 next = (step >= distance) ? targetPos : elevatorRb.position + dir * step;

            elevatorRb.MovePosition(next);
            return false;
        }

        // Barricade
        private void TickBarricade()
        {
            if (!barrConfigured) return;

            if (barricadeForceDown && barrState != BarricadeState.HoldDown)
            {
                barrState = BarricadeState.ToDown;
                barrTarget = barricadeDown.position;
            }

            switch (barrState)
            {
                case BarricadeState.ToDown:
                {
                    barrTarget = barricadeDown.position;
                    bool reached = MoveBodyLinear(barricadeRb, barrTarget, barricadeSpeed, barricadeArriveThreshold);
                    if (reached)
                    {
                        barrState = barricadeForceDown ? BarricadeState.HoldDown : BarricadeState.ToUp;
                    }
                    break;
                }
                case BarricadeState.ToUp:
                {
                    if (barricadeForceDown)
                    {
                        barrState = BarricadeState.ToDown;
                        break;
                    }

                    barrTarget = barricadeUp.position;
                    bool reached = MoveBodyLinear(barricadeRb, barrTarget, barricadeSpeed, barricadeArriveThreshold);
                    if (reached)
                    {
                        RequestStartTrain();
                        barrState = BarricadeState.HoldUp;
                    }
                    break;
                }
                case BarricadeState.HoldUp:
                    break;
                case BarricadeState.HoldDown:
                    break;
                case BarricadeState.Idle:
                    break;
            }
        }

        private void RequestStartTrain()
        {
            if (!systemEnabled || !trainConfigured) return;
            trainRunRequested = true;
            onTrainStarted?.Invoke();
            OnTrenStartMovement?.Invoke();
        }

        // Train (Rigidbody Kinematic + Loop / PingPong / Once)
        private void TickTrain()
        {
            if (!trainConfigured)
                return;

            switch (trainState)
            {
                case TrainState.IdleAtDeparture:
                    {
                        int depIndex =
                            (trainPathMode == TrainPathMode.Once)
                                ? 0
                                : trainDepIdx;

                        Vector3 depPos =
                            trainWaypoints[depIndex].position;

                        if (!IsNear(
                                trainRb.position,
                                depPos,
                                trainArriveThreshold))
                        {
                            TeleportRbTo(trainRb, depPos);

                            ResetTrainSegmentProfile();
                        }

                        if (trainRunRequested)
                        {
                            trainDir = +1;

                            trainCurrentIdx =
                                (trainPathMode == TrainPathMode.Once)
                                    ? 1
                                    : NextIndexFrom(depIndex, trainDir);

                            ResetTrainSegmentProfile();

                            trainState = TrainState.Running;
                        }

                        break;
                    }

                case TrainState.Running:
                    {
                        Vector3 target =
                            trainWaypoints[trainCurrentIdx].position;

                        bool reached =
                            MoveKinematicLinear(
                                trainRb,
                                target,
                                trainSpeed,
                                trainArriveThreshold
                            );

                        if (!reached)
                            break;

                        if (trainHaltAtDeparture &&
                            trainCurrentIdx == trainDepIdx &&
                            trainPathMode != TrainPathMode.Once)
                        {
                            trainRunRequested = false;

                            onTrainStoppedAtDeparture?.Invoke();

                            trainHaltAtDeparture = false;

                            trainState = TrainState.IdleAtDeparture;

                            ResetTrainSegmentProfile();

                            break;
                        }

                        if (trainPathMode == TrainPathMode.Once)
                        {
                            trainRunRequested = false;

                            trainState = TrainState.HoldAtEnd;

                            ResetTrainSegmentProfile();
                        }
                        else if (trainPathMode == TrainPathMode.Loop)
                        {
                            int next = trainCurrentIdx + 1;

                            if (next > trainLastIdx)
                            {
                                trainWaitUntil =
                                    Time.fixedUnscaledTime +
                                    trainTeleportWaitSeconds;

                                trainPauseDoTeleport = true;

                                trainPauseNextIdx =
                                    Mathf.Clamp(
                                        trainFirstIdx + 1,
                                        trainFirstIdx,
                                        trainLastIdx
                                    );

                                trainState = TrainState.PauseAtEnd;

                                ResetTrainSegmentProfile();
                            }
                            else
                            {
                                trainCurrentIdx = next;

                                ResetTrainSegmentProfile();
                            }
                        }
                        else
                        {
                            int next = trainCurrentIdx + trainDir;

                            if (next > trainLastIdx ||
                                next < trainFirstIdx)
                            {
                                trainDir *= -1;

                                trainWaitUntil =
                                    Time.fixedUnscaledTime +
                                    trainTeleportWaitSeconds;

                                trainPauseDoTeleport = false;

                                trainPauseNextIdx =
                                    Mathf.Clamp(
                                        trainCurrentIdx + trainDir,
                                        trainFirstIdx,
                                        trainLastIdx
                                    );

                                trainState = TrainState.PauseAtEnd;

                                ResetTrainSegmentProfile();
                            }
                            else
                            {
                                trainCurrentIdx = next;

                                ResetTrainSegmentProfile();
                            }
                        }

                        break;
                    }

                case TrainState.PauseAtEnd:
                    {
                        if (Time.fixedUnscaledTime < trainWaitUntil)
                            break;

                        if (trainPauseDoTeleport)
                        {
                            TeleportRbTo(
                                trainRb,
                                trainWaypoints[trainFirstIdx].position
                            );
                        }

                        ResetTrainSegmentProfile();

                        trainCurrentIdx = trainPauseNextIdx;

                        if (trainHaltAtDeparture &&
                            trainCurrentIdx == trainDepIdx)
                        {
                            trainRunRequested = false;

                            onTrainStoppedAtDeparture?.Invoke();

                            trainHaltAtDeparture = false;

                            trainState = TrainState.IdleAtDeparture;
                        }
                        else
                        {
                            trainState = TrainState.Running;
                        }

                        break;
                    }

                case TrainState.HoldAtEnd:
                    break;
            }

            // ===================================
            // VELOCIDAD REAL DEL TREN KINEMATIC
            // ===================================

        }
        private int NextIndexFrom(int idx, int dir)
        {
            int next = idx + Mathf.Clamp(dir, -1, 1);
            return Mathf.Clamp(next, 0, (trainWaypoints.Count - 1));
        }

        // Helpers: Rigidbody linear (ascensor/barricada)
        private bool MoveBodyLinear(Rigidbody rb, Vector3 targetPos, float speed, float arriveThreshold)
        {
            Vector3 toTarget = targetPos - rb.position;
            float dist = toTarget.magnitude;
            if (dist <= arriveThreshold)
            {
                rb.MovePosition(targetPos);
                return true;
            }

            float s = Mathf.Max(0f, speed);
            float step = s * Time.fixedDeltaTime;
            Vector3 dir = (dist > 1e-5f) ? (toTarget / dist) : Vector3.zero;
            Vector3 next = (step >= dist) ? targetPos : rb.position + dir * step;
            rb.MovePosition(next);
            return false;
        }

        // --- Perfil trapezoidal para el tren --- //
        private void ResetTrainSegmentProfile()
        {
            trainSegmentActive = false;
            trainSegmentDistance = 0f;
            trainSegmentTravelled = 0f;
            trainCurrentSpeed = 0f;
            trainProfilePhase = TrainProfilePhase.Accel;
        }

        private void BeginTrainSegment(Vector3 from, Vector3 to)
        {
            trainSegmentFrom = from;
            trainSegmentTo = to;
            trainSegmentDirN = (to - from).normalized;
            trainSegmentDistance = Vector3.Distance(from, to);
            trainSegmentTravelled = 0f;
            trainCurrentSpeed = 0f;
            trainProfilePhase = TrainProfilePhase.Accel;
            trainSegmentActive = true;
        }

        // Helpers: Rigidbody Kinematic (tren) con aceleración y desaceleración suaves
        private bool MoveKinematicLinear(Rigidbody rb,Vector3 targetPos,float cruiseSpeed,float arriveThreshold)
        {
            Vector3 toTarget =
                targetPos - rb.position;

            float distance =
                toTarget.magnitude;

            if (distance <= arriveThreshold)
            {
                TrainVelocity = Vector3.zero;

                rb.MovePosition(targetPos);

                trainCurrentSpeed = 0f;

                return true;
            }

            float dt =
                Time.fixedDeltaTime;

            float accel =
                Mathf.Max(1e-4f, trainAcceleration);

            float maxSpeed =
                Mathf.Max(0f, cruiseSpeed);

            float maxSpeedForStop =
                Mathf.Sqrt(
                    Mathf.Max(
                        0f,
                        2f * accel * distance));

            float desiredSpeed =
                Mathf.Min(
                    maxSpeed,
                    maxSpeedForStop);

            trainCurrentSpeed =
                Mathf.MoveTowards(
                    trainCurrentSpeed,
                    desiredSpeed,
                    accel * dt);

            float step =
                trainCurrentSpeed * dt;

            Vector3 dir =
                distance > 1e-5f
                    ? toTarget / distance
                    : Vector3.zero;

            //Vector3 movement =
            //    dir * step;

            //if (movement.magnitude > distance)
            //{
            //    movement =
            //        dir * distance;
            //}
            Vector3 movement =  dir * Mathf.Min(step, distance);

            Vector3 next =
                rb.position + movement;

            // =========================
            // VELOCIDAD REAL
            // =========================

            TrainVelocity =
                movement / dt;

            rb.MovePosition(next);

            return false;
        }
        public Vector3 GetTrainVelocity()
        {
            return TrainVelocity;
        }
        private static bool IsNear(Vector3 a, Vector3 b, float eps) =>
            (a - b).sqrMagnitude <= eps * eps;

        private void TeleportRbTo(Rigidbody rb, Vector3 pos)
        {
            if (rb == null)
                return;

            rb.position = pos;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (rb == trainRb)
            {
                ResetTrainSegmentProfile();

                TrainVelocity = Vector3.zero;

            }
        }
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (elevatorWP1 != null && elevatorWP2 != null)
            {
                Transform low = (elevatorWP1.position.y <= elevatorWP2.position.y) ? elevatorWP1 : elevatorWP2;
                Transform high = (low == elevatorWP1) ? elevatorWP2 : elevatorWP1;
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(low.position, 0.08f);
                Gizmos.color = Color.magenta;
                Gizmos.DrawCube(high.position, Vector3.one * 0.14f);
            }

            if (barricadeWP1 != null && barricadeWP2 != null)
            {
                Transform down = (barricadeWP1.position.y <= barricadeWP2.position.y) ? barricadeWP1 : barricadeWP2;
                Transform up = (down == barricadeWP1) ? barricadeWP2 : barricadeWP1;
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(down.position, 0.08f);
                Gizmos.color = Color.red;
                Gizmos.DrawCube(up.position, 0.14f * Vector3.one);
            }
        }
#endif
    }
}
