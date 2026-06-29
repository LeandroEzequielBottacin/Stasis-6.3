using Puzzle_Elements.Path.CurvedPathGenerator.Scripts;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.PlatformMovement
{
    [RequireComponent(typeof(Rigidbody))]
    public class PathFollower1 : MonoBehaviour
    {
        public ParticleSystem particle;
        public UnityEvent EndEvent;
        public PathGenerator Generator;
        public float Speed = 100f;
        public float DistanceThreshold = 0.2f;
        public bool IsLoop;
        public bool IsMove = true;
        public bool InvokeEndEventAtExtremes;

        Rigidbody _rb;
        int _index;
        int _dir;
        Vector3 _next;
        bool _initialized;
        Matrix4x4 _lastGenMatrix;

        void Start()
        {
            _rb = GetComponent<Rigidbody>();
            if (Generator == null)
            {
                IsMove = false;
                return;
            }

            Generator.UpdatePath();
            if (Generator.PathList == null || Generator.PathList.Count < 2)
            {
                IsMove = false;
                return;
            }

            transform.position = Generator.PathList[0];
            _index = 1;
            _dir = 1;
            _next = Generator.PathList[_index];
            _lastGenMatrix = Generator.transform.localToWorldMatrix;
            _initialized = true;
            if (particle != null) particle.Play();
        }

        void FixedUpdate()
        {
            if (!IsMove || !_initialized) return;

            RefreshPathIfNeeded();

            Vector3 toNext = _next - transform.position;
            float dist = toNext.magnitude;

            if (dist > 1e-6f)
            {
                Vector3 step = toNext.normalized * (Speed * Time.fixedDeltaTime);
                if (step.magnitude > dist) step = toNext;
                _rb.MovePosition(_rb.position + step);
            }

            if (dist < DistanceThreshold)
                AdvanceIndex();
        }

        void AdvanceIndex()
        {
            int count = Generator.PathList.Count;

            if (IsLoop)
            {
                _index++;
                if (_index >= count)
                {
                    _index = 0;
                    if (InvokeEndEventAtExtremes && EndEvent != null)
                        EndEvent.Invoke();
                }
            }
            else
            {
                _index += _dir;

                if (_index >= count)
                {
                    _dir = -1;
                    _index = count - 2;
                    if (InvokeEndEventAtExtremes && EndEvent != null)
                        EndEvent.Invoke();
                }
                else if (_index < 0)
                {
                    _dir = 1;
                    _index = 1;
                    if (InvokeEndEventAtExtremes && EndEvent != null)
                        EndEvent.Invoke();
                }
            }

            _next = Generator.PathList[_index];
        }

        void RefreshPathIfNeeded()
        {
            bool trsChanged = (_lastGenMatrix != Generator.transform.localToWorldMatrix);
            if (trsChanged || Generator.IsLivePath)
            {
                Generator.UpdatePath();
                int count = (Generator.PathList != null) ? Generator.PathList.Count : 0;
                if (count < 2)
                {
                    IsMove = false;
                    return;
                }

                _index = Mathf.Clamp(_index, 0, count - 1);
                _next = Generator.PathList[_index];
                _lastGenMatrix = Generator.transform.localToWorldMatrix;
            }
        }

        public void StopFollow()
        {
            IsMove = false;
            if (particle != null) particle.Stop();
        }

        public void StartFollow()
        {
            if (Generator == null) return;
            if (Generator.PathList == null || Generator.PathList.Count < 2) return;
            IsMove = true;
            if (particle != null && !particle.isPlaying) particle.Play();
        }
    }
}
