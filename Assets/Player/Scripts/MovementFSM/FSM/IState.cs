namespace Player.Scripts.MovementFSM
{
    public interface IState
    {
        void OnEnter();
        void OnUpdate();
        void OnFixedUpdate();
        void OnExit();
        void OnLateUpdate();
    }
}
