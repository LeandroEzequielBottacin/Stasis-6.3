using UnityEngine;

namespace Scenes.Level.Lau.Tutorial_Assets
{
    public class PausedButtomSound : MonoBehaviour
    {
        public AudioSource uiAudio;

        void Awake()
        {
            uiAudio.ignoreListenerPause = true;
        }
    }
}

