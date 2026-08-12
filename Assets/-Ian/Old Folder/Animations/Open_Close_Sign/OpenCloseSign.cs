using UnityEngine;

namespace _Ian.Animations.Open_Close_Sign
{
    public class OpenCloseSign : MonoBehaviour
    {
        private Animator _anim;
        // Start is called before the first frame update
        void Start()
        {
            _anim = GetComponent<Animator>();
        }

        public void OpenSign() => _anim.SetBool("Open",true);
        public void CloseSign() => _anim.SetBool("Open",false);
    }
}
