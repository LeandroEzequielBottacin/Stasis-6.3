using UnityEngine;

namespace Puzzle_Elements.Spider
{
    public class SpiderMovement : MonoBehaviour
    {
        private Rigidbody _rb;
        [SerializeField] private float _forwardMoveForce = 1;
        [SerializeField] private float _turnTorque = 1;
        [SerializeField] private float _maxLinearVelocity = 4;
        [SerializeField] private float _maxAngularVelocity = 90;
        private Vector3 _currentInput = Vector3.zero;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.maxAngularVelocity = _maxAngularVelocity;
            _rb.maxLinearVelocity = _maxLinearVelocity;

        }

        private void FixedUpdate()
        {
            _rb.AddForce(transform.forward * _forwardMoveForce * Time.fixedDeltaTime * _currentInput.y, ForceMode.Acceleration);
            _rb.AddRelativeTorque(transform.up * _turnTorque * Time.fixedDeltaTime * _currentInput.x, ForceMode.Acceleration);
        }
        //    public void OnMoveInput(CallBackContext context)
        //    {
        //        _currentInput = context.ReadValue<Vector2>();
        //    }
    }
}
