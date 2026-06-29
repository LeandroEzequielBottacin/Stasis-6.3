using System.Collections;
using UnityEngine;

namespace Player.Scripts.Interactor
{
    public class HeldObjectController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float alignDuration = 0.2f;
        [SerializeField] private float scrollMoveSpeed = 3.5f;
        [SerializeField] private float retractForce = 3f;

        private Transform _holdArea;
        private Transform _holdGhost;
        private UnityEngine.Camera _playerCamera;
        private Rigidbody _rb;
        private Quaternion _fixedRotation;
        private bool _initialized;

        /// <summary> Debe llamarse al spawnear/agarrar el objeto. </summary>
        public void Initialize(Transform holdContainer, Transform ghost, UnityEngine.Camera cam)
        {
            _holdArea = holdContainer;
            _holdGhost = ghost;
            _playerCamera = cam ? cam : UnityEngine.Camera.main;
            if (!_rb) _rb = GetComponent<Rigidbody>();

            if (!_rb)
            {
                Debug.LogError("[HeldObjectController] Falta Rigidbody en el objeto.");
                enabled = false;
                return;
            }

            // Si faltan targets, no iniciamos.
            if (!_holdArea || !_holdGhost)
            {
                Debug.LogError("[HeldObjectController] Faltan referencias: holdArea u holdGhost.");
                enabled = false;
                return;
            }

            // Defaults suaves
            if (!_playerCamera) _playerCamera = UnityEngine.Camera.main;

            _rb.useGravity = false;
            _rb.linearDamping = 10;

            _initialized = true;
            StartCoroutine(AlignToHoldArea());
        }

        private IEnumerator AlignToHoldArea()
        {
            if (!_initialized) yield break;

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float elapsed = 0f;

            while (elapsed < alignDuration)
            {
                elapsed += Time.deltaTime;

                // Lerp hacia el área de sujeción (siempre valida)
                transform.position = Vector3.Lerp(startPos, _holdArea.position, elapsed / alignDuration);

                // Rotación: preserva X original
                Quaternion targetRot = Quaternion.Euler(startRot.eulerAngles.x, 0f, 0f);
                transform.rotation = Quaternion.Lerp(startRot, targetRot, elapsed / alignDuration);
                yield return null;
            }

            transform.position = _holdArea.position;
            transform.rotation = Quaternion.Euler(startRot.eulerAngles.x, 0f, 0f);

            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _fixedRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            if (!_initialized || !_rb || !_holdGhost) return;

            _rb.linearVelocity = Vector3.zero;

            float lerpFactor = Vector3.Distance(transform.position, _holdGhost.position) * Time.smoothDeltaTime * 2f;
            transform.position = Vector3.Lerp(_rb.position, _holdGhost.position, lerpFactor);
        }

        private void Update()
        {
            if (!_initialized) return;

            // Scroll: mover relativo a la cámara si existe; si no, en espacio mundial.
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0f)
            {
                Vector3 direction = scroll > 0 ? Vector3.forward : Vector3.back;
                Transform frame = _playerCamera ? _playerCamera.transform : null;

                if (frame)
                    transform.Translate(direction * (scrollMoveSpeed * Time.deltaTime), frame);
                else
                    transform.Translate(direction * (scrollMoveSpeed * Time.deltaTime), Space.World);

                if (_holdGhost) _holdGhost.position = transform.position;
            }

            // Retracción + release con RMB
            if (Input.GetKey("mouse 1") && _holdArea && _rb)
            {
                _rb.AddForce(-(_holdArea.position - transform.position) * retractForce, ForceMode.Impulse);
                ReleaseAndCleanup();
            }

            // Rotación del contenedor con R
            if (Input.GetKey("r") && _holdArea)
            {
                float angleY = Input.GetAxis("Mouse X") * 4.0f;
                float angleX = Input.GetAxis("Mouse Y") * 4.0f;
                Vector3 currentPos = transform.position;
                _holdArea.Rotate(new Vector3(angleX, -angleY, 0));
                transform.position = currentPos;
            }

            // Release con LMB al soltar si hay hijos
            if (_holdArea && _holdArea.childCount >= 2 && Input.GetMouseButtonUp(0))
            {
                ReleaseAndCleanup();
            }
        }

        private void ReleaseObject(float throwForce)
        {
            if (!_rb) return;

            _rb.constraints = RigidbodyConstraints.None;
            _rb.useGravity = true;
            _rb.linearDamping = 1;

            // Aplicar fuerza solo si tenemos cámara
            if (_playerCamera)
                _rb.AddForce(_playerCamera.transform.forward * throwForce, ForceMode.Impulse);

            transform.SetParent(null);
            Destroy(this);
        }

        private void ReleaseAndCleanup()
        {
            // Evitar NRE si no se inicializó
            if (!_initialized) return;
            ReleaseObject(0f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_rb) _rb.freezeRotation = false;
        }

        private void OnCollisionExit(Collision collision)
        {
            if (_rb) _rb.freezeRotation = true;
        }
    }
}