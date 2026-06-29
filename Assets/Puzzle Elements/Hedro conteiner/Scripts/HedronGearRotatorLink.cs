using Environment;
using Puzzle_Elements.Hedro_conteiner.Scripts;
using UnityEngine;
// MultiGearRotator
// HedronContainerIn

public class HedronGearRotatorLink : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private HedronContainerIn container;
    [SerializeField] private MultiGearRotator multiGearRotator;

    [Header("Engranajes controlados por este Spot")]
    [Tooltip("Solo estos engranajes se verán afectados por este container.")]
    [SerializeField] private Transform[] controlledGears;

    [Header("Estado inicial")]
    [Tooltip("Si está activo, estos engranajes arrancan parados hasta que el hedro se inserte.")]
    [SerializeField] private bool startPaused = true;

    private void Reset()
    {
        if (container == null)
            container = GetComponent<HedronContainerIn>();

        if (multiGearRotator == null)
            multiGearRotator = FindObjectOfType<MultiGearRotator>();
    }

    private void Awake()
    {
        // Subscribirse a los eventos del container
        if (container != null)
        {
            container.onHedronPlaced.AddListener(OnHedronPlaced);
            container.onHedronRemoved.AddListener(OnHedronRemoved);
        }
        else
        {
            Debug.LogWarning("[HedronGearRotatorLink] No hay HedronContainerIn asignado.", this);
        }
    }

    private void Start()
    {
        if (startPaused)
            PauseControlledGears();
    }

    private void OnDestroy()
    {
        if (container != null)
        {
            container.onHedronPlaced.RemoveListener(OnHedronPlaced);
            container.onHedronRemoved.RemoveListener(OnHedronRemoved);
        }
    }

    // ================== Callbacks de Container ==================

    private void OnHedronPlaced()
    {
        ResumeControlledGears();
    }

    private void OnHedronRemoved()
    {
        PauseControlledGears();
    }

    // ================== Helpers ==================

    private void PauseControlledGears()
    {
        if (multiGearRotator == null || controlledGears == null) return;

        foreach (var t in controlledGears)
        {
            if (t == null) continue;
            multiGearRotator.PauseItem(t);
        }
    }

    private void ResumeControlledGears()
    {
        if (multiGearRotator == null || controlledGears == null) return;

        foreach (var t in controlledGears)
        {
            if (t == null) continue;
            multiGearRotator.ResumeItem(t);
        }
    }
}
