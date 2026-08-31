using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SequenceController : MonoBehaviour
{
    // =========================================================
    // CONFIGURACIÓN
    // =========================================================

    [Header("Sequence")]
    [SerializeReference]
    private List<SequenceAction> actions = new();

    [Header("Settings")]
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private bool loop = false;

    [Tooltip("Índice desde el que vuelve a empezar cuando Loop está activo.")]
    [SerializeField] private int loopFromIndex = 0;

    private Coroutine sequenceCoroutine;

    public bool IsPlaying => sequenceCoroutine != null;

    private IEnumerator ExecuteAction(
    SequenceAction action,
    Action onFinished
)
    {
        yield return action.Execute(this);

        onFinished?.Invoke();
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void Start()
    {
        if (playOnStart)
            Play();
    }


    // =========================================================
    // CONTROL
    // =========================================================

    public void Play()
    {
        if (sequenceCoroutine != null)
            return;

        sequenceCoroutine = StartCoroutine(RunSequence());
    }

    public void Stop()
    {
        StopAllCoroutines();
        sequenceCoroutine = null;
    }

    public void Restart()
    {
        Stop();
        Play();
    }


    // =========================================================
    // SEQUENCE
    // =========================================================

    private IEnumerator RunSequence()
    {
        if (actions == null || actions.Count == 0)
        {
            sequenceCoroutine = null;
            yield break;
        }

        int index = 0;

        while (true)
        {
            if (index >= actions.Count)
            {
                if (!loop)
                    break;

                index = Mathf.Clamp(
                    loopFromIndex,
                    0,
                    actions.Count - 1
                );
            }

            SequenceAction action = actions[index];

            if (action != null)
            {
                bool actionFinished = false;

                StartCoroutine(
                    ExecuteAction(
                        action,
                        () => actionFinished = true
                    )
                );

                // ==========================================
                // CUÁNDO PUEDE EMPEZAR LA SIGUIENTE
                // ==========================================

                float normalizedTime =
                    Mathf.Clamp01(action.nextActionTime);

                // 1 significa:
                // esperar REALMENTE a que termine.
                if (normalizedTime >= 1f)
                {
                    while (!actionFinished)
                    {
                        yield return null;
                    }
                }
                else
                {
                    float delay =
                        action.Duration *
                        normalizedTime;

                    if (delay > 0f)
                    {
                        yield return new WaitForSeconds(delay);
                    }

                    // Si delay == 0 no hacemos yield.
                    // La siguiente acción puede comenzar
                    // en este mismo frame.
                }
            }

            index++;
        }

        sequenceCoroutine = null;
    }


    // =========================================================
    // BASE ACTION
    // =========================================================

    [Serializable]
    public abstract class SequenceAction
    {
        [SerializeField]
        private string name;

        public string Name => name;

        [Header("Sequence Timing")]
        [Range(0f, 1f)]
        [Tooltip(
            "0 = la siguiente acción empieza inmediatamente. " +
            "1 = espera a que esta acción termine."
        )]
        public float nextActionTime = 1f;

        /// <summary>
        /// Duración estimada de esta acción.
        /// Las acciones instantáneas devuelven 0.
        /// </summary>
        public virtual float Duration => 0f;

        public abstract IEnumerator Execute(
            SequenceController sequence
        );
    }

    // =========================================================
    // WAIT
    // =========================================================

    [Serializable]
    public class WaitAction : SequenceAction
    {
        [Min(0)]
        public float seconds = 1f;
        public override float Duration => seconds;
        public override IEnumerator Execute(
            SequenceController sequence
        )
        {
            yield return new WaitForSeconds(seconds);
        }
    }


    // =========================================================
    // UNITY EVENT
    // =========================================================

    [Serializable]
    public class InvokeEventAction : SequenceAction
    {
        public UnityEvent onExecute;

        public override IEnumerator Execute(
            SequenceController sequence
        )
        {
            onExecute?.Invoke();

            yield break;
        }
    }


    // =========================================================
    // SET ACTIVE
    // =========================================================

    [Serializable]
    public class SetActiveAction : SequenceAction
    {
        public GameObject target;

        public bool active = true;

        public override IEnumerator Execute(
            SequenceController sequence
        )
        {
            if (target != null)
                target.SetActive(active);

            yield break;
        }
    }


    // =========================================================
    // MOVE
    // =========================================================

    [Serializable]
    public class MoveAction : SequenceAction
    {
        public Transform target;
        public Transform destination;

        public Vector3 offset;
        public bool canRotate;

        [Min(0.01f)]
        public float duration = 1f;
        public override float Duration => duration;
        public AnimationCurve movementCurve =
            AnimationCurve.EaseInOut(
                0,
                0,
                1,
                1
            );

        public override IEnumerator Execute(
            SequenceController sequence
        )
        {
            if (target == null || destination == null)
                yield break;

            Vector3 startPosition = target.position;
            Quaternion startRotation = target.rotation;

            Vector3 endPosition =
                destination.position + offset;

            Quaternion endRotation =
                destination.rotation;

            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;

                float t = Mathf.Clamp01(
                    time / duration
                );

                float curvedT =
                    movementCurve.Evaluate(t);

                target.position = Vector3.Lerp(
                    startPosition,
                    endPosition,
                    curvedT
                );

                if (canRotate)
                {
                    target.rotation = Quaternion.Slerp(
                        startRotation,
                        endRotation,
                        curvedT
                    );
                }

                yield return null;
            }

            target.position = endPosition;

            if (canRotate)
            {
                target.rotation = endRotation;
            }
        }
    }


    // =========================================================
    // SHOOT
    // =========================================================

    [Serializable]
    public class ShootAction : SequenceAction
    {
        public Cannon cannon;

        public float force = 10f;

        public int damage = 10;

        public override IEnumerator Execute(
            SequenceController sequence
        )
        {
            if (cannon != null)
            {
                cannon.Shoot(
                    force,
                    damage
                );
            }

            yield break;
        }
    }
    // =========================================================
    // PARABOLIC MOVE
    // =========================================================

    [Serializable]
    public class ParabolicMoveAction : SequenceAction
    {
        [Header("References")]
        public Transform target;
        public Transform destination;

        [Header("Movement")]
        public Vector3 offset;

        [Min(0.01f)]
        public float duration = 1f;
        public override float Duration => duration;
        [Min(0f)]
        public float height = 2f;

        [Header("Rotation")]
        public bool canRotate = false;

        [Tooltip("Hace que la rotación tome el camino contrario.")]
        public bool oppositeRotationDirection = false;

        public AnimationCurve movementCurve =
            AnimationCurve.EaseInOut(
                0,
                0,
                1,
                1
            );

        public override IEnumerator Execute(
            SequenceController sequence
        )
        {
            if (target == null || destination == null)
                yield break;

            Vector3 startPosition = target.position;
            Quaternion startRotation = target.rotation;

            Vector3 endPosition =
                destination.position + offset;

            Quaternion endRotation =
                destination.rotation;

            // ==========================================
            // CALCULAR ROTACIÓN
            // ==========================================

            Quaternion relativeRotation =
                Quaternion.Inverse(startRotation) *
                endRotation;

            relativeRotation.ToAngleAxis(
                out float rotationAngle,
                out Vector3 rotationAxis
            );

            // Quaternion puede devolver un eje inválido
            // cuando prácticamente no hay rotación.
            if (rotationAxis.sqrMagnitude < 0.0001f)
            {
                rotationAxis = Vector3.up;
                rotationAngle = 0f;
            }

            // Camino contrario
            if (oppositeRotationDirection)
            {
                rotationAngle -= 360f;
            }

            // ==========================================
            // MOVIMIENTO
            // ==========================================

            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;

                float t = Mathf.Clamp01(
                    time / duration
                );

                float curvedT =
                    movementCurve.Evaluate(t);

                // Movimiento hacia el destino
                Vector3 position = Vector3.Lerp(
                    startPosition,
                    endPosition,
                    curvedT
                );

                // Parábola
                float parabola =
                    4f * curvedT * (1f - curvedT);

                position +=
                    Vector3.up *
                    parabola *
                    height;

                target.position = position;

                // ======================================
                // ROTACIÓN
                // ======================================

                if (canRotate)
                {
                    Quaternion rotationDelta =
                        Quaternion.AngleAxis(
                            rotationAngle * curvedT,
                            rotationAxis
                        );

                    target.rotation =
                        startRotation *
                        rotationDelta;
                }

                yield return null;
            }

            // Aseguramos posición final
            target.position = endPosition;

            // Aseguramos rotación final exacta
            if (canRotate)
            {
                target.rotation = endRotation;
            }
        }
    }
    /// <summary>
    /// Grab
    /// </summary>
    [Serializable]
    public class GrabAction : SequenceAction
    {
        [Header("References")]
        public Transform objectToGrab;
        public Transform grabPoint;

        [Header("Settings")]
        public bool snapToGrabPoint = true;

        public bool disableRigidbodyPhysics = true;

        public override IEnumerator Execute(
            SequenceController sequence
        )
        {
            if (objectToGrab == null || grabPoint == null)
                yield break;

            // Lo hacemos hijo de la garra
            objectToGrab.SetParent(grabPoint);

            // Lo acomodamos exactamente en el punto de agarre
            if (snapToGrabPoint)
            {
                objectToGrab.localPosition = Vector3.zero;
                objectToGrab.localRotation = Quaternion.identity;
            }

            // Si tiene Rigidbody, evitamos que la física pelee
            // contra el movimiento de la garra
            if (disableRigidbodyPhysics)
            {
                Rigidbody rb =
                    objectToGrab.GetComponent<Rigidbody>();

                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;

                    rb.isKinematic = true;
                }
            }

            yield break;
        }
    }
    /// <summary>
    /// Look At
    /// </summary>
    [Serializable]
    public class LookAtAction : SequenceAction
    {
        [Header("References")]
        public Transform target;
        public Transform lookAtTarget;

        [Header("Rotation")]
        public bool instant = false;

        [Min(0.01f)]
        public float duration = 1f;

        public override float Duration =>    instant ? 0f : duration;
        public AnimationCurve rotationCurve =
            AnimationCurve.EaseInOut(
                0,
                0,
                1,
                1
            );

        public override IEnumerator Execute(
            SequenceController sequence
        )
        {
            if (target == null || lookAtTarget == null)
                yield break;

            Vector3 direction =
                lookAtTarget.position - target.position;

            if (direction.sqrMagnitude <= 0.0001f)
                yield break;

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up
                );

            // Rotación instantánea
            if (instant)
            {
                target.rotation = targetRotation;
                yield break;
            }

            Quaternion startRotation =
                target.rotation;

            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;

                float t =
                    Mathf.Clamp01(
                        time / duration
                    );

                float curvedT =
                    rotationCurve.Evaluate(t);

                // Recalculamos por si el objetivo se mueve
                direction =
                    lookAtTarget.position - target.position;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    targetRotation =
                        Quaternion.LookRotation(
                            direction.normalized,
                            Vector3.up
                        );
                }

                target.rotation =
                    Quaternion.Slerp(
                        startRotation,
                        targetRotation,
                        curvedT
                    );

                yield return null;
            }

            direction =
                lookAtTarget.position - target.position;

            if (direction.sqrMagnitude > 0.0001f)
            {
                target.rotation =
                    Quaternion.LookRotation(
                        direction.normalized,
                        Vector3.up
                    );
            }
        }
    }
    // =========================================================
    // CLAW
    // =========================================================

    [Serializable]
    public class ClawAction : SequenceAction
    {
        public Claw claw;

        public Transform destination;

        public float speed = 2f;

        public bool grabObject;

        public int objectID;

        public override float Duration
        {
            get
            {
                if (claw == null || destination == null || speed <= 0f)
                    return 0f;

                float distance = Vector3.Distance(
                    claw.transform.position,
                    destination.position
                );

                return distance / speed;
            }
        }

        public override IEnumerator Execute(
            SequenceController sequence
        )
        {
            if (claw != null)
            {
                claw.Move(
                    destination,
                    speed,
                    grabObject,
                    objectID
                );
            }

            // Esperamos el tiempo que debería tardar el movimiento
            yield return new WaitForSeconds(Duration);
        }
    }
    /// <summary>
    /// Release
    /// </summary>
    [Serializable]
    public class ReleaseAction : SequenceAction
    {
        [Header("References")]
        public Transform objectToRelease;

        [Header("Settings")]
        public bool enableRigidbodyPhysics = true;

        public override IEnumerator Execute(
            SequenceController sequence
        )
        {
            if (objectToRelease == null)
                yield break;

            // Lo soltamos de la garra
            objectToRelease.SetParent(null);

            // Reactivamos la física
            if (enableRigidbodyPhysics)
            {
                Rigidbody rb =
                    objectToRelease.GetComponent<Rigidbody>();

                if (rb != null)
                {
                    rb.isKinematic = false;

                    // Por seguridad, parte sin velocidades heredadas
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            yield break;
        }
    }
}