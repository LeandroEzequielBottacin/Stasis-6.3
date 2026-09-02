using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Animations.Rigging;

namespace Puzzle_Elements.IK.Scripts
{
    /// <summary>
    /// Control manual del Target de un Chain IK Constraint.
    /// El Animator debe usar actualizacion Normal. El Target no debe estar
    /// dentro de la cadena de huesos ni tener otro controlador de movimiento.
    /// </summary>
    [DisallowMultipleComponent]
    public class IKLaserController : MonoBehaviour
    {
        [Header("IK")]
        [Tooltip("Rig de Animation Rigging cuyo peso se modifica con SetIKWeight. Asigna el Rig que contiene la restriccion IK del arma.")]
        [SerializeField] private Rig rig;
        [Tooltip("Ultimo hueso de la cadena IK. Su rotacion se usa para calcular la orientacion relativa de la boca del canon.")]
        [SerializeField] private Transform tip;
        [Tooltip("Target de la restriccion Chain IK. Este controlador modifica su posicion y rotacion para mover y orientar el arma.")]
        [SerializeField] private Transform target;
        [Tooltip("Hijo del Tip, ubicado en la boca del canon. Su eje Z azul apunta hacia el disparo.")]
        [SerializeField] private Transform muzzle;

        [Header("Movimiento del Target")]
        [Tooltip("Duracion predeterminada del movimiento y del regreso a la pose inicial, en segundos. Con 0, el movimiento es inmediato.")]
        [Min(0f)] [SerializeField] private float moveDuration = 1f;
        [Tooltip("Velocidad maxima de giro al apuntar o seguir un objetivo, en grados por segundo. Con 0, no gira.")]
        [Min(0f)] [SerializeField] private float aimSpeed = 180f;
        [Tooltip("Curva de progreso del movimiento: X es el tiempo normalizado y Y la interpolacion de posicion y rotacion. Valores fuera de 0 a 1 pueden sobrepasar el destino.")]
        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Laser")]
        [Tooltip("LineRenderer opcional que dibuja el laser desde Muzzle hasta el impacto o el alcance maximo.")]
        [SerializeField] private LineRenderer laserLine;
        [Tooltip("Alcance maximo del raycast y del laser cuando no hay impacto, en unidades de mundo.")]
        [Min(0.01f)] [SerializeField] private float range = 100f;
        [Tooltip("Duracion del disparo individual iniciado con Fire, en segundos. No limita el disparo continuo de StartFire.")]
        [Min(0.01f)] [SerializeField] private float shotDuration = 0.15f;
        [Tooltip("Excluir las capas del arma para que no se impacte a si misma.")]
        [SerializeField] private LayerMask hitMask = ~0;
        [Tooltip("Grosor uniforme del LineRenderer del laser, aplicado durante la inicializacion.")]
        [Min(0f)] [SerializeField] private float laserWidth = 0.03f;

        [Header("Eventos")]
        [Tooltip("Evento ejecutado al llegar al destino, tambien cuando el movimiento es inmediato.")]
        [SerializeField] private UnityEvent onMovementCompleted = new UnityEvent();
        [Tooltip("Evento ejecutado cuando el laser pasa de estar apagado a disparar.")]
        [SerializeField] private UnityEvent onFireStarted = new UnityEvent();
        [Tooltip("Evento ejecutado cuando se detiene un disparo que estaba activo.")]
        [SerializeField] private UnityEvent onFireStopped = new UnityEvent();

        // Se emite cada frame de disparo que encuentra un collider.
        // deltaTime permite aplicar dano por segundo sin depender de los FPS.
        public event Action<RaycastHit, float> OnLaserHit;
        public bool IsMoving { get; private set; }
        public bool IsFiring { get; private set; }
        public bool HasHit { get; private set; }
        public RaycastHit LastHit { get; private set; }

        [Tooltip("Estado interno: indica que las referencias y los recursos ya fueron inicializados.")]
        private bool initialized;
        [Tooltip("Estado interno: permite orientar el Target hacia el punto de apuntado en cada Update.")]
        private bool aiming;
        [Tooltip("Referencia interna al objeto seguido. Su posicion actualiza el punto de apuntado cada frame.")]
        private Transform followedObject;
        [Tooltip("Punto de apuntado interno, expresado en coordenadas de mundo.")]
        private Vector3 aimPoint;
        [Tooltip("Rotacion relativa inicial entre Tip y Muzzle, usada para orientar correctamente la boca del canon.")]
        private Quaternion tipToMuzzle;
        [Tooltip("Padre original del Target, utilizado para reconstruir su pose inicial al regresar.")]
        private Transform initialParent;
        [Tooltip("Posicion local inicial del Target respecto de su padre original.")]
        private Vector3 initialPosition;
        [Tooltip("Rotacion local inicial del Target respecto de su padre original.")]
        private Quaternion initialRotation;
        [Tooltip("Posicion mundial del Target al comenzar el movimiento actual.")]
        private Vector3 moveStartPosition;
        [Tooltip("Rotacion mundial del Target al comenzar el movimiento actual.")]
        private Quaternion moveStartRotation;
        [Tooltip("Posicion mundial de destino almacenada para el movimiento actual.")]
        private Vector3 destinationPosition;
        [Tooltip("Rotacion mundial de destino almacenada para el movimiento actual.")]
        private Quaternion destinationRotation;
        [Tooltip("Tiempo transcurrido del movimiento actual, en segundos.")]
        private float movementTime;
        [Tooltip("Duracion efectiva del movimiento actual, en segundos; puede diferir de Move Duration cuando se indica por codigo.")]
        private float movementDuration;
        [Tooltip("Estado interno: mantiene el disparo activo hasta llamar a StopFire, sin descontar la duracion del disparo individual.")]
        private bool continuousFire;
        [Tooltip("Tiempo restante del disparo individual, en segundos.")]
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
                Debug.LogError("IKLaserController: asigna Rig, Tip, Target y Muzzle.", this);
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
            // Despues de la evaluacion normal del Animator/Animation Rigging.
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

        // Copia la posicion y rotacion del destino al recibir la orden.
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

        // Conserva la posicion del Target y cambia su orientacion.
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
