using Unity.Cinemachine;
using UnityEngine;

namespace Player.Scripts
{
    public class CmSensController : MonoBehaviour
    {
        [Header("Refs")]
        public CinemachineInputAxisController axisController;

        public const float MIN_SENS = 4f;
        public const float MAX_SENS = 15f;

        [Header("Sensitivity")]
        [Range(MIN_SENS, MAX_SENS)] public float sensitivity = 3f;

        const string PREF_KEY = "MouseSensitivity";

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Start()
        {
            if (PlayerPrefs.HasKey(PREF_KEY))
                sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(PREF_KEY, sensitivity), MIN_SENS, MAX_SENS);

            Apply();
        }

        void Reset()
        {
            if (!axisController) axisController = GetComponent<CinemachineInputAxisController>();
        }

        public void SetSensitivity(float newValue)
        {
            sensitivity = Mathf.Clamp(newValue * MAX_SENS, MIN_SENS, MAX_SENS);
            PlayerPrefs.SetFloat(PREF_KEY, sensitivity);
            PlayerPrefs.Save();
            Apply();
        }

        [ContextMenu("Apply Now")]
        public void Apply()
        {
            if (!axisController) return;

            var controllers = axisController.Controllers;
            for (int i = 0; i < controllers.Count; i++)
            {
                var c = controllers[i];
                c.Input.LegacyGain = (c.Input.LegacyGain >= 0f ? 20f : -20f) * sensitivity;
                controllers[i] = c;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sensitivity = Mathf.Clamp(sensitivity, MIN_SENS, MAX_SENS);
        }
#endif
    }
}