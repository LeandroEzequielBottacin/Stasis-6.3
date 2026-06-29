using System.Collections.Generic;
using System.Linq;
using Environment;
using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.Gears
{
    public class StasisGear : MonoBehaviour, IStasis
    {
        public bool IsFreezed => isFreezed;
        public StasisEffect StasisEffect { get; private set; }
        public bool isFreezed;

        [Header("Visual Stasis (Outline)")]
        public Material matStasis;
        public readonly string _outlineThicknessName = "_BorderThickness";
        private MaterialPropertyBlock _mpb;
        [SerializeField] private List<Renderer> _renders = new List<Renderer>();

        [Header("Emission (URP Lit/HDR)")]
        public bool controlEmission = true;
        public Color emissiveOnColor = Color.white;
        [Min(0f)] public float emissiveOnIntensity = 1.0f;
        public Color emissiveOffColor = Color.black;
        [Min(0f)] public float emissiveOffIntensity;
        public bool forceEnableEmissionKeyword = true;
        private readonly string _emissionColorName = "_EmissionColor";
        private readonly string _emissionKeyword = "_EMISSION";

        [Header("Rotación manejada por MultiGearRotator (per-item)")]
        public MultiGearRotator multiGearRotator;
        public Transform gearTarget;

        // ------------ Nearest Link ------------
        public enum LinkMode { PauseOnly, VisualOnly, PauseAndVisual }
        [Header("Nearest Link")]
        [Tooltip("Si está activo, al stasear este engrane también afectará al más cercano.")]
        public bool linkNearest = true;

        [Tooltip("Cantidad de engranes vecinos a afectar (1 = solo el más cercano).")]
        [Min(1)] public int linkCount = 1;

        [Tooltip("Máxima distancia para considerar vecinos.")]
        public float maxLinkDistance = 2.5f;

        [Tooltip("Requiere línea de vista limpia (raycast) entre engranes.")]
        public bool requireLineOfSight;
        public LayerMask losMask = ~0;

        [Tooltip("Qué efecto aplicar al vecino.")]
        public LinkMode linkMode = LinkMode.PauseAndVisual;

        // Evita recursión (los vecinos no siguen encadenando)
        private bool _isChainActivation;

        // Pausas que hicimos nosotros (para reanudar correctamente)
        private bool _pausedThisItem;
        private readonly List<Transform> _extraPaused = new List<Transform>(8);

        private void Start()
        {
            _mpb = new MaterialPropertyBlock();

            if (controlEmission && forceEnableEmissionKeyword && _renders != null)
            {
                foreach (var instancedMat in from r in _renders where r != null select r.material into instancedMat where instancedMat != null && !instancedMat.IsKeywordEnabled(_emissionKeyword) select instancedMat)
                {
                    instancedMat.EnableKeyword(_emissionKeyword);
                }
            }

            if (controlEmission)
                ApplyEmission(emissiveOffColor, emissiveOffIntensity);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void StatisEffectActivate() => FreezeObject();
        // ReSharper disable Unity.PerformanceAnalysis
        public void StatisEffectDeactivate() => UnfreezeObject();

        // Permite activar desde un vecino sin encadenar más
        // ReSharper disable Unity.PerformanceAnalysis
        public void ActivateFromChain()
        {
            _isChainActivation = true;
            FreezeObject();
            _isChainActivation = false;
        }

        public void FreezeObject()
        {
            if (isFreezed) return;
            isFreezed = true;

            // Visual propio
            SetOutlineThickness(1.05f);
            SetColorOutline(Color.green, 1f);
            if (controlEmission) ApplyEmission(emissiveOnColor, emissiveOnIntensity);

            // Pausar SOLO este engrane
            _pausedThisItem = false;
            if (multiGearRotator != null && gearTarget != null)
            {
                _pausedThisItem = multiGearRotator.PauseItem(gearTarget);
            }
            else
            {
                Debug.LogWarning("[StasisGear] Falta multiGearRotator o gearTarget. No se pausó el propio engrane.", this);
            }

            // Vecino(s) más cercano(s), sin reencadenar
            if (linkNearest && !_isChainActivation)
            {
                var neighbors = FindNearestTargetsInSameRotator();
                AffectNeighbors(neighbors);
            }
        }

        private void UnfreezeObject()
        {
            if (!isFreezed) return;
            isFreezed = false;

            // Visual propio OFF
            SetOutlineThickness(0f);
            SetColorOutline(new Color(0.6f, 1f, 0.6f), 1f);
            if (controlEmission) ApplyEmission(emissiveOffColor, emissiveOffIntensity);

            // Reanudar SOLO este engrane (si lo pausamos nosotros)
            if (_pausedThisItem && multiGearRotator != null && gearTarget != null)
            {
                multiGearRotator.ResumeItem(gearTarget);
                _pausedThisItem = false;
            }

            // Reanudar vecinos pausados por este script
            if (_extraPaused.Count > 0 && multiGearRotator != null)
            {
                for (int i = 0; i < _extraPaused.Count; i++)
                {
                    var t = _extraPaused[i];
                    if (t != null) multiGearRotator.ResumeItem(t);
                }
                _extraPaused.Clear();
            }

            // Apagar visual de vecinos que activamos (siempre que tengan StasisGear y estaban en stasis por cadena)
            // Nota: si querés que el vecino se “des-stasee” solo cuando su propio Stasis termine, podés omitir esto.
            // Aquí preferimos simetría (ON al congelar, OFF al descongelar).
            if (linkNearest)
            {
                var neighbors = FindNearestTargetsInSameRotator();
                for (int i = 0; i < neighbors.Count; i++)
                {
                    var sg = neighbors[i].GetComponent<StasisGear>();
                    if (sg != null && sg != this)
                    {
                        // Apagamos solo si lo activamos a través de cadena
                        sg.DeactivateFromChainIfActive();
                    }
                }
            }
        }

        // Llamada desde el vecino “dueño” para apagar SOLO visual si fue cadena
        public void DeactivateFromChainIfActive()
        {
            if (!isFreezed) return; // si no quedó en stasis visual, nada que hacer
            // Si nos activaron por cadena, el flag no se conserva; apagamos visual de todos modos.
            isFreezed = false;
            SetOutlineThickness(0f);
            SetColorOutline(new Color(0.6f, 1f, 0.6f), 1f);
            if (controlEmission) ApplyEmission(emissiveOffColor, emissiveOffIntensity);
            // No tocamos pausas aquí (las maneja el dueño)
        }

        // ---------- Búsqueda de vecinos ----------
        private List<Transform> FindNearestTargetsInSameRotator()
        {
            var found = new List<Transform>(linkCount);

            if (multiGearRotator == null || gearTarget == null) return found;
            var items = multiGearRotator.items;
            if (items == null || items.Count == 0) return found;

            Vector3 origin = gearTarget.position;
            float maxDistSq = maxLinkDistance * maxLinkDistance;

            // Seleccion rápida de N mínimos
            Transform bestT = null; float bestSq = float.PositiveInfinity;
            // Si linkCount > 1, mantenemos top-N (simple)
            var candidates = new List<(Transform t, float d2)>(items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null || it.target == null) continue;
                if (it.target == gearTarget) continue;

                float d2 = (it.target.position - origin).sqrMagnitude;
                if (d2 > maxDistSq) continue;

                if (requireLineOfSight && !HasLineOfSight(origin, it.target.position))
                    continue;

                candidates.Add((it.target, d2));
            }

            // Ordenamos por distancia y tomamos los N primeros
            candidates.Sort((a, b) => a.d2.CompareTo(b.d2));
            int take = Mathf.Min(linkCount, candidates.Count);
            for (int i = 0; i < take; i++)
                found.Add(candidates[i].t);

            return found;
        }

        private bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist <= 0.0001f) return true;
            return !Physics.Raycast(from, dir / dist, dist, losMask, QueryTriggerInteraction.Ignore);
        }

        // ---------- Aplicación a vecinos ----------
        private void AffectNeighbors(List<Transform> neighbors)
        {
            for (int i = 0; i < neighbors.Count; i++)
            {
                var t = neighbors[i];

                if (linkMode == LinkMode.PauseOnly || linkMode == LinkMode.PauseAndVisual)
                {
                    if (multiGearRotator != null && multiGearRotator.PauseItem(t))
                        _extraPaused.Add(t);
                }

                if (linkMode == LinkMode.VisualOnly || linkMode == LinkMode.PauseAndVisual)
                {
                    var sg = t.GetComponent<StasisGear>();
                    if (sg != null && sg != this)
                    {
                        // Activación visual/pausa del vecino SIN reencadenar
                        sg.ActivateFromChain();
                    }
                    // Si el vecino no tiene StasisGear, podríamos aplicar solo outline/emission por MPB aquí si lo necesitás.
                    // Lo omitimos para mantener el sistema limpio.
                }
            }
        }

        // ---------- Visual helpers ----------
        public void SetOutlineThickness(float thickness)
        {
            foreach (var rend in _renders)
            {
                if (rend == null) continue;
                rend.GetPropertyBlock(_mpb);
                _mpb.SetFloat(_outlineThicknessName, thickness);
                rend.SetPropertyBlock(_mpb);
            }
        }
        //
        public void SetColorOutline(Color color, float alpha)
        {
            foreach (var rend in _renders)
            {
                if (rend == null) continue;
                rend.GetPropertyBlock(_mpb);
                _mpb.SetColor("_Color", color);
                rend.SetPropertyBlock(_mpb);
            }
        }

        private void ApplyEmission(Color color, float intensity)
        {
            Color hdr = color * Mathf.Max(0f, intensity);
            foreach (var rend in _renders)
            {
                if (rend == null) continue;
                rend.GetPropertyBlock(_mpb);
                _mpb.SetColor(_emissionColorName, hdr);
                rend.SetPropertyBlock(_mpb);
            }
        }
    }
}
