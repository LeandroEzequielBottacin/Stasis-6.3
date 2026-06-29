using UnityEngine;

namespace Puzzle_Elements.Tren_nuevo
{
    public class CompuertasTren : MonoBehaviour
    {
        private Animator _anim;

        private void Start()
        {
            _anim = GetComponent<Animator>();
        }
        public void Open()
        {
            _anim.SetBool("Open", true);
        }
        public void Close()
        {
            _anim.SetBool("Open", false);
        }
    }
}
