using UnityEngine;

namespace Player.Scripts.MovementFSM.MVC
{
    public class Controller : IController
    {
        private Model _model;
        private View _view;
        
        public Controller(Model model, View view)
        {
            _model = model;
            _view = view;
            
            _model.OnGroundedChanged += _view.GroundedChangedEvent;
            _model.OnJumpSucceeded += _view.OnJumpEvent;
            _model.OnShoot += _view.OnShootEvent;
            _model.OnMove += _view.OnMove;
            _model.OnRun += _view.OnRun;
            _model.OnStop += _view.OnStop;
            _model.OnCrouchStart += _view.OnCrouchStartEvent;
            _model.OnCrouchEnd += _view.OnCrouchEndEvent;
            _model.OnVaultStart += _view.OnVaultStartEvent;
            _model.OnVaultEnd += _view.OnVaultEndEvent;
            _model.OnClimbStart += _view.OnClimbStartEvent;
            _model.OnClimbEnd += _view.OnClimbEndEvent;
            _model.OnSlideStart += _view.OnSlideStartEvent;
            _model.OnSlideEnd += _view.OnSlideEndEvent;
            _model.OnGetDamage += _view.OnDamageEvent;
            _model.OnDeath += _view.OnDeathEvent;
            _model.OnWallrunStart += view.OnWallrunStartEvent;
            _model.OnWallrunEnd += view.OnWallrunEndEvent;
            _model.OnTurnYaw += view.OnTurnYaw;
            _model.OnInteractFocusEnter += view.OnCanInteractEnterEvent;
            _model.OnInteractFocusExit += view.OnCanInteractExitEvent;
            _model.OnInteract += view.OnInteractEvent;
        }

        public void OnUpdate()
        {
            float xAxis    = Mathf.Clamp(Input.GetAxis("Horizontal"), -1f, 1f);
            float zAxis    = Mathf.Clamp(Input.GetAxis("Vertical"),   -1f, 1f);
            float rawXAxis = Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f);
            float rawZAxis = Mathf.Clamp(Input.GetAxisRaw("Vertical"),   -1f, 1f);

            float m = Mathf.Clamp01(_model.hazardSpeedMultiplier);

            // Escalado suave del input
            float t = 1f - m;
            float moveScale = Mathf.SmoothStep(1f, 0f, t);

            xAxis    *= moveScale;
            zAxis    *= moveScale;
            rawXAxis *= moveScale;
            rawZAxis *= moveScale;

            _model.UpdateAxisInput(xAxis, zAxis, rawXAxis, rawZAxis);

            // Run bloqueado cerca de la muerte (ligado al mismo umbral)
            float deathThreshold = 0.2f;  // mismo valor que en SmokeHazardTrigger
    
            bool runKeyPressed = Input.GetKey(_model.runningKey);
            if (m <= deathThreshold)
                runKeyPressed = false;

            _model.UpdateRunKey(runKeyPressed);

            // Saltos: no aceptamos nuevos cuando estás en zona de muerte
            if (m > deathThreshold && Input.GetKeyDown(_model.jumpKey))
            {
                _model.RegisterJumpDownThisFrame();
                _model.JumpInput();
                _model.BufferJumpNow();
            }

            if (Input.GetKeyDown(_model.mouseLeft))
            {
                _model.ShootInput();
            }
        }


    }
}
