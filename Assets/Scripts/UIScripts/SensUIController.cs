using Player.Scripts;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace UIScripts
{
    public class SensitivityUIController : MonoBehaviour
    {
        [Header("Refs")]
        public CmSensController sensController;
        public CinemachineInputAxisController axisController;   // opcional (si no, usa el del sensController)
        public GameObject panel;                                 // Panel padre del Slider
        public Slider slider;                                    // El Slider de sensibilidad

        [Header("Behavior")]
        public KeyCode toggleKey = KeyCode.F10;
        public bool unlockCursorWhenOpen = true;
        public bool disableLookWhenOpen = true;

        CanvasGroup _group;

        void Reset()
        {
            if (!sensController) sensController = FindAnyObjectByType<CmSensController>();
            if (!slider) slider = GetComponentInChildren<Slider>(true);
            if (!panel && slider) panel = slider.transform.root.gameObject;
        }

        void Awake()
        {
            if (panel) _group = panel.GetComponent<CanvasGroup>();
            HidePanelImmediate();
        }

        void Start()
        {
            if (slider)
            {
                slider.SetValueWithoutNotify(sensController ? sensController.sensitivity : 3f);
                slider.onValueChanged.AddListener(OnSliderChanged);
                slider.minValue = CmSensController.MIN_SENS;
                slider.maxValue = CmSensController.MAX_SENS;
                slider.SetValueWithoutNotify(sensController.sensitivity);
            }
        }

        void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
                Toggle();
        }

        public void Toggle()
        {
            if (IsOpen()) Hide();
            else Show();
        }

        public void Show()
        {
            SetPanel(true);
            SyncFromSens();

            if (unlockCursorWhenOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (disableLookWhenOpen)
            {
                var ac = axisController ? axisController : (sensController ? sensController.axisController : null);
                if (ac) ac.enabled = false;
            }
        }

        public void Hide()
        {
            SetPanel(false);

            if (unlockCursorWhenOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (disableLookWhenOpen)
            {
                var ac = axisController ? axisController : (sensController ? sensController.axisController : null);
                if (ac) ac.enabled = true;
            }
        }

        void OnSliderChanged(float v)
        {
            if (sensController) sensController.SetSensitivity(v);
        }

        void SyncFromSens()
        {
            if (slider && sensController)
                slider.SetValueWithoutNotify(sensController.sensitivity);
        }

        bool IsOpen()
        {
            if (!panel) return false;
            if (_group) return _group.alpha > 0.5f && _group.interactable;
            return panel.activeSelf;
        }

        void SetPanel(bool open)
        {
            if (!panel) return;

            if (_group)
            {
                _group.alpha = open ? 1f : 0f;
                _group.interactable = open;
                _group.blocksRaycasts = open;
            }
            else
            {
                panel.SetActive(open);
            }
        }

        void HidePanelImmediate()
        {
            if (!panel) return;
            if (_group)
            {
                _group.alpha = 0f;
                _group.interactable = false;
                _group.blocksRaycasts = false;
            }
            else
            {
                panel.SetActive(false);
            }
        }
    }
}