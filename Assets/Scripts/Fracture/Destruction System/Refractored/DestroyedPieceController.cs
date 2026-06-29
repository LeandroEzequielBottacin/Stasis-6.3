using System.Collections.Generic;
using Player.Stasis;
using UnityEngine;

namespace Fracture.Destruction_System.Refractored
{
    public class DestroyedPieceController : MonoBehaviour, IStasis
    {
        public bool is_connected = true;
        [HideInInspector] public bool visited;
        public List<DestroyedPieceController> connected_to;

        public static bool is_dirty;

        [SerializeField] private Rigidbody _rigidbody;
        private Vector3 _starting_pos;
        private Quaternion _starting_orientation;
        private Vector3 _starting_scale;

        private bool _configured;
        private bool _connections_found = false;

        public bool IsFreezed => isFreezed;
        public StasisEffect StasisEffect { get; set; }
        public bool isFreezed;
        public bool wasHit;

        public Renderer _renderer;

        public int ID;
        public bool alreadyColision;

        void Start()
        {
            ID = Random.Range(1, 10000);
            _renderer = GetComponent<Renderer>();
            StasisEffect = new StasisEffect(_renderer);

            connected_to = new List<DestroyedPieceController>();
            _starting_pos = transform.position;
            _starting_orientation = transform.rotation;
            _starting_scale = transform.localScale;

            transform.localScale *= 1.02f;

            _rigidbody = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_configured)
            {
                var neighbour = collision.gameObject.GetComponent<DestroyedPieceController>();
                if (neighbour)
                {
                    if (!connected_to.Contains(neighbour))
                        connected_to.Add(neighbour);
                }
            }
        }

        private void Update()
        {
            if (wasHit)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        public void make_static()
        {
            _configured = true;
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = true;

            transform.localScale = _starting_scale;
            transform.position = _starting_pos;
            transform.rotation = _starting_orientation;
        }

        public void cause_damage(Vector3 force)
        {
            is_connected = false;
            _rigidbody.isKinematic = false;
            is_dirty = true;
            _rigidbody.AddForce(force, ForceMode.Impulse);
        }

        public void drop()
        {
            is_connected = false;
            _rigidbody.isKinematic = false;
        }

        public void StatisEffectActivate()
        {
            FreezeObject();
        }

        public void StatisEffectDeactivate()
        {
            UnfreezeObject();
        }

        private void FreezeObject()
        {
            if (isFreezed) return;
            if (is_connected) return;
            isFreezed = true;
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
            _rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            StasisEffect.StasisEffectStart();
        }


        private void UnfreezeObject()
        {
            if (!isFreezed) return;
            isFreezed = false;
            _rigidbody.useGravity = true;
            _rigidbody.isKinematic = false;
            _rigidbody.constraints = RigidbodyConstraints.None;
            StasisEffect.StasisEffectStop();
        }
    }
}