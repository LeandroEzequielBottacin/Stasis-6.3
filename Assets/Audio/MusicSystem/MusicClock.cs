using UnityEngine;

namespace Audio.MusicSystem
{
    public sealed class MusicClock
    {
        public double DspStart { get; private set; }
        public double Bpm { get; private set; } = 120.0;
        public int    BeatsPerBar { get; private set; } = 4;

        public double SecondsPerBeat => 60.0 / Bpm;
        public double SecondsPerBar  => SecondsPerBeat * BeatsPerBar;

        public void Start(double bpm, int beatsPerBar)
        {
            Bpm = bpm; BeatsPerBar = beatsPerBar;
            DspStart = AudioSettings.dspTime;
        }

        public double NowDSP => AudioSettings.dspTime;

        // próximo beat/bar >= now
        public double NextBeat(int beatsAhead = 1)
            => AlignToGrid(NowDSP, SecondsPerBeat, beatsAhead);

        public double NextBar(int barsAhead = 1)
            => AlignToGrid(NowDSP, SecondsPerBar, barsAhead);

        static double AlignToGrid(double now, double step, int stepsAhead)
        {
            double q = Mathf.Ceil((float)(now / step));
            return (q + (stepsAhead - 1)) * step;
        }
    }
}