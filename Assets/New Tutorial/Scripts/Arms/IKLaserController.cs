using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Animations.Rigging;

namespace Puzzle_Elements.IK.Scripts
{
    /// <summary>
    /// Control manual del Target de un Chain IK Constraint.
    /// El Animator debe usar actualización Normal. El Target no debe estar
    /// dentro de la cadena de huesos ni tener otro controlador de movimiento.
    /// </summary>
    [DisallowMultipleComponent]
    public class IKLaserController : MonoBehaviour
    {
        [Header("IK")]
        [SerializeField] private Rig rig;
        [SerializeField] private Transform tip;
        [SerializeField] private Transform target;
        [Tooltip("Hijo del Tip, ubicado en la boca del cañón. Su eje Z azul apunta hacia el disparo.")]
        [SerializeField] private Transform muzzle;

        [Header("Movimiento del Target")]
        [Min(0f)] [SerializeField] private float moveDuration = 1f;
        [Min(0f)] [SerializeField] private float aimSpeed = 180f;
        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Láser")]
        [SerializeField] private LineRenderer laserLine;
        [Min(0.01f)] [SerializeField] private float range = 100f;
        [Min(0.01f)] [SerializeField] private float shotDuration = 0.15f;
        [Tooltip("Excluir las capas del arma para que no se impacte a sí misma.")]
        [SerializeField] private LayerMask hitMask = ~0;
        [Min(0f)] [SerializeField] private float laserWidth = 0.03f;

        [Header("Eventos")]
        [SerializeField] private UnityEvent onMovementCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onFireStarted = new UnityEvent();
        [SerializeField] private UnityEvent onFireStopped = new UnityEvent();

        // Se emite cada frame de disparo que encuentra un collider.
        // deltaTime permite aplicar daño por segundo sin depender de los FPS.
        public event Action<RaycastHit, float> OnLaserHit;
        public bool IsMoving { get; private set; }
        public bool IsFiring { get; private set; }
        public bool HasHit { get; private set; }
        public RaycastHit LastHit { get; private set; }

        private bool initialized;
        private bool aiming;
        private Transform followedObject;
        private Vector3 aimPoint;
        private Quaternion tipToMuzzle;
        private Transform initialParent;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 moveStartPosition;
        private Quaternion moveStartRotation;
        private Vector3 destinationPosition;
        private Quaternion destinationRotation;
        private float movementTime;
        private float movementDuration;
        private bool continuousFire;
        private float shotTimeRemaining;

        private void Awake()
        {
            Initialize();
        }

        private bool Initialize()
        {
            if (initialized) return true;
            if (!rig || !tip || !target || !muzzle)
            {
                Debug.LogError("IKLaserController: asigná Rig, Tip, Target y Muzzle.", this);
                return false;
            }

            initialParent = target.parent;
            initialPosition = target.localPosition;
            initialRotation = target.localRotation;
            tipToMuzzle = Quaternion.Inverse(tip.rotation) * muzzle.rotation;
            if (laserLine)
            {
                laserLine.enabled = false;
                laserLine.useWorldSpace = true;
                laserLine.loop = false;
                laserLine.positionCount = 2;
                laserLine.startWidth = laserWidth;
                laserLine.endWidth = laserWidth;
            }
            initialized = true;
            return true;
        }

        private void Update()
        {
            if (!initialized) return;

            if (IsMoving)
            {
                movementTime += Time.deltaTime;
                float t = Mathf.Clamp01(movementTime / movementDuration);
                float k = moveCurve == null ? t : moveCurve.Evaluate(t);
                target.SetPositionAndRotation(
                    Vector3.LerpUnclamped(moveStartPosition, destinationPosition, k),
                    Quaternion.SlerpUnclamped(moveStartRotation, destinationRotation, k));
                if (t >= 1f)
                {
                    target.SetPositionAndRotation(destinationPosition, destinationRotation);
                    IsMoving = false;
                    onMovementCompleted.Invoke();
                }
            }

            if (!aiming) return;
            if (followedObject) aimPoint = followedObject.position;
            Vector3 direction = aimPoint - muzzle.position;
            if (direction.sqrMagnitude < 0.000001f) return;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.999f
                ? Vector3.forward : Vector3.up;
            Quaternion desiredRotation = Quaternion.LookRotation(direction, up)
                * Quaternion.Inverse(tipToMuzzle);
            target.rotation = Quaternion.RotateTowards(target.rotation, desiredRotation,
                aimSpeed * Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!initialized || !IsFiring || Time.deltaTime <= 0f) return;
            // Después de la evaluación normal del Animator/Animation Rigging.
            Vector3 origin = muzzle.position;
            Vector3 direction = muzzle.forward;
            HasHit = Physics.Raycast(origin, direction, out RaycastHit hit,
                range, hitMask, QueryTriggerInteraction.Ignore);
            LastHit = hit;
            if (laserLine)
            {
                laserLine.enabled = true;
                laserLine.SetPosition(0, origin);
                laserLine.SetPosition(1, HasHit ? hit.point : origin + direction * range);
            }

            float activeTime = continuousFire ? Time.deltaTime
                : Mathf.Min(Time.deltaTime, shotTimeRemaining);
            bool endShot = false;
            if (!continuousFire)
            {
                shotTimeRemaining -= Time.deltaTime;
                endShot = shotTimeRemaining <= 0f;
            }
            if (HasHit) OnLaserHit?.Invoke(hit, activeTime);
            if (endShot && !continuousFire && shotTimeRemaining <= 0f) StopFire();
        }

        // Copia la posición y rotación del destino al recibir la orden.
        public void MoveTo(Transform destination)
        {
            if (destination) MoveTo(destination.position, destination.rotation, moveDuration);
        }

        public void MoveTo(Vector3 position, float duration)
        {
            if (Initialize()) MoveTo(position, target.rotation, duration);
        }

        public void MoveTo(Vector3 position, Quaternion rotation, float duration)
        {
            if (!isActiveAndEnabled || !Initialize()) return;
            StopMovement();
            destinationPosition = position;
            destinationRotation = rotation;
            if (duration <= 0f)
            {
                target.SetPositionAndRotation(position, rotation);
                onMovementCompleted.Invoke();
                return;
            }
            moveStartPosition = target.position;
            moveStartRotation = target.rotation;
            movementDuration = duration;
            movementTime = 0f;
            IsMoving = true;
        }

        // Conserva la posición del Target y cambia su orientación.
        public void AimAt(Vector3 worldPoint)
        {
            if (!isActiveAndEnabled || !Initialize()) return;
            StopMovement();
            aimPoint = worldPoint;
            aiming = true;
        }

        public void AimAt(Transform destination)
        {
            if (destination) AimAt(destination.position);
        }

        public void FollowTarget(Transform destination)
        {
            if (!destination || !isActiveAndEnabled || !Initialize()) return;
            AimAt(destination.position);
            followedObject = destination;
        }

        public void StopMovement()
        {
            IsMoving = false;
            aiming = false;
            followedObject = null;
        }

        public void ReturnToStart()
        {
            if (!Initialize()) return;
            Vector3 position = initialParent ? initialParent.TransformPoint(initialPosition) : initialPosition;
            Quaternion rotation = initialParent ? initialParent.rotation * initialRotation : initialRotation;
            MoveTo(position, rotation, moveDuration);
        }

        public void SetIKWeight(float weight)
        {
            if (Initialize()) rig.weight = Mathf.Clamp01(weight);
        }

        public void Fire()
        {
            if (!isActiveAndEnabled || !Initialize()) return;
            continuousFire = false;
            shotTimeRemaining = Mathf.Max(0.01f, shotDuration);
            BeginFire();
        }

        public void StartFire()
        {
            if (!isActiveAndEnabled || !Initialize()) return;
            continuousFire = true;
            BeginFire();
        }

        private void BeginFire()
        {
            if (IsFiring) return;
            IsFiring = true;
            onFireStarted.Invoke();
        }

        public void StopFire()
        {
            bool wasFiring = IsFiring;
            IsFiring = false;
            continuousFire = false;
            HasHit = false;
            LastHit = default;
            if (laserLine) laserLine.enabled = false;
            if (wasFiring) onFireStopped.Invoke();
        }

        private void OnDisable()
        {
            StopMovement();
            StopFire();
        }
    }
}
