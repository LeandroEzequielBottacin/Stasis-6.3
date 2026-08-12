namespace _Ian.VFX.Smoke
{
    public interface IHazardSlowTarget
    {
        /// <summary>
        /// Multiplicador externo de velocidad (1 = normal, 0 = totalmente inmóvil).
        /// El controller/estado debe combinar esto en su lógica de movimiento.
        /// </summary>
        void SetExternalSpeedMultiplier(float multiplier);

        /// <summary>
        /// Opcional: lectura del valor actual, útil para debug/FX.
        /// </summary>
        float GetExternalSpeedMultiplier();
    }
}