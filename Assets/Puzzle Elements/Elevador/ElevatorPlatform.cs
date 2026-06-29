using UnityEngine;

namespace Puzzle_Elements.Elevador
{
    public class ElevatorPlatform : MonoBehaviour
    {
        [Header("Elevator (Rigidbody)")]
        public Rigidbody elevatorRb;
        public Transform elevatorWP1;
        public Transform elevatorWP2;

        [Tooltip("Max travel speed (units/sec).")]
        public float elevatorSpeed = 2f;

        [Tooltip("Acceleration used to ramp up/down (units/sec^2).")]
        public float elevatorAcceleration = 20f;

        [Tooltip("Seconds to wait at each end.")]
        public float elevatorWaitSeconds = 2f;

        [Tooltip("How close is considered 'arrived' to a waypoint.")]
        public float elevatorArriveThreshold = 0.02f;

        [Tooltip("If true, starts moving automatically on Enable.")]
        public bool autoStart = true;

        // Resolved waypoints (low/high by Y)
        private Transform elevatorLow;
        private Transform elevatorHigh;

        // State
        private enum ElevatorState { Idle, ToA, WaitAtA, ToB, WaitAtB }
        private ElevatorState elevState = ElevatorState.Idle;
        private Vector3 elevTarget;
        private float elevCurrentSpeed;
        private float elevWaitUntil;
        private bool elevConfigured;
        private bool systemEnabled;

        void Awake()
        {
            ResolveHeights();
            PrepareElevator();
            EnsureKinematic();
        }

        void OnEnable()
        {
            ResolveHeights();
            PrepareElevator();
            EnsureKinematic();
            if (autoStart) StartElevator();
        }

        void OnDisable()
        {
            StopElevator();
        }

        void OnValidate()
        {
            EnsureKinematic();
            if (elevatorArriveThreshold < 0f) elevatorArriveThreshold = 0f;
            if (elevatorSpeed < 0f) elevatorSpeed = 0f;
            if (elevatorAcceleration < 0f) elevatorAcceleration = 0f;
            if (elevatorWaitSeconds < 0f) elevatorWaitSeconds = 0f;
        }

        public void StartElevator()
        {
            systemEnabled = true;
            ResolveHeights();
            PrepareElevator(true);
            EnsureKinematic();
        }

        public void StopElevator()
        {
            systemEnabled = false;
            elevState = ElevatorState.Idle;
            // No velocity/angularVelocity touches; kinematic uses MovePosition only.
        }

        void FixedUpdate()
        {
            elevConfigured = IsElevatorConfigured();
            if (!elevConfigured) return;

            // Force kinematic usage at runtime as well.
            EnsureKinematic();

            if (!systemEnabled) return;

            TickElevator();
        }

        private void ResolveHeights()
        {
            if (elevatorWP1 != null && elevatorWP2 != null)
            {
                if (elevatorWP1.position.y <= elevatorWP2.position.y)
                {
                    elevatorLow = elevatorWP1;
                    elevatorHigh = elevatorWP2;
                }
                else
                {
                    elevatorLow = elevatorWP2;
                    elevatorHigh = elevatorWP1;
                }
            }
            else
            {
                elevatorLow = null;
                elevatorHigh = null;
            }
        }

        private bool IsElevatorConfigured() =>
            elevatorRb != null && elevatorLow != null && elevatorHigh != null;

        private void PrepareElevator(bool snapIfNeeded = false)
        {
            elevCurrentSpeed = 0f;

            if (IsElevatorConfigured())
            {
                Vector3 p = elevatorRb.position;
                float dLow = Vector3.Distance(p, elevatorLow.position);
                float dHigh = Vector3.Distance(p, elevatorHigh.position);

                // Start toward the farther point so it bounces between ends
                Transform first = (dLow <= dHigh) ? elevatorHigh : elevatorLow;
                elevTarget = first.position;
                elevState = ElevatorState.ToA;
                elevWaitUntil = 0f;

                if (snapIfNeeded)
                {
                    // Optional: align to current (do nothing). Kept intentionally.
                }
            }
            else
            {
                elevState = ElevatorState.Idle;
            }
        }

        private void TickElevator()
        {
            switch (elevState)
            {
                case ElevatorState.ToA:
                {
                    bool reached = MoveElevatorAcc(elevTarget);
                    if (reached)
                    {
                        elevCurrentSpeed = 0f;
                        elevWaitUntil = Time.fixedTime + elevatorWaitSeconds;
                        elevState = ElevatorState.WaitAtA;
                    }
                    break;
                }
                case ElevatorState.WaitAtA:
                {
                    if (Time.fixedTime >= elevWaitUntil)
                    {
                        elevTarget = IsNear(elevTarget, elevatorHigh.position, 1e-4f)
                            ? elevatorLow.position
                            : elevatorHigh.position;
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
                        elevWaitUntil = Time.fixedTime + elevatorWaitSeconds;
                        elevState = ElevatorState.WaitAtB;
                    }
                    break;
                }
                case ElevatorState.WaitAtB:
                {
                    if (Time.fixedTime >= elevWaitUntil)
                    {
                        elevTarget = IsNear(elevTarget, elevatorHigh.position, 1e-4f)
                            ? elevatorLow.position
                            : elevatorHigh.position;
                        elevState = ElevatorState.ToA;
                    }
                    break;
                }
            }
        }

        // Kinematic-friendly mover using only MovePosition
        private bool MoveElevatorAcc(Vector3 targetPos)
        {
            Vector3 current = elevatorRb.position;
            Vector3 toTarget = targetPos - current;
            float distance = toTarget.magnitude;

            if (distance <= elevatorArriveThreshold)
            {
                elevatorRb.MovePosition(targetPos);
                return true;
            }

            float maxSpeed = Mathf.Max(0f, elevatorSpeed);
            float accel = Mathf.Max(0f, elevatorAcceleration);

            // Speed cap so we can stop in remaining distance: v <= sqrt(2*a*d)
            float maxSpeedForStop = Mathf.Sqrt(Mathf.Max(0f, 2f * accel * distance));
            float desiredSpeed = Mathf.Min(maxSpeed, maxSpeedForStop);

            elevCurrentSpeed = Mathf.MoveTowards(elevCurrentSpeed, desiredSpeed, accel * Time.fixedDeltaTime);

            float step = elevCurrentSpeed * Time.fixedDeltaTime;
            Vector3 dir = (distance > 1e-5f) ? (toTarget / distance) : Vector3.zero;
            Vector3 next = (step >= distance) ? targetPos : current + dir * step;

            elevatorRb.MovePosition(next);
            return false;
        }

        private static bool IsNear(Vector3 a, Vector3 b, float eps) =>
            (a - b).sqrMagnitude <= eps * eps;

        private void EnsureKinematic()
        {
            if (elevatorRb == null) return;
            if (!elevatorRb.isKinematic)
            {
                elevatorRb.isKinematic = true;
            }
            // For visual smoothness when camera is not in fixed time:
            if (elevatorRb.interpolation == RigidbodyInterpolation.None)
                elevatorRb.interpolation = RigidbodyInterpolation.Interpolate;
            // Continuous Speculative works well for kinematic movers:
            if (elevatorRb.collisionDetectionMode == CollisionDetectionMode.Discrete)
                elevatorRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (elevatorWP1 != null && elevatorWP2 != null)
            {
                Transform low = (elevatorWP1.position.y <= elevatorWP2.position.y) ? elevatorWP1 : elevatorWP2;
                Transform high = (low == elevatorWP1) ? elevatorWP2 : elevatorWP1;

                Gizmos.color = Color.cyan;   // low
                Gizmos.DrawSphere(low.position, 0.08f);

                Gizmos.color = Color.magenta; // high
                Gizmos.DrawCube(high.position, Vector3.one * 0.14f);
            }
        }
#endif
    }
}
