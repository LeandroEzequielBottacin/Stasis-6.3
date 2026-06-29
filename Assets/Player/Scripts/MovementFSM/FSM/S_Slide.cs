using System;
using Player.Scripts.MovementFSM.MVC;

namespace Player.Scripts.MovementFSM
{
    public class S_Slide : IState
    {
        private FSM _fsm;
        private Model _model;

        public S_Slide(FSM fsm, Model model)
        {
            _fsm = fsm;
            _model = model;
        }

        public void OnEnter()
        {
            
        }
        
        public void OnUpdate()
        {

        }
        
        public void OnFixedUpdate()
        {

        }

        public void OnExit()
        {
        
        }

        public void OnLateUpdate()
        {
            throw new NotImplementedException();
        }
    }
}
