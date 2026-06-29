using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle_Elements.LaserSensor.Scripts
{
    public class Laser : MonoBehaviour
    {
        [Header("Settings")]
        public LayerMask mirrorMask;
        public LineRenderer line;
        public float maxDistance = 128;
        public int maxBounces = 128;
        public bool checkForIntruders;
        public bool intruderConfirm;
        public bool otherDetectIntruder;
        public LayerMask intruderMask;

        [Header("State")]
        readonly List<Vector3> _linePoints = new();

        [Header("Colors")]
        public Color defaultColor;
        public Color intruderConfirmColor;

        [HideInInspector] public bool canInvokeEvent = true;
        [HideInInspector] public bool canShootLaserByStasis = true;

        [Header("Fade Settings")]
        public float fadeDuration = 0.5f;
        private Coroutine fadeCoroutine;
        private bool lastShootState;

        private void Start()
        {
            defaultColor = line.startColor;
            lastShootState = canShootLaserByStasis;
        }

        private void Update()
        {
            // Detectar cambio de estado y disparar fade solo una vez
            if (lastShootState != canShootLaserByStasis)
            {
                if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
                fadeCoroutine = StartCoroutine(FadeLaser(canShootLaserByStasis));
                lastShootState = canShootLaserByStasis;
            }

            // Si est� staseado no actualizar los puntos del laser
            if (!canShootLaserByStasis)
            {
                line.positionCount = 0; // �Esto asegura que desaparezca por completo!
                return;
            }

            // Actualizar rayos normalmente
            intruderConfirm = false;
            _linePoints.Clear();
            _linePoints.Add(transform.position);
            ShootLaser(transform.position, transform.forward, maxBounces);

            line.positionCount = _linePoints.Count;
            line.SetPositions(_linePoints.ToArray());

            // Cambiar color RGB sin tocar alpha
            UpdateColor();
        }

        private void UpdateColor()
        {
            Color current = line.startColor;
            Color target = (intruderConfirm || otherDetectIntruder) ? intruderConfirmColor : defaultColor;
            target.a = current.a;
            line.startColor = target;
            line.endColor = target;
        }

        void ShootLaser(Vector3 position, Vector3 direction, int bounceLimit)
        {
            if (bounceLimit == 0) return;

            --bounceLimit;
            direction.Normalize();

            if (!Physics.Raycast(position, direction, out RaycastHit hit, maxDistance))
            {
                _linePoints.Add(position + direction * maxDistance);
                canInvokeEvent = true;
                return;
            }

            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Player"))
                intruderConfirm = true;

            _linePoints.Add(hit.point);
            var target = hit.collider.gameObject;

            if (checkForIntruders && IsObjectIntruder(target))
            {
                IntruderDetected();
                return;
            }

            if (IsObjectMirror(target))
            {
                Vector3 reflected = Vector3.Reflect(direction, hit.normal);
                ShootLaser(hit.point, reflected, bounceLimit);
            }
        }

        bool IsObjectMirror(GameObject target)
        {
            return (mirrorMask.value & (1 << target.layer)) != 0;
        }

        bool IsObjectIntruder(GameObject target)
        {
            return (intruderMask.value & (1 << target.layer)) != 0;
        }

        void IntruderDetected()
        {
            Debug.Log("INTRUDER FOUND");
        }

        private IEnumerator FadeLaser(bool fadeIn)
        {
            float elapsed = 0f;
            Color start = line.startColor;
            Color target = fadeIn
                ? new Color(start.r, start.g, start.b, 1f)
                : new Color(start.r, start.g, start.b, 0f);

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                Color c = Color.Lerp(start, target, t);

                // Mantener RGB seg�n intruderConfirm sin sobrescribir alpha
                if (intruderConfirm || otherDetectIntruder)
                {
                    c.r = intruderConfirmColor.r;
                    c.g = intruderConfirmColor.g;
                    c.b = intruderConfirmColor.b;
                }
                else
                {
                    c.r = defaultColor.r;
                    c.g = defaultColor.g;
                    c.b = defaultColor.b;
                }

                line.startColor = c;
                line.endColor = c;

                yield return null;
            }

            line.startColor = target;
            line.endColor = target;

            // Si alpha es 0, limpiar los puntos para desaparecer
            if (!fadeIn)
                line.positionCount = 0;

            fadeCoroutine = null;
        }
    }
}
