using System;
using Player.Scripts.Interactor;
using Player.Scripts.MovementFSM.MVC;
using Puzzle_Elements.Hedron.Scripts;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.LaserSensor.Scripts
{
    public class SensorLaser : MonoBehaviour
    {
        [Tooltip("Lista de lasers")]
        public Laser[] lasers;

        public GameObject[] laserGameobject;
        [Header("Events")]
        [Tooltip("Eventos que se llaman cuando el laser detecta al Player")]
        public UnityEvent OnIntruderDetected;

        private bool _detectPlayer;
        private bool alreadyEventInit;


        public Action OnLaser;
        public Action OnPlayerHit;

        public bool _canKillPlayer = true;
        private void Start()
        {
            OnLaser?.Invoke();
        }
        private void Update()
        {
            if (PlayerConfirmByTrigger() && !alreadyEventInit && _canKillPlayer)
            {
                OnIntruderDetected?.Invoke();
                OnPlayerHit?.Invoke();
                alreadyEventInit = true;
                //StartCoroutine(WaitForNextEvent());
            }
            if (alreadyEventInit)
            {
                if (!PlayerConfirmByTrigger()) alreadyEventInit = false;
            }
        }
        public void CanShootLasers(bool a)
        {
            foreach (var item in lasers)
            {
                item.canShootLaserByStasis = a;
            }
        }
        public void CanKillPlayer()
        {
            _canKillPlayer = true;
            foreach (var item in laserGameobject)
            {
                if(item != null)
                {
                    item.gameObject.SetActive(true);
                }
            }
        }
        public void CantKillPlayer()
        {
            _canKillPlayer = false;
            foreach (var item in laserGameobject)
            {
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                }
            }
        }
        bool PlayerConfirmByTrigger()
        {
            return _detectPlayer;
        }
        private void OnTriggerEnter(Collider other)
        {
            if (!_canKillPlayer) return;
            Model player = other.GetComponent<Model>();
            if(player != null)
            {

                PlayerInteractor playerInteractor = player.GetComponentInChildren<PlayerInteractor>();
                if (playerInteractor._objectGrabbable != null)
                {
                    PhysicsBox box = playerInteractor._objectGrabbable.GetComponent<PhysicsBox>();
                    if (box != null)
                    {
                        playerInteractor.TryDropObject();
                        box.transform.position = box.posInitial;
                        Rigidbody rbBox = box.GetComponent<Rigidbody>();
                        if (rbBox != null)
                        {
                            rbBox.linearVelocity = Vector3.zero;
                            rbBox.useGravity = false;
                            rbBox.isKinematic = true;
                            rbBox.isKinematic = false;
                        }
                    }
                }




                Debug.Log("Player intruso");
                _detectPlayer = true;
            }



            PhysicsBox hedro = other.GetComponent<PhysicsBox>();
            if (hedro != null)
            {
                hedro.transform.position = hedro.posInitial;
                Rigidbody rb = hedro.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.useGravity = false;
                    rb.isKinematic = true;
                    rb.isKinematic = false;
                }
            }
        }
        private void OnTriggerExit(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if (player != null)
            {
                _detectPlayer = false;
            }
        }
    }
}
