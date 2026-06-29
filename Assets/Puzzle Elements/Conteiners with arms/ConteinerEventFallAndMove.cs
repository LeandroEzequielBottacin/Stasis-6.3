using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConteinerEventFallAndMove : MonoBehaviour
{
    [Header("=== REFERENCIAS ===")]
    [Tooltip("Rigidbody del contenedor que va a moverse.")]
    [SerializeField] private Rigidbody rb;

    [Tooltip("Waypoint FINAL al que debe moverse al final de la secuencia.")]
    [SerializeField] private Transform waypoint;

    [Tooltip("Waypoint de piso al que debe ir primero al disparar el evento.")]
    [SerializeField] private Transform waypointPiso;

    [Header("=== MOVIMIENTO HORIZONTAL ===")]
    [Tooltip("Velocidad con la que el contenedor se mueve hacia los waypoints.")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("Distancia m�xima para considerar que 'lleg�' a un waypoint.")]
    [SerializeField] private float stopDistance = 0.05f;

    [Header("=== TIEMPOS ===")]
    [Tooltip("Tiempo que espera en el waypointPiso antes de ir al waypoint final.")]
    [SerializeField] private float waitAtFloorSeconds = 1f;

    [Header("=== L�SERES A DESACTIVAR AL LLEGAR AL FINAL ===")]
    public List<GameObject> lasers = new List<GameObject>();
    public List<BoxCollider> colliders = new List<BoxCollider>();

    // Estados internos
    private bool _sequenceStarted;
    private bool _isMoving;
    private bool _movingToPiso;
    private bool _movingToFinal;
    private bool _waitingAtFloor;

    private Coroutine _waitCoroutine;

    private void Awake()
    {
        if (!rb)
            rb = GetComponent<Rigidbody>();

        if (rb)
        {
            // SIEMPRE KINEMATIC, sin gravedad del motor.
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    private void Update()
    {
        // Tecla de debug opcional para disparar la secuencia
        if (Input.GetKeyDown(KeyCode.O))
        {
            TriggerFallAndMove();
        }
    }

    private void FixedUpdate()
    {
        if (_isMoving)
        {
            MoveTowardsCurrentTarget();
        }
    }

    /// <summary>
    /// M�TODO QUE LLAMA EL EVENTO EXTERNO.
    /// Al llamarlo: primero va a waypointPiso, luego de 1s a waypoint.
    /// </summary>
    public void TriggerFallAndMove()
    {
        if (_sequenceStarted) return; // Evita m�ltiples disparos

        if (!rb)
        {
            Debug.LogWarning("[ConteinerEventFallAndMove] No hay Rigidbody asignado.");
            return;
        }

        _sequenceStarted = true;

        // Decidimos el primer objetivo
        if (waypointPiso != null)
        {
            _movingToPiso = true;
            _movingToFinal = false;
        }
        else
        {
            // Si no hay waypointPiso, vamos directo al final
            _movingToPiso = false;
            _movingToFinal = true;
        }

        _isMoving = true;
    }

    /// <summary>
    /// Mueve el contenedor hacia el objetivo actual (piso o final)
    /// usando MovePosition con rigidbody kinematic.
    /// </summary>
    private void MoveTowardsCurrentTarget()
    {
        if (!rb) return;

        Transform targetTransform = null;

        if (_movingToPiso && waypointPiso != null)
        {
            targetTransform = waypointPiso;
        }
        else if (_movingToFinal && waypoint != null)
        {
            targetTransform = waypoint;
        }
        else
        {
            _isMoving = false;
            return;
        }

        Vector3 current = rb.position;
        Vector3 target = targetTransform.position;

        // Movimiento suave hacia el target
        Vector3 next = Vector3.MoveTowards(current, target, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        float distance = Vector3.Distance(next, target);
        if (distance <= stopDistance)
        {
            // Lleg� al waypoint actual
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (_movingToPiso)
            {
                // Termin� de ir al piso
                _movingToPiso = false;
                _isMoving = false;

                // Espera en el piso y luego va al final
                if (!_waitingAtFloor)
                {
                    _waitingAtFloor = true;
                    _waitCoroutine = StartCoroutine(WaitThenMoveToFinal());
                }
            }
            else if (_movingToFinal)
            {
                // Lleg� al destino final
                _movingToFinal = false;
                _isMoving = false;

                DesactivateLasers();
            }
        }
    }

    /// <summary>
    /// Espera X segundos en el waypointPiso y luego empieza a moverse al waypoint final.
    /// </summary>
    private IEnumerator WaitThenMoveToFinal()
    {
        yield return new WaitForSeconds(waitAtFloorSeconds);

        _waitingAtFloor = false;

        if (waypoint != null)
        {
            _movingToFinal = true;
            _isMoving = true;
        }
        else
        {
            Debug.LogWarning("[ConteinerEventFallAndMove] No hay waypoint final asignado.");
        }

        _waitCoroutine = null;
    }

    private void DesactivateLasers()
    {
        foreach (var item in lasers)
        {
            if (item != null)
                item.SetActive(false);
        }
        foreach (var item in colliders)
        {
            item.enabled = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Gizmo waypoint final
        if (waypoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, waypoint.position);
            Gizmos.DrawSphere(waypoint.position, 0.1f);
        }

        // Gizmo waypoint piso
        if (waypointPiso)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, waypointPiso.position);
            Gizmos.DrawSphere(waypointPiso.position, 0.1f);
        }
    }
}
