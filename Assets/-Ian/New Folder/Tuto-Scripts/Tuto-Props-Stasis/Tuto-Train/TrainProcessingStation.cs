using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class TrainProcessingStation : MonoBehaviour
{
    [Serializable]
    private sealed class AnimatorAction
    {
        [Tooltip("Animator controlled by this station.")]
        [SerializeField] private Animator animator;

        [Tooltip(
            "Trigger parameter used to start the animation. " +
            "Leave empty if the Animator starts automatically.")]
        [SerializeField] private string triggerName;

        [Tooltip(
            "Exact Animator state name that must finish before the train can continue.")]
        [SerializeField] private string completionStateName;

        [Tooltip("Animator layer containing the completion state.")]
        [SerializeField, Min(0)] private int layerIndex;

        public Animator Animator => animator;

        public string TriggerName => triggerName;

        public string CompletionStateName => completionStateName;

        public int LayerIndex => layerIndex;
    }

    [Header("Animations")]
    [Tooltip(
        "Animations started when the train reaches this processing station.")]
    [SerializeField]
    private List<AnimatorAction> animatorActions = new();

    [Header("Material Processing")]
    [Tooltip(
        "Change the wagon material when processing starts.")]
    [SerializeField]
    private bool changeWagonMaterial = true;

    [Header("Safety")]
    [Tooltip(
        "Maximum time the station will wait for animations. " +
        "Prevents the train from remaining blocked forever because of an Animator configuration error.")]
    [SerializeField, Min(0.1f)]
    private float animationTimeout = 30f;

    [Header("Debug")]
    [SerializeField]
    private bool logProcessing;

    private bool isProcessing;

    public bool IsProcessing => isProcessing;

    /// <summary>
    /// Runs the complete industrial processing sequence.
    /// The coroutine finishes only when the wagon is allowed to continue.
    /// </summary>
    public IEnumerator ProcessWagon(TrainWagon wagon)
    {
        if (wagon == null)
        {
            yield break;
        }

        if (isProcessing)
        {
            yield break;
        }

        isProcessing = true;

        if (logProcessing)
        {
            Debug.Log(
                $"[Train Processing] Started processing '{wagon.name}' at '{name}'.",
                this
            );
        }

        /*
         * Example:
         * laser activates, crane starts, mechanical arms move, etc.
         */
        StartAnimations();

        /*
         * Material modification can represent the Stasis Hedros
         * being extracted, released, discharged, illuminated, etc.
         */
        if (changeWagonMaterial)
        {
            wagon.ApplyProcessedMaterial();
        }

        yield return WaitForAnimations();

        isProcessing = false;

        if (logProcessing)
        {
            Debug.Log(
                $"[Train Processing] Processing completed for '{wagon.name}'.",
                this
            );
        }
    }

    private void StartAnimations()
    {
        for (int i = 0; i < animatorActions.Count; i++)
        {
            AnimatorAction action = animatorActions[i];

            if (action == null ||
                action.Animator == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(action.TriggerName))
            {
                action.Animator.SetTrigger(
                    action.TriggerName
                );
            }
        }
    }

    private IEnumerator WaitForAnimations()
    {
        if (animatorActions.Count == 0)
        {
            yield break;
        }

        float elapsedTime = 0f;

        /*
         * First wait until every configured Animator has actually
         * entered its expected state.
         */
        while (!HaveAllAnimationsEnteredExpectedState())
        {
            elapsedTime += Time.deltaTime;

            if (elapsedTime >= animationTimeout)
            {
                ReportTimeout();
                yield break;
            }

            yield return null;
        }

        /*
         * Then wait until every expected state reaches its end.
         */
        while (!HaveAllAnimationsFinished())
        {
            elapsedTime += Time.deltaTime;

            if (elapsedTime >= animationTimeout)
            {
                ReportTimeout();
                yield break;
            }

            yield return null;
        }
    }

    private bool HaveAllAnimationsEnteredExpectedState()
    {
        for (int i = 0; i < animatorActions.Count; i++)
        {
            AnimatorAction action = animatorActions[i];

            if (!IsActionConfigured(action))
            {
                continue;
            }

            Animator animator = action.Animator;

            if (action.LayerIndex >= animator.layerCount)
            {
                return false;
            }

            AnimatorStateInfo stateInfo =
                animator.GetCurrentAnimatorStateInfo(
                    action.LayerIndex
                );

            if (!stateInfo.IsName(
                    action.CompletionStateName))
            {
                return false;
            }
        }

        return true;
    }

    private bool HaveAllAnimationsFinished()
    {
        for (int i = 0; i < animatorActions.Count; i++)
        {
            AnimatorAction action = animatorActions[i];

            if (!IsActionConfigured(action))
            {
                continue;
            }

            Animator animator = action.Animator;

            AnimatorStateInfo stateInfo =
                animator.GetCurrentAnimatorStateInfo(
                    action.LayerIndex
                );

            /*
             * If the Animator has already transitioned away from the
             * expected state, we consider that animation completed.
             */
            if (!stateInfo.IsName(
                    action.CompletionStateName))
            {
                continue;
            }

            if (animator.IsInTransition(
                    action.LayerIndex))
            {
                return false;
            }

            if (stateInfo.normalizedTime < 1f)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsActionConfigured(
        AnimatorAction action)
    {
        return action != null &&
               action.Animator != null &&
               !string.IsNullOrWhiteSpace(
                   action.CompletionStateName
               );
    }

    private void ReportTimeout()
    {
        Debug.LogWarning(
            $"{nameof(TrainProcessingStation)} '{name}' reached the " +
            $"{animationTimeout:F1}s animation timeout. " +
            "The train will be released to prevent the system from locking permanently.",
            this
        );
    }

    private void OnValidate()
    {
        animationTimeout =
            Mathf.Max(
                0.1f,
                animationTimeout
            );
    }
}