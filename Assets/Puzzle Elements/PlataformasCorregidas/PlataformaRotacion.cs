using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlataformaRotacion : MonoBehaviour
{
    [Header("Layer / Tag del Player")]
    [Tooltip("Opcional: filtrar por tag para asegurarnos de que es el player.")]
    public string playerTag = "Player";

    private Rigidbody _playerRb;      // Rigidbody del player
    private bool _playerInside;       // Solo true si está dentro del trigger

    private Vector3 lastPos;
    private Quaternion lastRot;

    void Start()
    {
        lastPos = transform.position;
        lastRot = transform.rotation;

        // Asegurate de que el collider esté en modo trigger en el inspector
        // (no lo fuerzo acá por si lo manejás desde otro lado).
        // GetComponent<Collider>().isTrigger = true;
    }

    void FixedUpdate()
    {
        // Calculamos siempre el delta de la plataforma por paso de física
        Vector3 deltaPos = transform.position - lastPos;
        Quaternion deltaRot = transform.rotation * Quaternion.Inverse(lastRot);

        if (_playerInside && _playerRb != null)
        {
            Vector3 worldPos = _playerRb.position;

            // Vector desde la plataforma (posición anterior) al player
            Vector3 fromOldPlatform = worldPos - lastPos;

            // Ese vector rotado con el delta de la plataforma
            Vector3 fromNewPlatform = deltaRot * fromOldPlatform;

            // Posición que tendría si estuviera solidario a la plataforma
            Vector3 attachedWorldPos = transform.position + fromNewPlatform;

            // Delta extra que hay que sumar (sin pisar el movimiento propio del player)
            Vector3 extraDelta = attachedWorldPos - worldPos;

            // Aplicamos con física
            _playerRb.MovePosition(worldPos + extraDelta);
            _playerRb.MoveRotation(deltaRot * _playerRb.rotation);
        }

        // Actualizamos estado de la plataforma para el próximo FixedUpdate
        lastPos = transform.position;
        lastRot = transform.rotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody == null) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        _playerRb = other.attachedRigidbody;
        _playerInside = true;

        // Reseteamos referencia para evitar un salto brusco al engancharse
        lastPos = transform.position;
        lastRot = transform.rotation;
    }

    private void OnTriggerStay(Collider other)
    {
        if (_playerRb == null) return;
        if (other.attachedRigidbody != _playerRb) return;

        _playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (_playerRb == null) return;
        if (other.attachedRigidbody != _playerRb) return;

        _playerInside = false;
        _playerRb = null;
    }
}
