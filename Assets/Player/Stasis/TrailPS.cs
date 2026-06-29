using System;
using System.Collections;
using UnityEngine;

namespace Player.Stasis
{
    public class TrailPS : MonoBehaviour
    {
        public ParticleSystem ps;
        public float lingerTime = 0.35f;   // tiempo que permanece el trazo
        public float fadeDuration = 0.25f; // desvanecer material
        public string intensityProp = "_Multiplier"; // o "_ViewFade"

        Material _matInst;
        float _initialIntensity = 1f;

        void Awake()
        {
            if (!ps) ps = GetComponentInChildren<ParticleSystem>();
            var r = ps.GetComponent<Renderer>();
            if (r && r.sharedMaterial)
            {
                _matInst = Instantiate(r.sharedMaterial); // instancia para este beam
                r.material = _matInst;
                if (_matInst.HasProperty(intensityProp))
                    _initialIntensity = _matInst.GetFloat(intensityProp);
            }
        }

        public void PlayOnce()
        {
            ps.Clear(true);
            ps.Play(true);
        }

        public void StopWithLinger(Action onDone = null)
        {
            StartCoroutine(CoStop(onDone));
        }

        IEnumerator CoStop(Action onDone)
        {
            // Deja de emitir; los trails quedan porque Trails->WorldSpace está ON
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            // Espera linger
            if (lingerTime > 0) yield return new WaitForSeconds(lingerTime);

            // Fade de material
            float t = 0;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.SmoothStep(0, 1, t / Mathf.Max(0.0001f, fadeDuration));
                if (_matInst && _matInst.HasProperty(intensityProp))
                    _matInst.SetFloat(intensityProp, _initialIntensity * k);
                yield return null;
            }

            // Espera a que mueran las partículas restantes (por seguridad)
            var main = ps.main;
            yield return new WaitForSeconds(main.startLifetime.constantMax);

            onDone?.Invoke();
            Destroy(gameObject);
        }
    }
}