using UnityEngine;

namespace Player.Stasis
{
    public class StasisEffect
    {
        private static readonly int MainColor = Shader.PropertyToID("_Color");
        
        private readonly Renderer _mainRenderer;
        private readonly Renderer[] _renderers;
        private readonly string _outlineThicknessName = "_BorderThickness";
        private readonly MaterialPropertyBlock _mpb;

        public StasisEffect(Renderer mainRenderer = null, Renderer[] renderers = null)
        {
            _mainRenderer = mainRenderer;
            _renderers = renderers;
            _mpb = new MaterialPropertyBlock();
        }

        public void StasisEffectStart()
        {
            SetOutlineThickness(1.05f);
            SetColorOutline(Color.green);
        }
    
        public void StasisEffectStop()
        {
            SetOutlineThickness(0);
        }
        
        private void SetOutlineThickness(float thickness)
        {
            if (!_mainRenderer)
            {
                if (_renderers != null)
                {
                    foreach (var rend in _renderers)
                    {
                        rend.GetPropertyBlock(_mpb);
                        _mpb.SetFloat(_outlineThicknessName, thickness);
                        rend.SetPropertyBlock(_mpb);
                    }
                }
            }
            else
            {
                _mainRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(_outlineThicknessName, thickness);
                _mainRenderer.SetPropertyBlock(_mpb);
            }
        }

        private void SetColorOutline(Color color)
        {
            if (!_mainRenderer)
            {
                if (_renderers != null)
                {
                    foreach (var rend in _renderers)
                    {
                        rend.GetPropertyBlock(_mpb);
                        _mpb.SetColor(MainColor, color);
                        rend.SetPropertyBlock(_mpb);
                    }
                }
            }
            else
            {
                _mainRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(MainColor, color);
                _mainRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
