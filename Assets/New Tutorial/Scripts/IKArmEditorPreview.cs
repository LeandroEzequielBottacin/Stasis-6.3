using UnityEngine;
using UnityEngine.Animations.Rigging;

[ExecuteAlways]
public class IKArmEditorPreview : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IKArmHelper armHelper;

    [SerializeField] private Transform tip;
    [SerializeField] private Transform target;

    [SerializeField] private ChainIKConstraint chainIK;
    [SerializeField] private RigBuilder rigBuilder;


    [Header("Editor Preview")]
    [SerializeField] private bool previewInEditor = true;

    [Range(0f, 200f)]
    [SerializeField] private float previewDistance;

    [Range(0f, 1f)]
    [SerializeField] private float weight;


    // =========================
    // INITIAL POSE
    // =========================

    [SerializeField, HideInInspector]
    private Vector3 initialTipPosition;

    [SerializeField, HideInInspector]
    private Vector3 initialTargetPosition;

    [SerializeField, HideInInspector]
    private Quaternion initialTipRotation;

    [SerializeField, HideInInspector]
    private Quaternion initialTargetRotation;

    [SerializeField, HideInInspector]
    private bool initialPoseSaved;


    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            SaveInitialPose();
        }
    }


    private void Update()
    {
        // Si entramos en Play:
        // restauramos todo y dejamos de intervenir.
        if (Application.isPlaying)
        {
            RestoreInitialPose();
            return;
        }

        // Si se desactiva el preview:
        // volver a la posición original.
        if (!previewInEditor)
        {
            RestoreInitialPose();
            return;
        }

        if (armHelper == null ||
            tip == null ||
            target == null ||
            chainIK == null ||
            rigBuilder == null)
            return;

        Preview();
    }


    private void SaveInitialPose()
    {
        if (initialPoseSaved)
            return;

        if (tip == null || target == null)
            return;

        initialTipPosition = tip.position;
        initialTargetPosition = target.position;

        initialTipRotation = tip.rotation;
        initialTargetRotation = target.rotation;

        initialPoseSaved = true;
    }


    private void RestoreInitialPose()
    {
        if (!initialPoseSaved)
            return;

        if (tip == null || target == null)
            return;

        tip.SetPositionAndRotation(
            initialTipPosition,
            initialTipRotation
        );

        target.SetPositionAndRotation(
            initialTargetPosition,
            initialTargetRotation
        );

        // Dejamos el IK sin influencia de preview.
        if (chainIK != null)
            chainIK.weight = 0f;

        if (rigBuilder != null)
        {
            if (!rigBuilder.graph.IsValid())
                rigBuilder.Build();

            rigBuilder.Evaluate(0f);
        }
    }


    private void Preview()
    {
        SaveInitialPose();

        float normalized = 0f;

        if (Mathf.Abs(
                armHelper.InMax -
                armHelper.InMin
            ) > 0.001f)
        {
            normalized = Mathf.InverseLerp(
                armHelper.InMin,
                armHelper.InMax,
                previewDistance
            );
        }

        float raw = Mathf.Lerp(
            armHelper.OutMin,
            armHelper.OutMax,
            normalized
        );

        weight = Mathf.Clamp01(raw);

        chainIK.weight = weight;

        if (!rigBuilder.graph.IsValid())
            rigBuilder.Build();

        rigBuilder.Evaluate(0f);
    }


    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            RestoreInitialPose();
        }
    }
}