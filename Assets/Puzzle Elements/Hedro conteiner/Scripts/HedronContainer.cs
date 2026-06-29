using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Hedro_conteiner.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class HedronContainer : MonoBehaviour
    {
        [Header("Anchor")]
        public Transform anchor;

        [Header("Tint Renderers")]
        public Renderer[] renderersToTint;

        [Header("Colors")]
        public Color emptyColor = Color.red;
        public Color occupiedColor = Color.green;

        [Header("Events")]
        public UnityEvent onPlaced;
        public UnityEvent onRemoved;

        [Header("Attraction Settings")]
        public float attractionSpeed = 5f; // velocidad de atracci�n
        public float stopDistance = 0.05f; // distancia m�nima para considerar que lleg�

        private Transform currentOccupant;
        private Rigidbody occupantRb;
        private MaterialPropertyBlock _mpb;
        private bool isAttracting;

        void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
            _mpb = new MaterialPropertyBlock();
            ApplyColor(emptyColor);
        }

        void Update()
        {
            if (isAttracting && currentOccupant != null && anchor != null)
            {
                Vector3 targetPos = anchor.position;
                currentOccupant.position = Vector3.Lerp(
                    currentOccupant.position,
                    targetPos,
                    Time.deltaTime * attractionSpeed
                );

                if (Vector3.Distance(currentOccupant.position, targetPos) <= stopDistance)
                {
                    // ya lleg� al centro
                    isAttracting = false;
                    currentOccupant.position = targetPos;
                    currentOccupant.rotation = anchor.rotation;
                    //currentOccupant.SetParent(anchor, true);

                    ApplyColor(occupiedColor);
                    onPlaced?.Invoke();
                    Debug.Log("LLegue");
                }
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (currentOccupant != null) return;
            if (!HasPhysicsBox(other.gameObject)) return;

            currentOccupant = other.transform;
            occupantRb = other.attachedRigidbody;

            if (occupantRb != null)
            {
                occupantRb.linearVelocity = Vector3.zero;
                occupantRb.angularVelocity = Vector3.zero;
                occupantRb.useGravity = false;
                // occupantRb.isKinematic = true;
                ApplyColor(occupiedColor);
            }

            isAttracting = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (currentOccupant == null) return;
            if (other.transform != currentOccupant) return;

            if (occupantRb != null)
            {
                occupantRb.isKinematic = false;
            }

            currentOccupant.SetParent(null);
            currentOccupant = null;
            occupantRb = null;
            isAttracting = false;

            ApplyColor(emptyColor);
            onRemoved?.Invoke();
        }

        bool HasPhysicsBox(GameObject go)
        {
            return go.GetComponent("PhysicsBox") != null;
        }

        void ApplyColor(Color c)
        {
            if (renderersToTint == null) return;
            for (int i = 0; i < renderersToTint.Length; i++)
            {
                var r = renderersToTint[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", c);
                _mpb.SetColor("_Color", c);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
