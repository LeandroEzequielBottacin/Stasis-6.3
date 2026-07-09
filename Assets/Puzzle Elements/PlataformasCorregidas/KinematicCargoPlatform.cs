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
        Phase _lastPhase = Phase.Idle;

        Vector3 _from;
        Vector3 _to;
        Vector3 _dirN;

        float _distanceTotal;
        float _travelled;
        float _velocity;
        float _tDwell;

        bool _headingUp;

        [SerializeField]
        private ElevatorShipmentTrain _elevatorShipmentTrain;

        bool delayFinished;
        float delayRemaining = 1.5f;
        bool waitingDelay;
        Coroutine delayCoroutine;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;

            _elevatorShipmentTrain = GetComponentInParent<ElevatorShipmentTrain>();

            _lastPosition = _rb.position;
            platformVelocity = Vector3.zero;
        }

        void OnEnable()
        {
            if (!pointA || !pointB)
            {
                Debug.LogError("[KinematicCargoPlatform] Asigna pointA y pointB.");
                enabled = false;
                return;
            }

            _rb.position = startAtA ? pointA.position : pointB.position;
            _lastPosition = _rb.position;

            _headingUp = startAtA;

            PrepareSegment();

            if (autoStart)
                StartMove();
        }

        void PrepareSegment()
        {
            _from = _headingUp ? pointA.position : pointB.position;
            _to = _headingUp ? pointB.position : pointA.position;

            Vector3 dir = _to - _from;

            _distanceTotal = dir.magnitude;
            _dirN = _distanceTotal > 0.0001f ? dir.normalized : Vector3.zero;

            _travelled = 0f;
            _velocity = 0f;
        }

        public void StartMove()
        {
            if (delayCoroutine != null)
                StopCoroutine(delayCoroutine);

            delayRemaining = 1.5f;
            delayFinished = false;
            waitingDelay = true;

            delayCoroutine = StartCoroutine(DelayRoutine());
        }

        IEnumerator DelayRoutine()
        {
            while (delayRemaining > 0f)
            {
                if (_elevatorShipmentTrain != null && _elevatorShipmentTrain.IsFreezed)
                {
                    yield return null;
                    continue;
                }

                delayRemaining -= Time.deltaTime;
                yield return null;
            }

            delayFinished = true;
            waitingDelay = false;

            if (_elevatorShipmentTrain == null || !_elevatorShipmentTrain.IsFreezed)
                _phase = Phase.Accel;
        }

        public void StopMove()
        {
            _phase = Phase.Idle;
        }

        public void Desestasear()
        {
            if (waitingDelay)
                return;

            if (delayFinished)
                _phase = _lastPhase == Phase.Idle ? Phase.Accel : _lastPhase;
        }

        public void stasear()
        {
            if (_elevatorShipmentTrain == null || _elevatorShipmentTrain.IsFreezed)
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
            Model model = other.GetComponent<Model>();

            if (model != null)
            {
                player = model;
                player.blockUseGravity = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Model model = other.GetComponent<Model>();

            if (model != null)
            {
                model.blockUseGravity = false;
                model.rb.useGravity = true;
                player = null;
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (waitingDelay || _phase == Phase.Idle)
            {
                _lastPosition = _rb.position;
                platformVelocity = Vector3.zero;
                return;
            }

            switch (_phase)
            {
                case Phase.Accel:
                    UpdateAccel(dt);
                    break;

                case Phase.Cruise:
                    UpdateCruise(dt);
                    break;

                case Phase.Decel:
                    UpdateDecel(dt);
                    break;

                case Phase.Dwell:
                    UpdateDwell(dt);
                    break;
            }

            Vector3 delta = _rb.position - _lastPosition;

            if (player != null)
                player.rb.position += delta;

            Vector3 newPos = _rb.position;
            platformVelocity = (newPos - _lastPosition) / Mathf.Max(dt, 0.0001f);
            _lastPosition = newPos;
        }

        void UpdateAccel(float dt)
        {
            float dAccel = (cruiseSpeed * cruiseSpeed) / (2f * Mathf.Max(acceleration, 0.0001f));
            float dDecel = dAccel;
            bool triangular = _distanceTotal <= dAccel + dDecel;

            float vTarget = triangular
                ? Mathf.Sqrt(acceleration * _distanceTotal)
                : cruiseSpeed;

            _velocity = Mathf.MoveTowards(_velocity, vTarget, acceleration * dt);

            Step(_velocity * dt);

            if (Reached())
            {
                Arrive();
                return;
            }

            if (triangular && _travelled >= _distanceTotal * 0.5f)
                _phase = Phase.Decel;
            else if (!triangular && _travelled >= dAccel)
                _phase = Phase.Cruise;
        }

        void UpdateCruise(float dt)
        {
            float dDecel = (cruiseSpeed * cruiseSpeed) / (2f * Mathf.Max(acceleration, 0.0001f));
            float remaining = _distanceTotal - _travelled;

            if (remaining <= dDecel)
            {
                _phase = Phase.Decel;
                return;
            }

            _velocity = cruiseSpeed;
            Step(_velocity * dt);

            if (Reached())
                Arrive();
        }

        void UpdateDecel(float dt)
        {
            float remaining = _distanceTotal - _travelled;

            if (remaining <= arriveEpsilon)
            {
                Arrive();
                return;
            }

            float vStop = Mathf.Sqrt(Mathf.Max(0f, 2f * acceleration * remaining));

            _velocity = Mathf.Min(_velocity, vStop);
            _velocity = Mathf.MoveTowards(_velocity, 0f, acceleration * dt);

            float step = Mathf.Max(_velocity * dt, arriveEpsilon);

            Step(step);

            if (Reached())
                Arrive();
        }

        void UpdateDwell(float dt)
        {
            _tDwell -= dt;

            if (_tDwell > 0f)
                return;

            if (mode == Mode.Once)
            {
                _phase = Phase.Idle;
                return;
            }

            if (mode == Mode.Loop)
            {
                _headingUp = true;
                _rb.position = pointA.position;
                _lastPosition = _rb.position;
            }
            else if (mode == Mode.PingPong)
            {
                _headingUp = !_headingUp;
            }

            PrepareSegment();
            _phase = Phase.Accel;
        }

        void Step(float step)
        {
            if (_distanceTotal <= 0.0001f)
            {
                Arrive();
                return;
            }

            float remaining = _distanceTotal - _travelled;
            step = Mathf.Min(step, remaining);

            Vector3 nextPosition = _rb.position + _dirN * step;

            _rb.MovePosition(nextPosition);

            _travelled += step;
        }

        bool Reached()
        {
            return (_distanceTotal - _travelled) <= arriveEpsilon;
        }

        void Arrive()
        {
            _rb.position = _to;

            _travelled = _distanceTotal;
            _velocity = 0f;

            _phase = Phase.Dwell;
            _tDwell = dwellTime;

            platformVelocity = Vector3.zero;

            if (_headingUp)
                onReachB?.Invoke();
            else
                onReachA?.Invoke();
        }
    }
}