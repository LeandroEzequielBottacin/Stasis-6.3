using System.Collections;
using Player.Scripts.MovementFSM.MVC;
using Puzzle_Elements.Tren_nuevo;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.PlataformasCorregidas
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class KinematicCargoPlatform : MonoBehaviour
    {
        public Model player;

        Vector3 _lastPosition;
        public Vector3 platformVelocity;

        public enum Mode { PingPong, Loop, Once }
        private enum Phase { Idle, Accel, Cruise, Decel, Dwell }

        [Header("Waypoints")]
        public Transform pointA;
        public Transform pointB;

        [Header("Movimiento")]
        public float cruiseSpeed = 2f;
        public float acceleration = 6f;
        public float dwellTime = 0.25f;
        public float arriveEpsilon = 0.005f;
        public Mode mode = Mode.PingPong;
        public bool autoStart = true;
        public bool startAtA = true;

        [Header("Eventos")]
        public UnityEvent onReachA;
        public UnityEvent onReachB;

        Rigidbody _rb;
        Phase _phase = Phase.Idle;

        Vector3 _from;
        Vector3 _to;
        Vector3 _dirN;

        float _distanceTotal;
        float _travelled;
        float _velocity;
        float _tDwell;

        bool _headingUp;

        Phase _lastPhase;

        [SerializeField]
        private ElevatorShipmentTrain _elevatorShipmentTrain;

        bool delayFinished;
        float delayRemaining = 1.5f;
        bool waitingDelay;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            _rb.isKinematic = true;

            _elevatorShipmentTrain =
                GetComponentInParent<ElevatorShipmentTrain>();

            _lastPosition = _rb.position;

            platformVelocity = Vector3.zero;
        }

        void OnEnable()
        {
            if (!pointA || !pointB)
            {
                Debug.LogError(
                    "[PlatformMoverTrapezoid] Asigna pointA/pointB.");

                enabled = false;

                return;
            }

            _rb.position =
                startAtA
                    ? pointA.position
                    : pointB.position;

            _lastPosition = _rb.position;

            _headingUp = startAtA;

            PrepareSegment();

            if (autoStart)
                StartMove();
        }

        void PrepareSegment()
        {
            _from =
                _headingUp
                    ? pointA.position
                    : pointB.position;

            _to =
                _headingUp
                    ? pointB.position
                    : pointA.position;

            _dirN =
                (_to - _from).normalized;

            _distanceTotal =
                Vector3.Distance(_from, _to);

            _travelled = 0f;

            _velocity = 0f;
        }

        // ==========================================
        // DELAY STASEABLE REAL
        // ==========================================

        public void StartMove()
        {
            delayRemaining = 1.5f;

            delayFinished = false;

            waitingDelay = true;

            StartCoroutine(DelayRoutine());
        }

        IEnumerator DelayRoutine()
        {
            while (delayRemaining > 0f)
            {
                if (_elevatorShipmentTrain != null &&
                    _elevatorShipmentTrain.IsFreezed)
                {
                    yield return null;

                    continue;
                }

                delayRemaining -= Time.deltaTime;

                yield return null;
            }

            delayFinished = true;

            waitingDelay = false;

            if (_elevatorShipmentTrain == null ||
                !_elevatorShipmentTrain.IsFreezed)
            {
                _phase = Phase.Accel;
            }
        }

        // ==========================================

        public void StopMove()
        {
            _phase = Phase.Idle;
        }

        public void Desestasear()
        {
            if (waitingDelay && delayFinished)
            {
                _phase = Phase.Accel;
            }
            else
            {
                _phase = _lastPhase;
            }
        }

        public void stasear()
        {
            if (_elevatorShipmentTrain == null ||
                _elevatorShipmentTrain.IsFreezed)
            {
                _lastPhase = _phase;

                _phase = Phase.Idle;
            }
        }

        public void ActivateKinematic()
        {
            _rb.isKinematic = true;
        }

        public void DesactivateKinematic()
        {
            _rb.isKinematic = false;
        }

        private void OnTriggerStay(Collider other)
        {
            Model model =
                other.GetComponent<Model>();

            if (model != null)
            {
                player = model;

                player.blockUseGravity = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Model model =
                other.GetComponent<Model>();

            if (model != null)
            {
                model.blockUseGravity = false;

                model.rb.useGravity = true;

                player = null;
            }
        }

        // ==========================================

        void FixedUpdate()
        {
            if (waitingDelay)
                return;

            if (_phase == Phase.Idle)
                return;

            float dt =
                Time.fixedDeltaTime;

            float dAccel =
                (cruiseSpeed * cruiseSpeed) /
                (2f * Mathf.Max(acceleration, 1e-4f));

            float dDecel =
                dAccel;

            float dMin =
                dAccel + dDecel;

            bool triangular =
                _distanceTotal <= dMin;

            float vTarget =
                triangular
                    ? Mathf.Sqrt(acceleration * _distanceTotal)
                    : cruiseSpeed;

            switch (_phase)
            {
                case Phase.Accel:
                    {
                        _velocity =
                            Mathf.MoveTowards(
                                _velocity,
                                vTarget,
                                acceleration * dt);

                        Step(_velocity * dt);

                        if (Reached())
                        {
                            Arrive();
                            break;
                        }

                        if (triangular &&
                            _travelled >= _distanceTotal * 0.5f)
                        {
                            _phase = Phase.Decel;
                        }
                        else if (!triangular &&
                                 _travelled >= dAccel)
                        {
                            _phase = Phase.Cruise;
                        }

                        break;
                    }

                case Phase.Cruise:
                    {
                        _velocity = vTarget;

                        float remaining =
                            _distanceTotal - _travelled;

                        if (remaining <= dDecel)
                        {
                            _phase = Phase.Decel;
                            break;
                        }

                        Step(_velocity * dt);

                        if (Reached())
                            Arrive();

                        break;
                    }

                case Phase.Decel:
                    {
                        float remaining =
                            _distanceTotal - _travelled;

                        if (remaining <= arriveEpsilon)
                        {
                            Arrive();
                            break;
                        }

                        float vStop =
                            Mathf.Sqrt(
                                Mathf.Max(
                                    0f,
                                    2f * acceleration * remaining));

                        _velocity =
                            Mathf.Min(_velocity, vStop);

                        _velocity =
                            Mathf.MoveTowards(
                                _velocity,
                                0f,
                                acceleration * dt);

                        Step(_velocity * dt);

                        break;
                    }
                case Phase.Dwell:
                    {
                        _tDwell -= dt;

                        if (_tDwell <= 0f)
                        {
                            if (mode == Mode.Once &&
                                _headingUp)
                            {
                                _phase = Phase.Idle;
                                break;
                            }

                            if (mode == Mode.Loop)
                            {
                                _headingUp = true;

                                _rb.position =
                                    pointA.position;
                            }
                            else
                            {
                                _headingUp = !_headingUp;
                            }

                            PrepareSegment();

                            _phase = Phase.Accel;
                        }

                        break;
                    }
            }

            // ==========================================
            // MOVER PLAYER CON LA PLATAFORMA
            // ==========================================

            if (player != null)
            {
                Vector3 delta =
                    _rb.position - _lastPosition;

                player.rb.position += delta;
            }

            // ==========================================
            // VELOCIDAD REAL
            // ==========================================

            Vector3 newPos =
                _rb.position;

            platformVelocity =
                (newPos - _lastPosition) /
                Mathf.Max(dt, 0.0001f);

            _lastPosition = newPos;
        }

        void Step(float step)
        {
            float remaining =
                _distanceTotal - _travelled;

            step =
                Mathf.Min(step, remaining);

            _rb.MovePosition(
                _rb.position + _dirN * step);

            _travelled += step;
        }

        bool Reached()
        {
            return
                (_distanceTotal - _travelled)
                <= arriveEpsilon;
        }

        void Arrive()
        {
            // NO MovePosition acá

            _rb.position = _to;

            _lastPosition = _to;

            platformVelocity = Vector3.zero;

            _travelled = _distanceTotal;

            _phase = Phase.Dwell;

            _tDwell = dwellTime;

            _velocity = 0f;

            if (_headingUp)
                onReachB?.Invoke();
            else
                onReachA?.Invoke();
        }
    }
}