using UnityEngine;

namespace Dodecaedro
{
    [RequireComponent(typeof(LineRenderer))]
    public class LineRendererAnimator : MonoBehaviour
    {
        [Header("Animaci�n del LineRenderer")]
        [Tooltip("Velocidad de desplazamiento de la textura.")]
        public float scrollSpeed = 2f;

        [Tooltip("Direcci�n del desplazamiento de la textura.")]
        public Vector2 scrollDirection = Vector2.right;

        private LineRenderer lineRenderer;
        private Material lineMaterial;
        private Vector2 currentOffset;

        void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer.material != null)
            {
                // Instancia el material para evitar modificar el material original
                lineMaterial = Instantiate(lineRenderer.material);
                lineRenderer.material = lineMaterial;
            }
            else
            {
                Debug.LogWarning("El LineRenderer no tiene un material asignado.");
            }
        }

        //void Update()
        //{
        //    if (lineMaterial != null)
        //    {
        //        currentOffset += scrollDirection.normalized * scrollSpeed * Time.deltaTime;
        //        lineMaterial.SetTextureOffset("_MainTex", currentOffset);
        //    }
        //}
    }
}
