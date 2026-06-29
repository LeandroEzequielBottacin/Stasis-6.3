using System.Collections;
using ObjectPool;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio.Scripts
{
    public class AudioSourceFactory : MonoBehaviour
    {
        public static AudioSourceFactory Instance;

        [Header("Prefab 3D (SFX)")]
        public AudioSource prefab;

        [Header("Pooling 3D")]
        public int stonks = 15;
        public bool dynamic = true;

        [Space]
        [Header("Prefab 2D (Music/Stingers)")]
        public AudioSource prefab2D;

        [Header("Pooling 2D")]
        public int stonks2D = 8;
        public bool dynamic2D = true;

        private ObjectPool<AudioSource> _pool3D;
        private ObjectPool<AudioSource> _pool2D;

        public bool Has2DPool => _pool2D != null;

        private void Awake()
        {
            Instance = this;

            if (!prefab)
            {
                Debug.LogError("[AudioSourceFactory] Asigná el prefab 3D (prefab).", this);
                enabled = false;
                return;
            }

            _pool3D = new ObjectPool<AudioSource>(
                factoryMethod: CreateInstance3D,
                callback: TurnOnOff,
                initialStonks: stonks,
                dynamic: dynamic
            );

            if (prefab2D)
            {
                _pool2D = new ObjectPool<AudioSource>(
                    factoryMethod: CreateInstance2D,
                    callback: TurnOnOff,
                    initialStonks: stonks2D,
                    dynamic: dynamic2D
                );
            }
            else
            {
                Debug.LogWarning("[AudioSourceFactory] Sin prefab2D: la música no podrá usar pool 2D.", this);
            }
        }

        // ---------- Core pooling ----------
        private AudioSource CreateInstance3D()
        {
            var inst = Instantiate(prefab, transform);
            return inst;
        }

        private AudioSource CreateInstance2D()
        {
            var inst = Instantiate(prefab2D, transform);
            // reforzamos 2D
            inst.spatialBlend = 0f;
            inst.playOnAwake = false;
            return inst;
        }

        private static void TurnOnOff(AudioSource src, bool on)
        {
            if (!src) return;

            src.gameObject.SetActive(on);

            if (!on)
            {
                // reset seguro al devolver al pool
                src.Stop();
                src.clip = null;
                src.loop = false;
                src.pitch = 1f;
                src.volume = 1f;
                src.panStereo = 0f;
                // NOTA: no tocamos outputAudioMixerGroup para permitir reuse controlado por caller
            }
        }

        // ---------- API 3D ----------
        public AudioSource GetSource()
        {
            return _pool3D.GetObject();
        }

        public void ReturnSource(AudioSource src)
        {
            if (!src) return;
            _pool3D.ReturnObject(src);
        }

        // ---------- API 2D ----------
        /// <summary>Obtiene un source 2D (musical) del pool y lo opcionalmente parenta.</summary>
        public AudioSource Get2DSource(Transform parent = null, AudioMixerGroup mixerOverride = null)
        {
            if (_pool2D == null)
            {
                Debug.LogWarning("[AudioSourceFactory] No hay pool 2D inicializado (asigná prefab2D).");
                return null;
            }

            var src = _pool2D.GetObject();
            if (parent) src.transform.SetParent(parent, false);
            src.transform.localPosition = Vector3.zero;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            if (mixerOverride) src.outputAudioMixerGroup = mixerOverride;
            return src;
        }

        public void Return2DSource(AudioSource src)
        {
            if (!src) return;

            src.transform.SetParent(transform, false);
            _pool2D.ReturnObject(src);
        }

        // ---------- Helpers 3D existentes ----------
        public AudioSource PlayClipAt(
            Vector3 position,
            AudioClip clip,
            float volume = 1f,
            float pitch = 1f,
            bool loop = false,
            float spatialBlend = 1f,
            AudioMixerGroup mixerOverride = null,
            bool autoReturnNonLoop = true)
        {
            if (!clip) return null;

            var src = GetSource();
            src.transform.position = position;
            SetupAndPlay(src, clip, volume, pitch, loop, spatialBlend, mixerOverride);

            if (!loop && autoReturnNonLoop)
                StartCoroutine(Co_AutoReturn(src, clip.length, pitch));

            return src;
        }

        public AudioSource PlayClipFollowing(
            Transform follow,
            AudioClip clip,
            float volume = 1f,
            float pitch = 1f,
            bool loop = false,
            float spatialBlend = 1f,
            AudioMixerGroup mixerOverride = null,
            bool autoReturnNonLoop = true)
        {
            if (!clip || !follow) return null;

            var src = GetSource();
            src.transform.position = follow.position;
            SetupAndPlay(src, clip, volume, pitch, loop, spatialBlend, mixerOverride);

            StartCoroutine(Co_FollowWhilePlaying(src, follow, loop));

            if (!loop && autoReturnNonLoop)
                StartCoroutine(Co_AutoReturn(src, clip.length, pitch));

            return src;
        }

        public AudioSource PlayOneShotAt(
            Vector3 position,
            AudioClip clip,
            float volume = 1f,
            float pitch = 1f,
            float spatialBlend = 1f,
            AudioMixerGroup mixerOverride = null,
            bool autoReturn = true)
        {
            if (!clip) return null;

            var src = GetSource();
            src.transform.position = position;

            if (mixerOverride) src.outputAudioMixerGroup = mixerOverride;

            src.spatialBlend = Mathf.Clamp01(spatialBlend);
            src.pitch = pitch;
            src.PlayOneShot(clip, Mathf.Clamp01(volume));

            if (autoReturn)
                StartCoroutine(Co_AutoReturn(src, clip.length, pitch));

            return src;
        }

        public void StopAndReturn(AudioSource src)
        {
            if (!src) return;
            src.Stop();
            ReturnSource(src);
        }

        // ---------- Internals ----------
        private void SetupAndPlay(
            AudioSource src,
            AudioClip clip,
            float volume,
            float pitch,
            bool loop,
            float spatialBlend,
            AudioMixerGroup mixerOverride)
        {
            if (mixerOverride) src.outputAudioMixerGroup = mixerOverride;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = pitch;
            src.loop = loop;
            src.spatialBlend = Mathf.Clamp01(spatialBlend);
            src.Play();
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
            ReturnSource(src);
        }

        private IEnumerator Co_FollowWhilePlaying(AudioSource src, Transform follow, bool loop)
        {
            while (src && follow && src.isPlaying)
            {
                src.transform.position = follow.position;
                yield return null;
            }
            if (!loop && src)
                ReturnSource(src);
        }
    }
}
