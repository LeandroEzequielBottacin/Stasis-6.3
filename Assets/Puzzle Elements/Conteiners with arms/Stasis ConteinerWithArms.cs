using System.Collections.Generic;
using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.Conteiners_with_arms
{
    public class StasisConteinerWithArms : MonoBehaviour,IStasis
    {
        public StasisConectionTipControllerWithCargo _stasisConection;
        public bool IsFreezed => isFreezed;

        public StasisEffect StasisEffect { get; private set; }

        public bool isFreezed;

        public Material matStasis;
        public readonly string _outlineThicknessName = "_BorderThickness";
        public MaterialPropertyBlock _mpb;
        private Renderer _rend;
        [SerializeField] private List<Renderer> _renders = new List<Renderer>();

        private void Start()
        {
            _mpb = new MaterialPropertyBlock();
        }
        public void StatisEffectActivate()
        {
            FreezeObject();
            if (_stasisConection != null)
            {
                //_stasisConection.Conection(false,null,this);
                _stasisConection.Notify(true, isFreezed, null, this);
            }
        }

        public void StatisEffectDeactivate()
        {
            UnfreezeObject();
            if (_stasisConection != null)
            {
                //_stasisConection.Conection(false,null,this);
                _stasisConection.Notify(true, isFreezed, null, this);
            }
        }

        public void FreezeObject()
        {
            if (!isFreezed)
            {
                isFreezed = true;
                //splineAnimate.Pause();
                SetOutlineThickness(1.05f);
                SetColorOutline(Color.green, 1f);
            }
        }

        public void UnfreezeObject()
        {

            if (!isFreezed) return;
            isFreezed = false;
            //splineAnimate.Play();
            SetOutlineThickness(0f);
            Color lightGreen = new Color(0.6f, 1f, 0.6f);
            SetColorOutline(lightGreen, 1f);
        }
        public void SetOutlineThickness(float thickness)
        {
            foreach (var rend in _renders)
            {
                rend.GetPropertyBlock(_mpb);
                _mpb.SetFloat(_outlineThicknessName, thickness);
                rend.SetPropertyBlock(_mpb);
            }
        }

        public void SetColorOutline(Color color, float alpha)
        {
            foreach (var rend in _renders)
            {
                rend.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Color", color);
                rend.SetPropertyBlock(_mpb);
            }
        }
    }
}
