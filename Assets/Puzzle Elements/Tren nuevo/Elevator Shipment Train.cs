using System.Collections.Generic;
using Player.Stasis;
using Puzzle_Elements.PlataformasCorregidas;
using UnityEngine;

namespace Puzzle_Elements.Tren_nuevo
{
    public class ElevatorShipmentTrain : MonoBehaviour, IStasis
    {
        [Header("Movimiento")]
        public bool canMove;

        [Header("Stasis Rendering")]
        [SerializeField] private List<Renderer> rends = new List<Renderer>();

        public bool IsFreezed => _isFreezed;
        private bool _isFreezed;

        public StasisEffect StasisEffect { get; private set; }

        private PistonVisualAuto _visual;
        private KinematicCargoPlatform _piston;

        private List<StasisPartElevatorShipmentTrain> list = new List<StasisPartElevatorShipmentTrain>();


        private void Awake()
        {
            _visual = GetComponent<PistonVisualAuto>();
            _piston = GetComponentInChildren<KinematicCargoPlatform>();
        }

        private void Start()
        {
            canMove = true;

            StasisEffect = new StasisEffect(null, rends.ToArray());

            list.AddRange(GetComponentsInChildren<StasisPartElevatorShipmentTrain>());
        }


        // ===========================
        //   Activar / Desactivar
        // ===========================

        public void ActivateElevatorShipment()
        {
            canMove = true;
        }

        public void DesactivateElevatorShipment()
        {
            canMove = false;
        }


        // ===========================
        //   Freeze
        // ===========================

        private void FreezeObject()
        {
            if (_isFreezed) return;

            _isFreezed = true;

            // Asegura que el piston pause su delay / movimiento
            if (_piston != null)
                _piston.stasear();

            // Efecto visual de stasis
            StasisEffect.StasisEffectStart();

            foreach (var item in list)
                item.isFreezed = true;
        }


        // ===========================
        //   UnFreeze
        // ===========================

        private void UnFreezeObject()
        {
            if (!_isFreezed) return;

            _isFreezed = false;

            if (_piston != null)
                _piston.Desestasear();  // Ahora el piston retoma correctamente incluso si estaba en delay

            StasisEffect.StasisEffectStop();

            foreach (var item in list)
                item.isFreezed = false;
        }


        // ===========================
        //   IStasis Interface
        // ===========================

        public void StatisEffectActivate()
        {
            FreezeObject();
        }

        public void StatisEffectDeactivate()
        {
            UnFreezeObject();
        }
    }
}

