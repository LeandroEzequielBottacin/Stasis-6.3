using _Ian.VFX.Smoke;
using Managers.Game;
using System;
using System.Collections;
using UnityEngine;
using static _Ian.VFX.Smoke.HazardController;

// =========================================================
// SLOW HAZARD
// =========================================================

[Serializable]
public class SlowHazardModule : HazardModule
{
    [Header("Ralentización")]
    [Tooltip("Tiempo para pasar desde el multiplicador actual hasta el mínimo.")]
    [Min(0.1f)]
    public float slowdownDuration = 1.2f;

    [Tooltip("Valor mínimo del multiplicador de velocidad.")]
    [Range(0.01f, 0.3f)]
    public float minMultiplier = 0.05f;

    [Tooltip("Por debajo de este valor comienza a contar el tiempo para morir.")]
    [Range(0.05f, 0.5f)]
    public float deathMultiplierThreshold = 0.2f;

    [Header("Muerte diferida")]
    [Min(0f)]
    public float deathDelay = 0.8f;

    [Min(0f)]
    public float repeatCooldown = 0.25f;

    [Header("Comportamiento al salir")]
    public bool resetMultiplierOnExit = true;

    [Header("Freno al entrar")]
    public bool applyEntryBrake = true;

    [Range(0f, 1f)]
    public float entryHorizontalVelocityScale = 0.75f;

    [NonSerialized]
    private bool _targetInside;

    [NonSerialized]
    private IHazardSlowTarget _target;

    [NonSerialized]
    private Rigidbody _rigidbody;

    [NonSerialized]
    private Coroutine _slowRoutine;

    [NonSerialized]
    private float _nextAllowedDeathTime;

    [NonSerialized]
    private float _currentMultiplier = 1f;

    public override void OnEnter(
        HazardController controller,
        HazardEntry hazard,
        Collider other)
    {
        IHazardSlowTarget target = FindSlowTarget(other);

        if (target == null)
            return;

        _target = target;
        _targetInside = true;

        _currentMultiplier = Mathf.Clamp01(
            _target.GetExternalSpeedMultiplier()
        );

        _rigidbody = other.attachedRigidbody;

        if (_rigidbody == null)
            _rigidbody = other.GetComponentInParent<Rigidbody>();

        if (applyEntryBrake)
            ApplyEntryBrake();

        StartSlowdown(controller);
    }

    public override void OnStay(
        HazardController controller,
        HazardEntry hazard,
        Collider other)
    {
        if (_target != null)
        {
            _targetInside = true;
            return;
        }

        IHazardSlowTarget target = FindSlowTarget(other);

        if (target == null)
            return;

        _target = target;
        _targetInside = true;
        _currentMultiplier = Mathf.Clamp01(
            target.GetExternalSpeedMultiplier()
        );

        StartSlowdown(controller);
    }

    public override void OnExit(
        HazardController controller,
        HazardEntry hazard,
        Collider other)
    {
        IHazardSlowTarget exitingTarget = FindSlowTarget(other);

        if (exitingTarget == null || exitingTarget != _target)
            return;

        _targetInside = false;

        StopSlowdown(controller);

        if (resetMultiplierOnExit && _target != null)
        {
            _target.SetExternalSpeedMultiplier(1f);
            _currentMultiplier = 1f;
        }

        _target = null;
        _rigidbody = null;
    }

    public override void Disable(HazardController controller, HazardEntry hazard)
    {
        _targetInside = false;

        StopSlowdown(controller);

        if (resetMultiplierOnExit && _target != null)
            _target.SetExternalSpeedMultiplier(1f);

        _currentMultiplier = 1f;
        _target = null;
        _rigidbody = null;
    }

    public override float GetDanger()
    {
        return Mathf.Clamp01(1f - _currentMultiplier);
    }

    private IHazardSlowTarget FindSlowTarget(Collider other)
    {
        MonoBehaviour[] components =
            other.GetComponentsInParent<MonoBehaviour>();

        foreach (MonoBehaviour component in components)
        {
            if (component is IHazardSlowTarget target)
                return target;
        }

        return null;
    }

    private void ApplyEntryBrake()
    {
        if (_rigidbody == null)
            return;

        Vector3 velocity = _rigidbody.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

        horizontal *= entryHorizontalVelocityScale;

        _rigidbody.linearVelocity =
            horizontal + Vector3.up * velocity.y;
    }

    private void StartSlowdown(HazardController controller)
    {
        if (_slowRoutine != null || _target == null)
            return;

        _slowRoutine = controller.RunCoroutine(SlowdownRoutine());
    }

    private void StopSlowdown(HazardController controller)
    {
        if (_slowRoutine == null)
            return;

        controller.StopHazardCoroutine(_slowRoutine);
        _slowRoutine = null;
    }

    private IEnumerator SlowdownRoutine()
    {
        if (_target == null)
        {
            _slowRoutine = null;
            yield break;
        }

        float time = 0f;
        float deathTimer = 0f;

        float start = Mathf.Clamp01(
            _target.GetExternalSpeedMultiplier()
        );

        float targetMin = Mathf.Clamp01(minMultiplier);

        while (_targetInside && _target != null)
        {
            time += Time.deltaTime;

            float normalized = slowdownDuration > 0f
                ? Mathf.Clamp01(time / slowdownDuration)
                : 1f;

            float multiplier = Mathf.Lerp(
                start,
                targetMin,
                normalized
            );

            multiplier = Mathf.Clamp01(multiplier);

            _currentMultiplier = multiplier;

            _target.SetExternalSpeedMultiplier(multiplier);

            if (multiplier <= deathMultiplierThreshold)
            {
                deathTimer += Time.deltaTime;

                if (deathTimer >= deathDelay)
                {
                    if (GameManager.Instance != null &&
                        Time.time >= _nextAllowedDeathTime)
                    {
                        GameManager.Instance.PlayerDeath();

                        _nextAllowedDeathTime =
                            Time.time + repeatCooldown;
                    }

                    break;
                }
            }
            else
            {
                deathTimer = 0f;
            }

            yield return null;
        }

        _slowRoutine = null;
    }
}

