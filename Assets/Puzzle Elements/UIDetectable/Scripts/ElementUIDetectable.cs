using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

[DisallowMultipleComponent]
public class ElementUIDetectable : MonoBehaviour
{
    [Header("Referencia al Player")]
    [Tooltip("Transform del jugador. Si est� vac�o, intentar� buscar uno con tag 'Player'.")]
    public Transform player;

    [Header("Billboard / Mirar hacia")]
    [Tooltip("Opcional: objetivo espec�fico a mirar (por ejemplo, la c�mara del player). Si es null, usar� 'player'.")]
    public Transform lookTarget;

    [Tooltip("Si est� activado, el prompt rotar� para mirar al objetivo.")]
    public bool faceTarget = true;

    [Tooltip("Si est� activado, solo rotar� en el eje Y (mantiene la vertical).")]
    public bool yAxisOnly = true;

    [Tooltip("Si el sprite se ve dado vuelta, activ� esto para invertir la direcci�n.")]
    public bool invertFacing = true;

    [Header("Distancia de detecci�n")]
    [Tooltip("Distancia m�xima a la que se muestra el icono/imagen.")]
    public float showDistance = 3f;

    [Tooltip("Distancia a partir de la cual se oculta de nuevo (ligeramente mayor para evitar flicker).")]
    public float hideDistance = 3.5f;

    [Header("Visual del prompt")]
    [Tooltip("GameObject con el Sprite/Imagen/Canvas que se mostrar� cuando el jugador est� cerca.")]
    public GameObject promptVisual;

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.5f);

    private bool _isVisible;
    [SerializeField]private bool _alreadyTakeByPlayer;
    private void Awake()
    {
        // Aseguramos que el prompt arranca apagado
        if (promptVisual != null)
            promptVisual.SetActive(false);

        // Evitar que hideDistance sea menor que showDistance
        if (hideDistance < showDistance)
            hideDistance = showDistance + 0.25f;
    }

    private bool IsPlayerMyFather()
    {
        // Busca Model en este objeto o en cualquiera de sus padres
        return GetComponentInParent<Model>() != null;
    }
    private void Update()
    {
        if (player == null || promptVisual == null)
            return;
        if (IsPlayerMyFather())
        {
            _alreadyTakeByPlayer = true;
            if(_isVisible)
            SetPromptVisible(false);
        }
        // --- L�gica de distancia (mostrar / ocultar) ---
        float dist = Vector3.Distance(player.position, transform.position);
        if (!_isVisible && dist <= showDistance && !_alreadyTakeByPlayer)
        {
            SetPromptVisible(true);

        }
        else if (_isVisible && dist >= hideDistance)
        {
            SetPromptVisible(false);
        }

        // --- Billboard (mirar hacia el player / c�mara) ---
        if (faceTarget && _isVisible)
        {
            Transform target = lookTarget != null ? lookTarget : player;
            if (target != null)
                FaceTarget(target);
        }
    }

    private void SetPromptVisible(bool visible)
    {
        _isVisible = visible;
        promptVisual.SetActive(visible);
    }
    private bool isPlayerMyFather()
    {
        if (transform.parent != null && transform.parent.GetComponent<Model>() != null)
        {
            return true;
        }
        return false;
    }
    private void FaceTarget(Transform target)
    {
        Transform t = promptVisual.transform;

        Vector3 toTarget = target.position - t.position;

        if (yAxisOnly)
        {
            toTarget.y = 0f;
        }

        if (toTarget.sqrMagnitude < 1e-4f)
            return;

        Vector3 dir = toTarget.normalized;

        // Si tu quad/sprite est� "al rev�s", lo invertimos
        if (invertFacing)
            dir = -dir;

        Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
        t.rotation = lookRot;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, showDistance);
    }
}
