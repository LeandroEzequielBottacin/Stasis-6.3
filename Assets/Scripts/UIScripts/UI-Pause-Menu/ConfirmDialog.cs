using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UIScripts.UI_Pause_Menu
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Confirm Dialog Controller")]
    public class ConfirmDialogController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private Action _onYes;
        private Action _onNo;

        void Awake()
        {
            HideImmediate();
            if (yesButton) yesButton.onClick.AddListener(OnYes);
            if (noButton)  noButton.onClick.AddListener(OnNo);
        }

        public void Show(string title, string message, Action onYes, Action onNo)
        {
            _onYes = onYes;
            _onNo  = onNo;

            if (titleText)   titleText.text = title;
            if (messageText) messageText.text = message;

            gameObject.SetActive(true);
            if (canvasGroup)
            {
                canvasGroup.alpha = 1;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
        }

        public void Hide()
        {
            if (canvasGroup)
            {
                canvasGroup.alpha = 0;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            gameObject.SetActive(false);
            _onYes = null;
            _onNo  = null;
        }

        private void HideImmediate() => Hide();

        private void OnYes()
        {
            _onYes?.Invoke();
            Hide();
        }

        private void OnNo()
        {
            _onNo?.Invoke();
            Hide();
        }
    }
}