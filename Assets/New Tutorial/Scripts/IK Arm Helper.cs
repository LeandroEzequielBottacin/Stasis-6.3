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

    [SerializeField] private bool isInverse;

    private ChainIKConstraint _tip;
    private GameObject _childrenTip;
    private Renderer _childrenTipRenderer;

    [SerializeField] private Color _colorMaterial;
    [SerializeField] private Color _colorInverseMaterial;


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
        _tip = GetComponentInChildren<ChainIKConstraint>();

        _childrenTip = _tip.data.tip.gameObject;
        _childrenTipRenderer = _childrenTip.GetComponent<Renderer>();

        ApplySettings();
    }


    private void ApplySettings()
    {
        if (_followTargetController == null)
            return;

        _followTargetController.inMax = InMax;
        _followTargetController.outMin = OutMin;
        _followTargetController.outMax = OutMax;

        if (_childrenTipRenderer != null)
        {
            _childrenTipRenderer.material.color =
                isInverse
                    ? _colorInverseMaterial
                    : _colorMaterial;
        }
    }
}