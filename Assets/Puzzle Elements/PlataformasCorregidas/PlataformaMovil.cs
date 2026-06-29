using System.Collections;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Puzzle_Elements.PlataformasCorregidas
{
    [RequireComponent(typeof(Collider))]
    public class PlataformaMovil : MonoBehaviour
    {
        [Tooltip("Tiempo de gracia para enganchar otra plataforma antes de desparentar.")]
        [SerializeField] private float unparentDelay = 2f;
        private void OnTriggerStay(Collider other)
        {
            if (other.GetComponent<Model>() != null)
            {
                // Parent inmediato a ESTA plataforma (manteniendo posici�n mundial)
                other.transform.SetParent(transform, true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var model = other.GetComponent<Model>();
            if (model == null) return;

            // Solo desparentar si el objeto que sale es hijo directo de ESTE objeto
            if (other.transform.parent == transform)
            {

                other.transform.SetParent(null, true);
                Debug.Log("cambio");
               // StartCoroutine(UnparentAfterDelay(other.transform, unparentDelay));
            }
        }

        private IEnumerator UnparentAfterDelay(Transform target, float delay)
        {
            // Guardamos referencia al padre �candidato� (esta plataforma)
            Transform expectedParent = transform;

            float t = 0f;
            while (t < delay)
            {
                // Si durante la espera el target ya cambi� de padre,
                // abortamos (se enganch� a otra plataforma).
                if (target == null || target.parent != expectedParent)
                    yield break;

                t += Time.deltaTime;
                yield return null;
            }

            // Si sigue siendo hijo de esta plataforma, reci�n ah� lo soltamos.
            if (target != null && target.parent == expectedParent)
                target.SetParent(null, true);
        }
    }
}
