using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle_Elements.IK.Scripts
{
    public class ChangeDirectionArms : MonoBehaviour
    {
        public List<FollowTargetController> followTargetControllers;
        //public Material yellow, green;
        private bool _alreadyCollision;

        private int _layerDefault;
        private int _layerInvisible;

        private List<Transform> transformsTipControllers = new List<Transform>();
        [SerializeField] private float delay = 0.3f;


    
        private void Start()
        {
            _layerDefault = LayerMask.NameToLayer("Walkable");
            _layerInvisible = LayerMask.NameToLayer("Invisible");

        }
        //private void OnTriggerEnter(Collider other)
        //{
        //    if (_alreadyCollision) return;
        //    Debug.Log("Entre en colision");
        //    foreach (var item in followTargetControllers)
        //    {
        //        item.ChangePosition();
        //    }
        //    _alreadyCollision = true;
        //    //ChangeMaterial();
        //}
        public void EventButton()
        {
            foreach (var item in followTargetControllers)
            {
                item.ChangePosition();
            }
            _alreadyCollision = true;
            StartCoroutine(wait());
        }
        IEnumerator wait()
        {
            yield return new WaitForSeconds(1f);
            _alreadyCollision = false;
        }
        private void OnTriggerExit(Collider other)
        {
            _alreadyCollision = false;
        }
        public void ChangeDirection()
        {
            if (_alreadyCollision) return;
            Debug.Log("Entre en colision");
            foreach (var item in followTargetControllers)
            {
                item.ChangePosition();
            }
            _alreadyCollision = true;
            //InitPath();
        }
        public void InitPath()
        {
            foreach (var item in followTargetControllers)
            {
                Transform firstChild = item.currentTip.GetChild(0);
                firstChild.gameObject.layer = _layerInvisible;
            }
            transformsTipControllers.Clear();
            foreach (var item in followTargetControllers)
            {
                Transform firstChild = item.currentTip.GetChild(0);
                transformsTipControllers.Add(firstChild);
            }
            TogglePath();
        }
        //async void ChangeMaterial()
        //{
        //    GetComponent<Renderer>().material = yellow;
        //    await Task.Delay(1000);
        //    GetComponent<Renderer>().material = green;
        //}


        [ContextMenu("TogglePath")] // pod�s llamarlo con bot�n derecho en el inspector
        public void TogglePath()
        {
            StopAllCoroutines(); // por si estaba corriendo antes
            StartCoroutine(SwitchPathCoroutine());
        }

        private IEnumerator SwitchPathCoroutine()
        {
            foreach (var obj in transformsTipControllers)
            {
                if (obj != null)
                {
                    // alterna entre Default e Invisible
                    obj.gameObject.layer = (obj.gameObject.layer== _layerDefault) ? _layerInvisible : _layerDefault;
                }

                yield return new WaitForSeconds(delay);
            }
        }
    }
}
