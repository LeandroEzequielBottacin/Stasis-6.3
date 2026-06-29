using System.Collections;
using Managers.Game;
using UnityEngine;

namespace Puzzle_Elements.Tren_nuevo.Puerta_tren_final
{
    public class DoorTrain : MonoBehaviour
    {
        private Animator _anim;
        private bool _open;

        private void Start()
        {
            _anim = GetComponent<Animator>();
            GameManager.Instance.OnDeathPlayer += CloseDoor;
        }


        public void OpenDoor()
        {
            StartCoroutine(wait());
        }
        IEnumerator wait()
        {
            yield return new WaitForSeconds(10f);
            _open = true;
            _anim.SetBool("Open", true);
        }

        public void CloseDoor()
        {
            if (!_open) return;

            _anim.SetBool("Open", false);
            _open = false;
        }
    }
}
