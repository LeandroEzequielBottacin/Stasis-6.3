using UnityEngine;

namespace Scripts_Menu_Folder
{
    [RequireComponent(typeof(AudioSource))]
    public class BackgroundMusic : MonoBehaviour
    {
        private AudioSource audioSource;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.loop = true;  
        }

        void Start()
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
    }
}

