using System.Collections.Generic;

namespace Player.Scripts.MovementFSM
{
    public class FSM
    {
        public enum States
        {
            Grounded,
            Wallrun,
            Vault,
            Climb,
            Slide,
            Air
        }

        private readonly Dictionary<States, IState> _states = new Dictionary<States, IState>();

        IState _currentState;

        public void CreateState(States newState, IState state)
        {
            _states.TryAdd(newState, state);
        }

        public void ChangeState(States state)
        {
            if (_states.ContainsKey(state))
            {
                if(_currentState != null)
                {
                    _currentState.OnExit();
                    _currentState = _states[state];
                    _currentState.OnEnter();
                }
                else
                {
                    _currentState = _states[state];
                    _currentState.OnEnter();
                }
            
            }

        }

        public void ArtificialUpdate()
        {
            _currentState?.OnUpdate();
        }
        
        public void ArtificialFixedUpdate()
        {
            _currentState?.OnFixedUpdate();
        }
        
        public void ArtificialLateUpdate()
        {
            _currentState?.OnLateUpdate();
        }
    }
}
