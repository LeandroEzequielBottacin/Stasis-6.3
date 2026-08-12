using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HeavyElevatorTrigger : MonoBehaviour
{
    [Header("=== ELEVADOR ===")]
    [Tooltip("Rigidbody del elevador (la plataforma que se mueve).")]
    [SerializeField] private Rigidbody elevatorRb;

    [Tooltip("Waypoint inferior (posición de descanso).")]
    [SerializeField] private Transform waypointBottom;

    [Tooltip("Waypoint superior.")]
    [SerializeField] private Transform waypointTop;

    [Header("=== TIMING ===")]
    [Tooltip("Segundos que espera después de pisar el trigger antes de arrancar.")]
    [SerializeField] private float startDelay = 1.0f;

    [Tooltip("Tiempo total (en segundos) que tarda en ir de un waypoint al otro.")]
    [SerializeField] private float travelTime = 3.0f;

    [Header("=== CURVA DE MOVIMIENTO ===")]
    [Tooltip("Curva de 0 a 1. X = tiempo normalizado, Y = progreso. Úsala para simular carga pesada.")]
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("=== OPCIONES ===")]
    [Tooltip("Tag del jugador que activa el elevador.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Si está en true, después de llegar arriba volverá solo abajo tras el mismo trayecto.")]
    [SerializeField] private bool autoReturn = false;

    [Tooltip("Elevador empieza en el waypoint superior en lugar de abajo.")]
    [SerializeField] private bool startAtTop = false;

    private bool _isMoving = false;
    private bool _isAtTop = false;
    private Collider _triggerCollider;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        _triggerCollider.isTrigger = true;

        if (elevatorRb == null)
        {
            Debug.LogError($"[HeavyElevatorTrigger] {name}: Falta asignar elevatorRb.");
            enabled = false;
            return;
        }
        if (!waypointBottom || !waypointTop)
        {
            Debug.LogError($"[HeavyElevatorTrigger] {name}: Falta asignar waypoints.");
            enabled = false;
            return;
        }

        // Colocamos al elevador en la posición inicial correcta
        _isAtTop = startAtTop;
        elevatorRb.position = _isAtTop ? waypointTop.position : waypointBottom.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isMoving) return;
        if (!other.CompareTag(playerTag)) return;

        // Al pisar el trigger, decidimos si sube o baja
        Transform from = _isAtTop ? waypointTop : waypointBottom;
        Transform to   = _isAtTop ? waypointBottom : waypointTop;

        StartCoroutine(MoveElevatorRoutine(from, to, autoReturn));
    }

    private IEnumerator MoveElevatorRoutine(Transform from, Transform to, bool doAutoReturn)
    {
        _isMoving = true;

        // Espera inicial
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        // Viaje principal
        yield return MoveOnce(from.position, to.position);

        _isAtTop = (to == waypointTop);

        // Auto-return opcional
        if (doAutoReturn)
        {
            // Puedes agregar aquí un pequeño delay extra si querés
            yield return MoveOnce(to.position, from.position);
            _isAtTop = (from == waypointTop);
        }

        _isMoving = false;
    }

    /// <summary>
    /// Mueve el elevador de startPos a endPos usando la curva de movimiento.
    /// </summary>
    private IEnumerator MoveOnce(Vector3 startPos, Vector3 endPos)
    {
        if (travelTime <= 0.01f)
        {
            elevatorRb.MovePosition(endPos);
            yield break;
        }

        float elapsed = 0f;

        // Usamos FixedUpdate para mantenernos en el mundo de física
        while (elapsed < travelTime)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / travelTime);

            // Evaluamos la curva para simular peso/carga
            float curvedT = movementCurve.Evaluate(t);

            Vector3 newPos = Vector3.Lerp(startPos, endPos, curvedT);
            elevatorRb.MovePosition(newPos);

            yield return new WaitForFixedUpdate();
        }

        // Forzamos posición final exacta
        elevatorRb.MovePosition(endPos);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (waypointBottom && waypointTop)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(waypointBottom.position, waypointTop.position);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(waypointBottom.position, 0.1f);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(waypointTop.position, 0.1f);
        }
    }
#endif
}
