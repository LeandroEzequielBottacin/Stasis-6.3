using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts_Menu_Folder
{
    [RequireComponent(typeof(AudioSource))]
    public class RandomBackgroundMusic : MonoBehaviour
    {
        [Header("Audio Clips")]
        [Tooltip("Lista de audios para reproducir aleatoriamente")]
        public List<AudioClip> audioClips = new List<AudioClip>();

        [Header("Intervalo de Tiempo (segundos)")]
        [Tooltip("Tiempo mínimo entre cada audio")]
        public float minInterval = 5f;
        [Tooltip("Tiempo máximo entre cada audio")]
        public float maxInterval = 10f;

        private AudioSource audioSource;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.loop = false; 
        }

        void Start()
        {
            if (audioClips.Count > 0)
            {
                StartCoroutine(PlayRandomClips());
            }
            else
            {
                Debug.LogWarning("No hay AudioClips asignados en " + gameObject.name);
            }
        }

        IEnumerator PlayRandomClips()
        {
            float initialDelay = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(initialDelay);

            while (true)
            {
                AudioClip clip = audioClips[Random.Range(0, audioClips.Count)];

                audioSource.clip = clip;
                audioSource.Play();

                float waitTime = clip.length + Random.Range(minInterval, maxInterval);
                yield return new WaitForSeconds(waitTime);
            }
        }
    }
}


