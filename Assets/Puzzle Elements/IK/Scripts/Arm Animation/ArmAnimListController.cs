using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Puzzle_Elements.IK.Scripts.Arm_Animation
{
    public class ArmAnimListController : MonoBehaviour
    {
        [Tooltip("Lista de controladores de animaci�n de los brazos.")]
        public List<ArmAnimController> armList;

        [Tooltip("Posici�n m�nima que puede alcanzar el brazo (valor normalizado entre 0 y 1).")]
        [Range(0, 1)] public float posMin;

        [Tooltip("Posici�n m�xima que puede alcanzar el brazo (valor normalizado entre 0 y 1).")]
        [Range(0, 1)] public float posMax = 1f;

        [Tooltip("Intensidad m�nima del efecto de 'shake' (valor normalizado entre 0 y 1).")]
        [Range(0, 1)] public float shakeMin;

        [Tooltip("Intensidad m�xima del efecto de 'shake' (valor normalizado entre 0 y 1).")]
        [Range(0, 1)] public float shakeMax = 1f;

        [Tooltip("Separaci�n de fase base entre cada brazo.")]
        public float offset;

        [Tooltip("Amplitud adicional de la fase por �ndice de brazo.")]
        public float amp;

        // Tiempo LOCAL por brazo (solo avanza cuando NO est� en stasis)
        private List<float> localTimes = new List<float>();

        private void Start()
        {
            offset = (armList != null && armList.Count > 0) ? 1f / armList.Count : 0f;
            SyncLocalTimes(reset: true);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            SyncLocalTimes();
        }

        private void Update()
        {
            CalculatePosAndShake();
        }

        private void SyncLocalTimes(bool reset = false)
        {
            int n = armList != null ? armList.Count : 0;
            if (reset || localTimes.Count != n)
            {
                localTimes.Clear();
                for (int i = 0; i < n; i++)
                {
                    // Arranca alineado con Time.time para mantener la fase inicial
                    localTimes.Add(Time.time);
                }
            }
        }

        private void CalculatePosAndShake()
        {
            if (armList == null) return;
            if (localTimes.Count != armList.Count) SyncLocalTimes();

            for (int i = 0; i < armList.Count; i++)
            {
                var arm = armList[i];
                if (!arm) continue;

                // Si est� staseado: NO avanza tiempo, NO recalcula pos,
                // y fuerza el shake a 0 para eliminar vibraci�n.
                if (arm.IsFreezed)
                {
                    arm.shake = 0f; // <- fix anti-vibraci�n
                    continue;
                }

                // Avanza solo cuando no est� staseado
                localTimes[i] += Time.deltaTime;

                // Misma f�rmula que ten�as, pero con tiempo local
                float t = localTimes[i] + offset;   // si quer�s por �ndice, podr�as usar offset * i
                t = math.cos(t + amp * i);
                t = t * 0.5f + 0.5f;

                float shake = math.remap(0f, 1f, shakeMin, shakeMax, t);
                float pos = math.remap(0f, 1f, posMin, posMax, t);

                UpdateArm(i, shake, pos);
            }
        }

        private void UpdateArm(int index, float shake, float pos)
        {
            armList[index].position = pos;
            armList[index].shake = shake;
        }
    }
}
