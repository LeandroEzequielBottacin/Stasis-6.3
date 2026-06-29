using Fracture.Destruction_System.Refractored;
using UnityEngine;
using UnityEngine.UI;

namespace Player.Stasis
{
    [RequireComponent(typeof(AudioSource))]
    public class StasisObjectEffects : MonoBehaviour
    {
        [Header("Animation Settings")]
        public AnimationCurve animCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float animSpeed = 2f;
        [Header("Scale Settings")]
        public float normalScale = 1f;
        public float highlightScale = 1.2f;
        [Header("Color Settings")]
        public Color normalColor = Color.white;
        public Color highlightColor = Color.cyan;
        [SerializeField] private float rotationSpeed = 10;
        
        [SerializeField] private Image crosshair;
        [SerializeField] private Sprite crosshairBasic;
        [SerializeField] private Sprite crosshairStasis;
        
        // Variables mejoradas para evitar parpadeo
        private float _lastCrosshairChangeTime;
        private readonly float _changeCooldown = 0.15f; // Cooldown para cambios
        private bool _isCurrentlyLookingAtStasis;

        public GameObject objectStaseable;
        
        private void Update()
        {

            if (crosshair.sprite == crosshairStasis)
            {
                crosshair.rectTransform.Rotate(Vector3.forward * (rotationSpeed * Time.deltaTime));
            }
            else
            {
                crosshair.rectTransform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }

        public void HandleVisualStasisFeedback(IStasis lookedStasisObject, UnityEngine.Camera cam, RaycastHit hit1)
        {
            Vector3 origin = cam.transform.position;
            Vector3 direction = cam.transform.forward;
            float radius = 0.05f;
            float maxDistance = 100f;

            bool hitSomething = Physics.SphereCast(origin, radius, direction, out RaycastHit hit, maxDistance);
            IStasis hitStasis = hitSomething ? hit.collider.GetComponent<IStasis>() : null;
            

            if(hitStasis != null)
            {
                objectStaseable = hit.collider.gameObject;
            }
            else
            {
                objectStaseable = null;
            }

            bool objectConnected = CheckObjectStasisConnected(hit1);

            bool confirmed = lookedStasisObject != null || objectStaseable != null;

            bool canChangeState = Time.time - _lastCrosshairChangeTime >= _changeCooldown;
            
            if (confirmed && !objectConnected && canChangeState)
            {
                
                if (!_isCurrentlyLookingAtStasis)
                {
                    _isCurrentlyLookingAtStasis = true;
                    crosshair.sprite = crosshairStasis;
                    crosshair.color = highlightColor;
                    _lastCrosshairChangeTime = Time.time;
                    Debug.Log("CONFIRMED " + lookedStasisObject);
                }
            }
            else if (objectConnected && _isCurrentlyLookingAtStasis)
            {
                _isCurrentlyLookingAtStasis = false;
                crosshair.sprite = crosshairBasic;
                crosshair.color = normalColor;
                _lastCrosshairChangeTime = Time.time;
            }

            else if (!confirmed && _isCurrentlyLookingAtStasis && canChangeState)
            {
                _isCurrentlyLookingAtStasis = false;
                crosshair.sprite = crosshairBasic;
                crosshair.color = normalColor;
                _lastCrosshairChangeTime = Time.time;
                Debug.Log("Crosshair reseteado a Básico");
                Debug.Log("NO CONFIRMED " + lookedStasisObject);
            }
        }

        private bool CheckObjectStasisConnected(RaycastHit hit)
        {
            DestroyedPieceController piece = hit.collider.GetComponentInParent<DestroyedPieceController>();
            if (!piece) return false;
            return piece.is_connected;
        }
    }
}