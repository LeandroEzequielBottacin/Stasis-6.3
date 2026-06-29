using TMPro;
using UnityEngine;

namespace URP_Renderer
{
    public class SetQuality : MonoBehaviour
    {
        public void SetVisualQuality(int index)
        {
            QualitySettings.SetQualityLevel(index);
        }
    }
}
