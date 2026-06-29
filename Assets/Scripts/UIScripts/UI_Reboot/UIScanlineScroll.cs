using UnityEngine;
using UnityEngine.UI;

namespace UIScripts.UI_Reboot
{
    [RequireComponent(typeof(Image))]
    public class UIScanlineScroll : MonoBehaviour
    {
        public float speed = -0.2f;       // negativo = hacia abajo
        public Vector2 direction = new Vector2(0f, 1f);
        private Material _mat;
        private Vector2 _offset;

        void Awake()
        {
            var img = GetComponent<Image>();
            // Material instanciado para no afectar a otros
            if (img.material != null) _mat = img.material = new Material(img.material);
            else _mat = img.material = new Material(Shader.Find("Unlit/Transparent"));        
        }

        void Update()
        {
            if (_mat == null) return;
            _offset += direction * speed * Time.unscaledDeltaTime;

            if (_mat.HasProperty("_BaseMap")) _mat.SetTextureOffset("_BaseMap", _offset);
            else if (_mat.HasProperty("_MainTex")) _mat.SetTextureOffset("_MainTex", _offset);
        }
    }
}