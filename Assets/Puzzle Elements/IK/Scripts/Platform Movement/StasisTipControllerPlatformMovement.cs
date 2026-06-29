using System;
using System.Collections;
using System.Collections.Generic;
using Player.Stasis;
using Puzzle_Elements.PlatformMovement;
using UnityEngine;

namespace Puzzle_Elements.IK.Scripts.Platform_Movement
{
    public class StasisTipControllerPlatformMovement : MonoBehaviour, IStasis
    {
        public bool IsFreezed => _isFreezed;
        public StasisEffect StasisEffect { get; }
        [SerializeField]private bool _isFreezed;
        [Header("Stasis")]
        public readonly string _outlineThicknessName = "_BorderThickness";
        public MaterialPropertyBlock _mpb;
        private Rigidbody _rb;

        // Soporta m�ltiples renderers
        public List<Renderer> renderers = new();
        [SerializeField] private Transform _root;
        public event Action OnFreezeEvent;
        public event Action OnUnFreezeEvent;

        [SerializeField] private PathFollower1 _pathFollower1;
    
        void Start()
        {
            _mpb = new MaterialPropertyBlock();
            _pathFollower1 = GetComponent<PathFollower1>();
            _rb = GetComponent<Rigidbody>();
            StartCoroutine(wait());
        }

        IEnumerator wait()
        {
            yield return new WaitForSeconds(2f);
            AddElementsToRenderer();
        }
        public void AddElementsToRenderer()
        {
            if (renderers.Count > 4) return;

            Renderer[] allRenderers = _root.GetComponentsInChildren<Renderer>();

            foreach (Renderer mesh in allRenderers)
            {
                // Evita duplicados si ya lo agregaste
                if (!renderers.Contains(mesh))
                    renderers.Add(mesh);
            }
            Renderer[] allRenderers2 = GetComponentsInChildren<Renderer>();
            foreach (Renderer mesh in allRenderers2)
            {
                // Evita duplicados si ya lo agregaste
                if (!renderers.Contains(mesh))
                    renderers.Add(mesh);
            }
        }
    
        public void EventPositiveTipPlatformMovement()
        {
            if(!IsFreezed) StatisEffectActivate();
        }
    
        public void EventNegativeTipPlatformMovement()
        {
            if (IsFreezed) StatisEffectDeactivate();
        }
    
        public void StatisEffectActivate()
        {
            FreezeObject();
            _pathFollower1.IsMove = false;
        }

        public void StatisEffectDeactivate()
        {
            UnfreezeObject();
            _pathFollower1.IsMove = true;
        }
        private void FreezeObject()
        {
            if (!_isFreezed)
            {
                _isFreezed = true;
                SetOutlineThickness(1.05f);
                SetColorOutline(Color.green, 1f);
                _rb.isKinematic = true;
            }
        }

        private void UnfreezeObject()
        {
            if (!_isFreezed) return;
            _isFreezed = false;
            _rb.isKinematic = false;
            SetOutlineThickness(0f);
            Color lightGreen = new Color(0.6f, 1f, 0.6f);
            SetColorOutline(lightGreen, 1f);
        }
    
        public void SetOutlineThickness(float thickness)
        {
            if (renderers == null || _mpb == null) return;

            foreach (var rend in renderers)
            {
                if (!rend) continue;
                rend.GetPropertyBlock(_mpb);
                _mpb.SetFloat(_outlineThicknessName, thickness);
                rend.SetPropertyBlock(_mpb);
            }
        }

        public void SetColorOutline(Color color, float alpha)
        {
            if (renderers == null) return;

            foreach (var rend in renderers)
            {
                if (!rend) continue;
                rend.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Color", color);
                rend.SetPropertyBlock(_mpb);
            }
        }
    }
}
