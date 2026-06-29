using UnityEngine;

namespace Puzzle_Elements.Hedro_conteiner.Scripts
{
    [DisallowMultipleComponent]
    public class HedronPlatformAnimatorLink : MonoBehaviour
    {
        [Header("Fuente")]
        [SerializeField] private HedronContainerIn container;

        [Header("Animator de la plataforma/cartel")]
        [SerializeField] private Animator animator;

        [Header("Modo de parámetros")]
        [Tooltip("Si true, usa triggers (OpenTrigger/CloseTrigger). Si false, usa un bool (IsOpenBool).")]
        [SerializeField] private bool useTriggers = true;

        [Header("Triggers")]
        [SerializeField] private string openTrigger = "OPEN";
        [SerializeField] private string closeTrigger = "CLOSE";

        [Header("Bool")]
        [SerializeField] private string isOpenBool = "IsOpen";

        [Header("Sincronizar al iniciar")]
        [SerializeField] private bool syncOnStart = true;

        int _openHash, _closeHash, _boolHash;

        void Reset()
        {
            container = GetComponent<HedronContainerIn>();
            animator  = GetComponentInChildren<Animator>();
        }

        void Awake()
        {
            if (useTriggers)
            {
                _openHash  = Animator.StringToHash(openTrigger);
                _closeHash = Animator.StringToHash(closeTrigger);
            }
            else
            {
                _boolHash = Animator.StringToHash(isOpenBool);
            }
        }

        void OnEnable()
        {
            if (container == null) container = GetComponent<HedronContainerIn>();
            if (container != null)
            {
                container.onHedronPlaced.AddListener(HandlePlaced);
                container.onHedronRemoved.AddListener(HandleRemoved);
            }
        }

        void Start()
        {
            if (syncOnStart && animator != null && container != null)
            {
                bool open = container.HasOccupant;
                Apply(open, instant: true);
            }
        }

        void OnDisable()
        {
            if (container != null)
            {
                container.onHedronPlaced.RemoveListener(HandlePlaced);
                container.onHedronRemoved.RemoveListener(HandleRemoved);
            }
        }

        void HandlePlaced()  => Apply(true,  instant:false);
        void HandleRemoved() => Apply(false, instant:false);

        void Apply(bool open, bool instant)
        {
            if (animator == null) return;

            if (useTriggers)
            {
                if (open) animator.SetTrigger(_openHash);
                else      animator.SetTrigger(_closeHash);
            }
            else
            {
                animator.SetBool(_boolHash, open);
                if (instant)
                {
                    // Reforzar estado sin blend si tu controller tiene una capa dedicada:
                    animator.Update(0f);
                }
            }
        }
    }
}
