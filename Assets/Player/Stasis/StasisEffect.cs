using Stasis.Rendering;
using UnityEngine;

namespace Player.Stasis
{
    /// <summary>
    /// Turns the stasis outline on and off for a set of renderers.
    ///
    /// This just flips a rendering layer bit; StasisOutlineFeature does the drawing. It
    /// used to push _BorderThickness through a MaterialPropertyBlock instead, which both
    /// forced every frozen renderer out of the SRP Batcher and required each object to
    /// carry an extra outline material slot.
    ///
    /// The outline colour is a property of the feature now, not of the object, so it is
    /// set once on the renderer feature rather than per object.
    /// </summary>
    public class StasisEffect
    {
        private readonly Renderer _mainRenderer;
        private readonly Renderer[] _renderers;

        public StasisEffect(Renderer mainRenderer = null, Renderer[] renderers = null)
        {
            _mainRenderer = mainRenderer;
            _renderers = renderers;

            if (!_mainRenderer && (_renderers == null || _renderers.Length == 0))
            {
                Debug.LogError($"{nameof(StasisEffect)} was constructed with no renderers; " +
                               "this object will never show a stasis outline.");
            }
        }

        public void StasisEffectStart() => SetOutlineEnabled(true);

        public void StasisEffectStop() => SetOutlineEnabled(false);

        private void SetOutlineEnabled(bool enabled)
        {
            if (_mainRenderer)
            {
                Apply(_mainRenderer, enabled);
                return;
            }

            if (_renderers == null) return;
            foreach (var rend in _renderers) Apply(rend, enabled);
        }

        private static void Apply(Renderer rend, bool enabled)
        {
            if (!rend) return;

            // Added to whatever mask the renderer already has, never replacing it, so
            // lighting and decal layer assignments are untouched.
            rend.renderingLayerMask = enabled
                ? rend.renderingLayerMask | StasisRenderingLayers.StasisMask
                : rend.renderingLayerMask & ~StasisRenderingLayers.StasisMask;
        }
    }
}
