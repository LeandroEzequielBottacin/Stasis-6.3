using System.Collections.Generic;
using Puzzle_Elements.IK.Scripts;
using UnityEngine;

namespace Puzzle_Elements.Conteiners_with_arms
{
    public class StasisConectionTipControllerWithCargo : MonoBehaviour
    {
        [SerializeField] private List<StasisTipController> _tipControllers;
        [SerializeField] private StasisConteinerWithArms _stasisConteinerWithArms;
        public void Notify(bool isTip, bool isFreezed, StasisTipController tip = null, StasisConteinerWithArms conteiner = null)
        {
            // Seguridad b�sica
            if (_tipControllers == null) return;

            if (isTip)
            {
                if (_stasisConteinerWithArms != null)
                {
                    if (isFreezed)
                    {
                        if (!_stasisConteinerWithArms.isFreezed)
                            _stasisConteinerWithArms.FreezeObject();
                    }
                    else
                    {
                        if (_stasisConteinerWithArms.isFreezed)
                            _stasisConteinerWithArms.UnfreezeObject();
                    }
                }

                foreach (var item in _tipControllers)
                {
                    if (item == null || item == tip) continue; // no tocar el emisor

                    if (isFreezed)
                    {
                        if (!item.IsFreezed) item.FreezeObject();
                    }
                    else
                    {
                        if (item.IsFreezed) item.UnfreezeObject();
                    }
                }
            }
            else
            {
                bool desired = isFreezed;

                foreach (var item in _tipControllers)
                {
                    if (item == null) continue;

                    if (desired)
                    {
                        if (!item.IsFreezed) item.FreezeObject();
                    }
                    else
                    {
                        if (item.IsFreezed) item.UnfreezeObject();
                    }
                }
            }
        }
    }
}
