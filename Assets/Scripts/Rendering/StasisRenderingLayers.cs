namespace Stasis.Rendering
{
    /// <summary>
    /// The rendering layer bit that marks a renderer as currently frozen in stasis.
    ///
    /// StasisEffect sets this bit, and StasisOutlineFeature filters its mask pass on it,
    /// so only the one or two objects actually in stasis are drawn each frame instead of
    /// every object that could ever be frozen.
    ///
    /// The bit is added to whatever mask the renderer already has, never replacing it.
    /// Everything in the project sits on bit 0 and lights accept all layers, so an extra
    /// high bit changes nothing about how the object is lit or decalled.
    /// </summary>
    public static class StasisRenderingLayers
    {
        /// <summary>Bit index reserved for stasis. Keep clear of the low bits Unity's
        /// rendering layer names occupy.</summary>
        public const int StasisBit = 20;

        public const uint StasisMask = 1u << StasisBit;
    }
}
