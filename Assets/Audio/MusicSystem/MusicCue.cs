using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio.MusicSystem
{
    [CreateAssetMenu(menuName = "Audio/Music Cue", fileName = "MusicCue")]
    public class MusicCue : ScriptableObject
    {
        [Header("Tempo")]
        [Min(1)] public float bpm = 120f;
        [Min(1)] public int beatsPerBar = 4;
        [Min(1)] public int barsPerLoop = 8;

        [Header("Mix / Routing")]
        public AudioMixerGroup outputMixer;

        [Header("Layers (stems)")]
        public List<Layer> layers = new();

        [Header("Stingers (opcionales)")]
        public List<Stinger> stingers = new();

        [Serializable]
        public class Layer
        {
            public string id = "Base";             // p.ej. "Drums", "Bass", "Pad"
            public AudioClip clip;
            [Range(0f, 1f)] public float defaultVolume = 1f;
            public bool enabledByDefault = true;
            public bool loop = true;
            public bool isOptional = true;         // si false => se activa siempre con el cue
            public string groupTag;                // p.ej. "Rhythm", "Harmony" (para mutear por grupo)
        }

        [Serializable]
        public class Stinger
        {
            public string id = "Hit";
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        /// <summary>Duración (segundos) de un compás.</summary>
        public double BarDuration => (60.0 / bpm) * beatsPerBar;

        /// <summary>Duración (segundos) del loop completo.</summary>
        public double LoopDuration => BarDuration * barsPerLoop;
    }
}