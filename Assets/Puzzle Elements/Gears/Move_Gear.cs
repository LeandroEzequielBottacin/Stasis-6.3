using Environment;
using UnityEngine;

// Necesario para MultiGearRotator

/// Controlador de un engranaje espec�fico dentro de un MultiGearRotator.
/// Permite leer/escribir el "speed" del RotatorItem asociado.
public class Move_Gear : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Referencia al MultiGearRotator que controla este engranaje.")]
    public MultiGearRotator gearRotator;

    [Tooltip("Transform del engranaje asociado al RotatorItem (debe coincidir con 'target' en la lista del MultiGearRotator). " +
             "Si se deja vac�o, usar� este mismo transform.")]
    public Transform gearTransform;

    [Header("Selecci�n de item")]
    [Tooltip("Si est� activado, se usar� 'itemIndex' en lugar de 'gearTransform' para encontrar el RotatorItem.")]
    public bool useIndex;

    [Tooltip("�ndice del item en la lista 'items' del MultiGearRotator.")]
    public int itemIndex;

    [Header("Configuraci�n de velocidad")]
    [Tooltip("Velocidad cuando el engranaje est� girando.")]
    public float runningSpeed = 120f;

    [Tooltip("Velocidad cuando el engranaje est� detenido (normalmente 0).")]
    public float stoppedSpeed;

    [Tooltip("Si est� activado, el engranaje arranca girando. Si no, arranca detenido.")]
    public bool startRunning = true;

    // Cache interno del item asociado
    private MultiGearRotator.RotatorItem _item;

    private void Awake()
    {
        // Si no se asign� el rotator por inspector, intentamos encontrar uno en la escena
        if (gearRotator == null)
        {
            gearRotator = FindObjectOfType<MultiGearRotator>();
            if (gearRotator == null)
            {
                Debug.LogError("[Move_Gear] No se encontr� ning�n MultiGearRotator en la escena.", this);
            }
        }

        if (gearTransform == null)
        {
            // Por defecto, usamos el mismo GameObject donde est� este script
            gearTransform = transform;
        }
    }

    private void Start()
    {
        CacheItem();

        if (_item == null)
        {
            Debug.LogError("[Move_Gear] No se pudo encontrar un RotatorItem asociado. Revis� 'gearTransform' o 'itemIndex'.", this);
            return;
        }

        // Estado inicial
        _item.speed = startRunning ? runningSpeed : stoppedSpeed;
        SetRunning(startRunning);
      //  NoRun();
    }

    private void OnValidate()
    {
        // Intentamos mantener coherencia b�sica en editor
        if (itemIndex < 0) itemIndex = 0;
    }

    /// <summary>
    /// Busca y cachea el RotatorItem correspondiente, ya sea por �ndice o por Transform.
    /// </summary>
    private void CacheItem()
    {
        _item = null;

        if (gearRotator == null || gearRotator.items == null || gearRotator.items.Count == 0)
            return;

        if (useIndex)
        {
            if (itemIndex >= 0 && itemIndex < gearRotator.items.Count)
            {
                _item = gearRotator.items[itemIndex];
            }
        }
        else
        {
            if (gearTransform == null)
                gearTransform = transform;

            foreach (var it in gearRotator.items)
            {
                if (it != null && it.target == gearTransform)
                {
                    _item = it;
                    break;
                }
            }
        }
    }
    public void NoRun()
    {
        SetRunning(false);
    }
    public void Run()
    {
        SetRunning(true);
    }
    // ===================== API P�BLICA =====================

    /// <summary>
    /// Activa o desactiva la rotaci�n del engranaje.
    /// true = runningSpeed, false = stoppedSpeed.
    /// </summary>
    public void SetRunning(bool running)
    {
        if (_item == null) CacheItem();
        if (_item == null) return;

        _item.speed = running ? runningSpeed : stoppedSpeed;
    }

    /// <summary>
    /// Alterna entre velocidad detenida y velocidad de giro.
    /// </summary>
    public void ToggleRunning()
    {
        if (_item == null) CacheItem();
        if (_item == null) return;

        bool isStopped = Mathf.Approximately(_item.speed, stoppedSpeed);
        _item.speed = isStopped ? runningSpeed : stoppedSpeed;
    }

    /// <summary>
    /// Setea un valor de speed arbitrario para este engranaje.
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        if (_item == null) CacheItem();
        if (_item == null) return;

        _item.speed = newSpeed;
    }

    /// <summary>
    /// Devuelve el speed actual del RotatorItem asociado.
    /// </summary>
    public float GetSpeed()
    {
        if (_item == null) CacheItem();
        if (_item == null) return 0f;

        return _item.speed;
    }

    // ===================== EJEMPLO OPCIONAL: Input =====================

    // Si quer�s probar r�pido, pod�s usar esto: con la tecla T toggles el engranaje.
    private void Update()
    {
        // Elimina esto si no quer�s input directo
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleRunning();
        }
    }
}
