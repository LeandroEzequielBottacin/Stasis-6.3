using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio.Scripts
{
    public class SfxManager : MonoBehaviour
    {
        public static SfxManager Instance;

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        
        public AudioSource GetSourceAt(Vector3 position, AudioMixerGroup mixerOverride = null, AudioSource template = null)
        {
            var factory = AudioSourceFactory.Instance;
            if (!factory)
            {
                Debug.LogWarning("[SFXManager] No hay AudioSourceFactory en escena. Creando source temporal.");
                var go = new GameObject("AudioSource_Temp");
                go.transform.position = position;
                var fallback = go.AddComponent<AudioSource>();
                ApplyTemplate(fallback, template);
                if (mixerOverride) fallback.outputAudioMixerGroup = mixerOverride;
                return fallback;
            }

            var src = factory.GetSource();
            src.transform.position = position;
            ApplyTemplate(src, template);

            if (mixerOverride) src.outputAudioMixerGroup = mixerOverride;

            return src;
        }

        public void Return(AudioSource src)
        {
            if (!src) return;
            var factory = AudioSourceFactory.Instance;
            if (factory) factory.ReturnSource(src);
            else src.gameObject.SetActive(false);
        }

        public AudioSource PlayAt(Vector3 position, AudioClip clip, float volume = 1f, float pitch = 1f,
                                  bool loop = false, float spatialBlend = 1f,
                                  AudioMixerGroup mixerOverride = null, AudioSource template = null,
                                  bool autoReturnNonLoop = true)
        {
            if (!clip) return null;

            var src = GetSourceAt(position, mixerOverride, template);
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = pitch;
            src.loop = loop;
            src.spatialBlend = Mathf.Clamp01(spatialBlend);
            src.Play();

            if (!loop && autoReturnNonLoop)
                StartCoroutine(Co_AutoReturn(src, clip.length, pitch));

            return src;
        }

        private IEnumerator Co_AutoReturn(AudioSource src, float clipLength, float pitch)
        {
            float t = 0f;
            float dur = clipLength / Mathf.Max(0.01f, Mathf.Abs(pitch));
            while (t < dur && src && src.isActiveAndEnabled)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Return(src);
        }

        private static void ApplyTemplate(AudioSource dst, AudioSource tpl)
        {
            if (!dst || !tpl) return;
            dst.spatialBlend = tpl.spatialBlend;
            dst.minDistance = tpl.minDistance;
            dst.maxDistance = tpl.maxDistance;
            dst.rolloffMode = tpl.rolloffMode;
            dst.dopplerLevel = tpl.dopplerLevel;
            dst.spread = tpl.spread;
            dst.priority = tpl.priority;
            dst.bypassEffects = tpl.bypassEffects;
            dst.bypassListenerEffects = tpl.bypassListenerEffects;
            dst.bypassReverbZones = tpl.bypassReverbZones;
            dst.reverbZoneMix = tpl.reverbZoneMix;
            dst.spatialize = tpl.spatialize;
            dst.panStereo = tpl.panStereo;
        }
    }
}
