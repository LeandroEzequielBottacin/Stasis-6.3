using System;
using System.Collections;
using Puzzle_Elements.AllInterfaces;
using UnityEngine;

namespace Puzzle_Elements.Door.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class Door : MonoBehaviour, IButtonActivator, IPlateActivator
    {
        public Transform leftDoor;
        public Transform rightDoor;
        public Transform[] gears;

        public float leftSlideDistance = 2f;
        public float rightSlideDistance = 2f;
        public float slideDuration = 1f;
        public float gearRotationAngle = 180f;
        public float gearRotateDuration = 0.5f;

        public string gearSoundName = "GearTurn";
        public string openSoundName = "Door.OPEN";
        public string closeSoundName = "Door.CLOSE";

        public bool isOpen;
        private Coroutine _currentSequence;

        public bool autoClose = true;
        public bool autoOpen;
        public bool playerCanOpen;
        public float autoCloseDelay = 1f;

        public bool hasTimedClose;
        public float timedCloseDelay = 5f;

        private Vector3 _leftClosedPos, _rightClosedPos;

        [SerializeField] private ParticleSystem[] particlesDoor = new ParticleSystem[3];

        public Action OnGear;
        public Action OnDoor;

        private void Start()
        {
            _leftClosedPos = leftDoor.localPosition;
            _rightClosedPos = rightDoor.localPosition;

            var col = GetComponent<Collider>();
            col.isTrigger = true;

            if (autoOpen)
            {
                OpenDoor();
            }
        }

        public void ToggleDoor()
        {
            SetDoorState(!isOpen);
        }

        public void OpenDoor()
        {
            SetDoorState(true);
        }

        public void CloseDoor()
        {
            SetDoorState(false);
        }

        private void SetDoorState(bool open)
        {
            if (_currentSequence != null)
                StopCoroutine(_currentSequence);

            isOpen = open;
            _currentSequence = StartCoroutine(RunDoorSequence(isOpen));
        }

        private IEnumerator RunDoorSequence(bool opening)
        {
            if (opening)
            {
                foreach (var gear in gears)
                {
                    //EventManager.TriggerEvent(gearSoundName, gameObject);
                    OnGear?.Invoke();
                    yield return RotateGear(gear, true);
                }

                //EventManager.TriggerEvent(openSoundName, gameObject);
                OnDoor?.Invoke();
                yield return SlideDoors(true);

                if (hasTimedClose)
                    _currentSequence = StartCoroutine(AutoCloseRoutine(timedCloseDelay));
            }
            else
            {
                //EventManager.TriggerEvent(closeSoundName, gameObject);
                OnDoor?.Invoke();
                yield return SlideDoors(false);

                foreach (var gear in gears)
                {
                    //EventManager.TriggerEvent(gearSoundName, gameObject);
                     OnGear?.Invoke();
                    yield return RotateGear(gear, false);
                }
            }
        }

        private IEnumerator AutoCloseRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            SetDoorState(false);
        }

        private IEnumerator RotateGear(Transform gear, bool opening)
        {
            float elapsed = 0f;
            float from = gear.localEulerAngles.z;
            float to = opening ? from + gearRotationAngle : from - gearRotationAngle;

            while (elapsed < gearRotateDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / gearRotateDuration);
                float angle = Mathf.Lerp(from, to, t);
                gear.localEulerAngles = new Vector3(gear.localEulerAngles.x, gear.localEulerAngles.y, angle);
                yield return null;
            }

            gear.localEulerAngles = new Vector3(gear.localEulerAngles.x, gear.localEulerAngles.y, to);
        }

        private IEnumerator SlideDoors(bool opening)
        {
            Vector3 leftStart = leftDoor.localPosition;
            Vector3 rightStart = rightDoor.localPosition;

            Vector3 leftEnd = opening ? _leftClosedPos + Vector3.right * leftSlideDistance : _leftClosedPos;
            Vector3 rightEnd = opening ? _rightClosedPos + Vector3.left * rightSlideDistance : _rightClosedPos;

            foreach (var particle in particlesDoor)
                particle?.Play();

            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
                leftDoor.localPosition = Vector3.Lerp(leftStart, leftEnd, t);
                rightDoor.localPosition = Vector3.Lerp(rightStart, rightEnd, t);
                yield return null;
            }

            leftDoor.localPosition = leftEnd;
            rightDoor.localPosition = rightEnd;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (!playerCanOpen) return;
            
            if (!isOpen && !hasTimedClose)
                OpenDoor();
        }
    }
}