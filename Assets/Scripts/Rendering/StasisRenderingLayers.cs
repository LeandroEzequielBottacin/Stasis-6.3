namespace Stasis.Rendering
{
    /// <summary>
    /// Rendering layer bits reserved for the stasis outline.
    ///
    /// Two bits, with different lifetimes:
    ///
    ///  * Eligible - authoring data, saved in the scene and prefabs. Marks the specific
    ///    renderers that should show an outline. Objects frozen by stasis are usually a
    ///    whole structure while only some of its renderers are meant to light up, and
    ///    that selection used to be expressed by putting an outline material on those
    ///    renderers. This bit carries the same selection without the material.
    ///
    ///  * Stasis - runtime state, set by StasisEffect while an object is frozen.
    ///    StasisOutlineFeature filters its mask pass on it, so the cost scales with how
    ///    many objects are actually in stasis rather than how many could be.
    ///
    /// Both are added to whatever mask a renderer already has, never replacing it.
    /// Everything in the project sits on bit 0 and lights accept all rendering layers,
    /// so extra high bits change nothing about how an object is lit or decalled.
    /// </summary>
    public static class StasisRenderingLayers
    {
        /// <summary>Bit index for "currently in stasis". Keep clear of the low bits
        /// Unity's rendering layer names occupy.</summary>
        public const int StasisBit = 20;

        /// <summary>Bit index for "this renderer is allowed to show an outline".</summary>
        public const int EligibleBit = 21;

        public const uint StasisMask = 1u << StasisBit;

        public const uint EligibleMask = 1u << EligibleBit;

        /// <summary>
        /// How many renderers are frozen right now.
        ///
        /// StasisOutlineFeature skips all of its work when this is zero, so a level with
        /// nothing frozen pays nothing for the effect. Best effort: a renderer destroyed
        /// while still frozen leaves the count high, which only costs an idle pass.
        /// </summary>
        public static int ActiveCount { get; private set; }

        public static bool AnyActive => ActiveCount > 0;

        // Statics survive a domain reload in the editor, so entering play mode with a
        // stale count would keep the feature running over an empty mask.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveCount() => ActiveCount = 0;


        private static UnityEngine.Shader s_OutlineShader;
        private static bool s_OutlineShaderResolved;

        /// <summary>
        /// Whether this renderer is allowed to show an outline.
        ///
        /// Normally that is the saved eligibility bit. Some renderers come from model
        /// prefabs nested inside other prefabs, where Unity will not persist the bit as
        /// an instance override; those still carry the old outline material, so the
        /// material doubles as the marker. Only renderers being frozen reach this, so
        /// the material scan costs nothing at steady state.
        /// </summary>
        private static bool IsEligible(UnityEngine.Renderer rend)
        {
            if ((rend.renderingLayerMask & EligibleMask) != 0) return true;

            if (!s_OutlineShaderResolved)
            {
                s_OutlineShader = UnityEngine.Shader.Find("Stasis/S_StasisOutline");
                s_OutlineShaderResolved = true;
                if (s_OutlineShader == null)
                    UnityEngine.Debug.LogError(
                        "StasisRenderingLayers: no se encontro el shader 'Stasis/S_StasisOutline'. " +
                        "Los renderers que aun usan el material de outline como marcador no se van a contornear.");
            }
            if (s_OutlineShader == null) return false;

            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].shader == s_OutlineShader) return true;

            return false;
        }


        /// <summary>
        /// Turns the stasis outline on or off for one renderer.
        ///
        /// Several systems freeze objects (StasisEffect, the IK tip controllers, the
        /// container arms, the gears) and each used to poke _BorderThickness through its
        /// own MaterialPropertyBlock. They all route through here instead, so the
        /// eligibility rule lives in exactly one place.
        /// </summary>
        public static void SetOutline(UnityEngine.Renderer rend, bool enabled)
        {
            if (!rend) return;

            // Callers hand us whole structures, but only the renderers marked eligible
            // are meant to light up. Skipping the rest is what keeps the outline off the
            // entire structure.
            if (!IsEligible(rend)) return;

            // Added to whatever mask the renderer already has, never replacing it, so
            // lighting and decal layer assignments are untouched.
            bool was = (rend.renderingLayerMask & StasisMask) != 0;
            if (enabled == was) return;

            rend.renderingLayerMask = enabled
                ? rend.renderingLayerMask | StasisMask
                : rend.renderingLayerMask & ~StasisMask;

            ActiveCount = enabled ? ActiveCount + 1 : UnityEngine.Mathf.Max(0, ActiveCount - 1);

            // Frozen objects also tremble in place. Hooking it here means every system
            // that freezes something gets it without having to opt in.
            StasisVibration.Set(rend.transform, enabled);
        }

    }
}
