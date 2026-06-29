using System.Threading.Tasks;
using UnityEngine;

namespace Audio.MusicSystem
{
    public class LayerPlayer
    {
        readonly AudioSource _a, _b;
        bool _useA = true;

        public LayerPlayer(AudioSource a, AudioSource b)
        {
            _a = a; _b = b;
            _a.loop = _b.loop = true;  // stems loopables
        }

        public void PlayLoopQuantized(AudioClip clip, double dspWhen, float volume = 1f, float pitch = 1f)
        {
            var s = _useA ? _a : _b;
            s.clip = clip;
            s.volume = 0f;
            s.pitch = pitch;
            s.PlayScheduled(dspWhen);
            s.SetScheduledStartTime(dspWhen);
            // dejalo a 0, levantarás volumen con un fade externo
        }

        public void CrossfadeTo(AudioClip next, double startAt, double fadeBeats, MusicClock clock, float targetVol = 1f)
        {
            var from = _useA ? _a : _b;
            var to   = _useA ? _b : _a;
            _useA = !_useA;

            to.clip = next;
            to.volume = 0f;
            to.loop = true;
            to.PlayScheduled(startAt);

            // envolvente en corutina/Update (no bloqueante)
            _ = FadeRoutine(from, to, fadeBeats * (float)clock.SecondsPerBeat, targetVol);
        }

        async Task FadeRoutine(AudioSource from, AudioSource to, double seconds, float targetVol)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01((float)(t / seconds));
                if (to)   to.volume   = Mathf.Lerp(0f, targetVol, k);
                if (from) from.volume = Mathf.Lerp(targetVol, 0f, k);
                await Task.Yield();
            }
            if (from) from.volume = 0f;
            if (to)   to.volume = targetVol;
        }
    }
}