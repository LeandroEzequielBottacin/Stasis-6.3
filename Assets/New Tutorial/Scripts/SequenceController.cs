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
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }
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
                yield return action.Execute(this);
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

            yield break;
        }
    }
}