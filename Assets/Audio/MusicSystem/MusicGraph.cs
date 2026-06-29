using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Audio.MusicSystem
{
    [CreateAssetMenu(menuName = "Audio/Music Graph", fileName = "MusicGraph")]
    public class MusicGraph : ScriptableObject
    {
        public List<Node> nodes = new();
        public List<Transition> transitions = new();

        [Header("Start")] public string startNodeId;

        [Serializable]
        public class Node
        {
            public string id;     // único
            public MusicCue cue;
        }

        [Serializable]
        public class Transition
        {
            public string fromNodeId;
            public string toNodeId;

            [Header("Trigger / Condición")]
            public string triggerName;
            public string paramName;
            public CompareOp compare = CompareOp.GreaterOrEqual;
            public float paramThreshold = 0.5f;

            [Header("Blend")]
            public Quantization quantization = Quantization.Bar;
            [Min(0f)] public float crossfadeSeconds = 1.5f;

            // --- NUEVO: Stingers separados ---
            [Header("Stingers")]
            // Compatibilidad: si ya tenías "fireStingerOnEnter" y "stingerId"
            [FormerlySerializedAs("fireStingerOnEnter")] public bool playEntryStinger;
            [FormerlySerializedAs("stingerId")]         public string entryStingerId;

            public bool  quantizeEntryStinger = true;
            public float entryOffsetSeconds;   // relativo al arranque del nuevo cue (si cuantizado) o "ahora"

            public bool  playExitStinger;     // tapa el fade-out
            public string exitStingerId;
            public bool  quantizeExitStinger; // tip: exit suele ir "ya"
            public float exitOffsetSeconds;    // si cuantizado: relativo a 'when'; si no: relativo al dspNow

            public enum CompareOp { Less, LessOrEqual, Greater, GreaterOrEqual, Equal, NotEqual }

            public bool Matches(float paramValue)
            {
                return compare switch
                {
                    CompareOp.Less => paramValue < paramThreshold,
                    CompareOp.LessOrEqual => paramValue <= paramThreshold,
                    CompareOp.Greater => paramValue > paramThreshold,
                    CompareOp.GreaterOrEqual => paramValue >= paramThreshold,
                    CompareOp.Equal => Mathf.Approximately(paramValue, paramThreshold),
                    CompareOp.NotEqual => !Mathf.Approximately(paramValue, paramThreshold),
                    _ => false
                };
            }
        }

        public enum Quantization { Immediate, Beat, Bar, TwoBars, FourBars, Loop }
    }
}
