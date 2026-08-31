using Player.Stasis;
using UnityEngine;

public enum StasisConnectionMode
{
    Arms,
    Fan
}

public class StasisElectricVisual : MonoBehaviour, IStasis
{
    [Header("Connection")]
    [SerializeField] private StasisConnectionMode connectionMode = StasisConnectionMode.Arms;

    [Header("Surface Collider")]
    [SerializeField] private Collider surfaceCollider;

    [Header("Optional Collider Sources")]
    [SerializeField] private Renderer[] targetRenderers;

    private void Awake()
    {
        ResolveSurfaceCollider();
    }

    public StasisConnectionMode ConnectionMode => connectionMode;

    public bool IsFreezed => throw new System.NotImplementedException();

    public StasisEffect StasisEffect => throw new System.NotImplementedException();

    public Collider GetSurfaceCollider()
    {
        if (surfaceCollider == null)
            ResolveSurfaceCollider();

        return surfaceCollider;
    }

    private void ResolveSurfaceCollider()
    {
        if (surfaceCollider != null)
            return;

        surfaceCollider = GetComponent<Collider>();

        if (surfaceCollider != null || targetRenderers == null)
            return;

        foreach (Renderer targetRenderer in targetRenderers)
        {
            if (targetRenderer == null)
                continue;

            surfaceCollider = targetRenderer.GetComponent<Collider>();

            if (surfaceCollider != null)
                return;
        }
    }

    public void StatisEffectActivate()
    {
        throw new System.NotImplementedException();
    }

    public void StatisEffectDeactivate()
    {
        throw new System.NotImplementedException();
    }
}
