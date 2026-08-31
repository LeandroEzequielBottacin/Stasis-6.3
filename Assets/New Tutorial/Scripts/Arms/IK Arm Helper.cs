using Puzzle_Elements.IK.Scripts;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class IKArmHelper : MonoBehaviour
{
    [SerializeField] private FollowTargetController _followTargetController;

    [SerializeField] private Transform _target;
    [SerializeField] private Transform _targetBrother;

    [SerializeField] private int _reactionDistance = 20;
    [SerializeField] private int _reactionDistanceInverse = 150;

    [Tooltip("Curve applied after remap (x: 0..1 input, y: 0..1 output).")]
    public AnimationCurve remapLerp = AnimationCurve.Linear(0, 0, 1, 1);

    [SerializeField] private bool isInverse;


    [SerializeField] private Color _colorMaterial;
    [SerializeField] private Color _colorInverseMaterial;
    [SerializeField] private GameObject _platform;


    // =========================
    // PUBLIC READ-ONLY DATA
    // =========================

    public bool IsInverse => isInverse;

    public float InMin => _followTargetController != null
        ? _followTargetController.inMin
        : 0f;

    public float InMax => isInverse
        ? _reactionDistanceInverse
        : _reactionDistance;

    public float OutMin => isInverse
        ? 0f
        : 1f;

    public float OutMax => isInverse
        ? 1f
        : 0f;


    private void Start()
    {

        ApplySettings();
    }


    private void ApplySettings()
    {
        if (_followTargetController == null)
            return;

        _followTargetController.inMax = InMax;
        _followTargetController.outMin = OutMin;
        _followTargetController.outMax = OutMax;

        if (_platform != null)
        {
            _platform.GetComponent<Renderer>().material.color =
                isInverse
                    ? _colorInverseMaterial
                    : _colorMaterial;
        }
        _followTargetController.remapLerp = remapLerp;
    }
}