//using System.Collections.Generic;
//using System.Reflection;
//using Puzzle_Elements.IK.Scripts;
//using UnityEditor;
////using UnityEditor.SceneManagement;
//using UnityEngine;
//using UnityEngine.Animations.Rigging;

//namespace Puzzle_Elements.IK_OBJECT.Scripts
//{
//    [ExecuteAlways]
//    [DisallowMultipleComponent]
//    public class IKObjectController : MonoBehaviour
//    {
//        [Header("<color=green>Scriptable Object</color>")]
//        public IKObjectPreset preset;

//        [Header("<color=red>Se mira y no se toca</color>")]
//        public Transform arm;        // Animator + RigBuilder (instancia en escena)
//        public Transform root;       // donde se instancian bones
//        public Transform rigObject;  // Rig + ChainIKConstraint (bajo arm)
//        public Transform sistemas;   // opcional

//        // Contenedores de visibilidad por sistema
//        public Transform sys_IK_Distance;
//        public Transform sys_IK_DistanceInverse;
//        public Transform sys_IK_PlatformMovement;
//        public Transform sys_IK_PlatformRotation;
//        public Transform sys_IK_PlatformRotation_Stairs;

//        // Targets por sistema
//        public Transform target_IK_Distance;                 // TipController
//        public Transform target_IK_DistanceInverse;          // TipController (inverse)
//        public Transform target_IK_PlatformMovement;         // PlatformM
//        public Transform target_IK_PlatformRotation;         // Platform
//        public Transform target_IK_PlatformRotation_Stairs;  // Platform (stairs)

//        // Stasis tip controllers (tus componentes)
//        public Component stasisTipCtrl_Distance;
//        public Component stasisTipCtrl_Inverse;
//        public Component stasisTipCtrl_Movement;
//        public Component stasisTipCtrl_Rotation;
//        public Component stasisTipCtrl_Stairs;

//        // Prefabs
//        public GameObject bonePrefab_Distance;
//        public GameObject bonePrefab_PlatformMovement;
//        public GameObject bonePrefab_Rotation;
//        public GameObject tipPrefab_Distance_MeshOn;
//        public GameObject tipPrefab_MeshOff;

//        // Opciones de editor
//        [Tooltip("En Editor (sin Play) apaga Animator/Rig para rotar/mover los huesos libremente.")]
//        public bool liveRebuildInEditor = true;
//        public bool editorFreePose = true;

//        // Offset de orientaci�n para Distance/Inverse
//        public Vector3 tipEulerOffset_Distance = new Vector3(0f, 180f, 0f);

//        // caches
//        ChainIKConstraint _chain;
//        RigBuilder _rigBuilder;
//        Rig _rig;
//        Animator _anim;

//        readonly List<Transform> _generatedBones = new List<Transform>();
//        Transform _generatedTip;

//        // -------- Persistencia de pose de autor --------
//        [System.Serializable]
//        public struct PoseData
//        {
//            public string name;
//            public Vector3 localPos;
//            public Quaternion localRot;
//            public Vector3 localScale;
//        }
//        [SerializeField] List<PoseData> _savedPose = new List<PoseData>();

//#if UNITY_EDITOR
//        [SerializeField, HideInInspector] int _presetHash;
//        bool _rebuildScheduled;
//#endif

//        // =====================================================================
//        // Lifecycle
//        // =====================================================================
//        void OnEnable()
//        {
//            ApplySystemVisibilityInEditor();
//            ApplyTunablesInEditor();
//            EnsureRigComponents();

//#if UNITY_EDITOR
//            if (liveRebuildInEditor) ScheduleRebuild();
//            else RebuildNow();
//#endif
//        }

//        void OnDisable()
//        {
//#if UNITY_EDITOR
            
//#endif
//        }

//        void Update()
//        {
//#if UNITY_EDITOR
//            if (!Application.isPlaying && preset && liveRebuildInEditor)
//            {
//                int h = ComputePresetHash();
//                if (h != _presetHash)
//                {
//                    _presetHash = h;
//                    ApplySystemVisibilityInEditor();
//                    ApplyTunablesInEditor();
//                    ScheduleRebuild();
//                }
//            }
//#endif
//        }

//        void OnValidate()
//        {
//            ApplySystemVisibilityInEditor();
//            ApplyTunablesInEditor();
//#if UNITY_EDITOR
//            if (liveRebuildInEditor) ScheduleRebuild();
//            else ApplyEditorFreePoseState();
//#endif
//        }

//        // =====================================================================
//        // Rebuild
//        // =====================================================================
//#if UNITY_EDITOR
//        [ContextMenu("Rebuild In Editor Now")]
//        void RebuildInEditorNowMenu() { RebuildNow(); }

//        [ContextMenu("Capture Author Pose (Editor)")]
//        void MenuCapturePose()
//        {
//            if (Application.isPlaying) return;
//            CaptureAuthorPoseToSaved();
//            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(gameObject.scene);
//        }

//        [ContextMenu("Apply Saved Pose (Editor)")]
//        void MenuApplyPose()
//        {
//            if (Application.isPlaying) return;
//            RestoreSavedPoseToCurrent();
//            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(gameObject.scene);
//        }

//        void ScheduleRebuild()
//        {
//            if (_rebuildScheduled) return;
//            _rebuildScheduled = true;
//            EditorApplication.delayCall += DoRebuildDelayed;
//        }

//        void DoRebuildDelayed()
//        {
//            _rebuildScheduled = false;
//            if (this == null) return;
//            RebuildNow();
//        }
//#endif

//        void RebuildNow()
//        {
//            if (!ValidateSetup()) return;

//            if (Application.isPlaying)
//            {
//                // En Play: si ya existe la cadena generada en escena, NO la destruimos.
//                if (TryBindExistingGeneratedInPlay())
//                {
//                    if (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse)
//                        ApplyDistanceRuntimeInit();
//#if UNITY_EDITOR
//                    ApplyEditorFreePoseState(); // en Play siempre deja Rig/Animator activos
//#endif
//                    return;
//                }

//                // Si no hab�a cadena, construir.
//                PurgeGeneratedRuntime();
//                BuildArm(false);

//                if (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse)
//                    ApplyDistanceRuntimeInit();

//                // NO llamar Animator.Rebind() para no perder pose
//                return;
//            }

//            // ----- Editor -----
//#if UNITY_EDITOR
//            CaptureAuthorPoseToSaved();
//            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(gameObject.scene);
//            PurgeGeneratedEditor();

//            BuildArm(true);

//            RestoreSavedPoseToCurrent();

//            var stasisTC = GetStasisTipControllerForMode(preset.systemType);
//            SafeCallAddElements(stasisTC);

//            ApplyEditorFreePoseState();
//#endif
//        }

//#if UNITY_EDITOR
//        void CaptureAuthorPoseToSaved()
//        {
//            _savedPose.Clear();
//            if (!root) return;

//            var tags = root.GetComponentsInChildren<IKGeneratedTag>(true);
//            foreach (var t in tags)
//            {
//                if (!t) continue;
//                var tr = t.transform;
//                _savedPose.Add(new PoseData
//                {
//                    name = tr.name,
//                    localPos = tr.localPosition,
//                    localRot = tr.localRotation,
//                    localScale = tr.localScale
//                });
//            }
//        }

//        void RestoreSavedPoseToCurrent()
//        {
//            if (_savedPose == null || _savedPose.Count == 0 || !root) return;

//            var dict = new Dictionary<string, Transform>();
//            var tags = root.GetComponentsInChildren<IKGeneratedTag>(true);
//            foreach (var t in tags)
//            {
//                if (!t) continue;
//                dict[t.transform.name] = t.transform;
//            }

//            foreach (var p in _savedPose)
//            {
//                if (!dict.TryGetValue(p.name, out var tr)) continue;
//                tr.localPosition = p.localPos;
//                tr.localRotation = p.localRot;
//                tr.localScale = p.localScale;
//            }
//        }
//#endif

//        // =====================================================================
//        // Build chain
//        // =====================================================================
//        void BuildArm(bool immediate)
//        {
//            EnsureRigComponents();

//            var bonePrefab = GetBonePrefabForMode(preset.systemType);
//            var tipPrefab = GetTipPrefabForMode(preset.systemType);
//            var target = GetTargetForMode(preset.systemType);
//            var stasisTC = GetStasisTipControllerForMode(preset.systemType);

//            Transform firstBone = null;
//            Transform prevBone = null;
//            Transform prevEnd = null;
//            Vector3 firstBoneWorldScale = Vector3.one;

//            int count = Mathf.Max(1, preset.boneCount);

//            // Bones
//            for (int i = 0; i < count; i++)
//            {
//                var boneT = InstantiateBone(bonePrefab, i == 0 ? root : prevBone, immediate);
//                if (boneT == null) return;
//                boneT.name = "Bone_" + (i + 1);
//                _generatedBones.Add(boneT);

//                if (i == 0)
//                {
//                    boneT.localPosition = Vector3.zero;
//                    boneT.localRotation = Quaternion.identity;
//                    firstBoneWorldScale = boneT.lossyScale;
//                    SetWorldScale(boneT, firstBoneWorldScale);
//                    firstBone = boneT;
//                }
//                else
//                {
//                    if (!prevEnd) return;
//                    boneT.localPosition = prevEnd.localPosition;
//                    boneT.localRotation = prevEnd.localRotation;
//                    SetWorldScale(boneT, firstBoneWorldScale);
//                }

//                TryAssignTipControllerToBone(boneT.gameObject, stasisTC);

//                var be = boneT.GetComponentInChildren<BoneEnd>(true);
//                if (!be || !be.GetEnd()) return;
//                prevEnd = be.GetEnd();
//                prevBone = boneT;
//            }

//            // TIP: hijo del PADRE de END, en la pose mundial de END (+ offset para Distance/Inverse)
//            Transform tipParent = prevEnd ? prevEnd.parent : prevBone;
//            var tipT = InstantiateTip(tipPrefab, tipParent, immediate);
//            if (tipT == null) return;

//            tipT.name = "TIP";
//            if (prevEnd != null)
//            {
//                tipT.position = prevEnd.position;
//                tipT.rotation = (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse)
//                                ? prevEnd.rotation * Quaternion.Euler(tipEulerOffset_Distance)
//                                : prevEnd.rotation;
//            }
//            else
//            {
//                tipT.localPosition = Vector3.zero;
//                tipT.localRotation = Quaternion.identity;
//            }
//            _generatedTip = tipT;

//            // Rig / Constraint: asignar data ANTES de build
//            _chain = rigObject ? rigObject.GetComponent<ChainIKConstraint>() : null;
//            if (_chain == null && rigObject) _chain = rigObject.gameObject.AddComponent<ChainIKConstraint>();
//            _rig = rigObject ? rigObject.GetComponent<Rig>() : null;
//            if (_rig == null && rigObject) _rig = rigObject.gameObject.AddComponent<Rig>();

//            if (_chain == null || _rig == null) return;

//            var data = _chain.data;
//            data.root = firstBone;
//            data.tip = tipT;
//            data.target = target;
//            _chain.data = data;

//            // Pesos por sistema
//            if (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse)
//            {
//                _chain.weight = 1f;
//                _rig.weight = 0f;   // FollowTargetController maneja el weight
//            }
//            else
//            {
//                _chain.weight = 1f;
//                _rig.weight = 1f;   // Movement/Rotation activos siempre
//            }

//            EnsureRigComponents();
//            if (_rigBuilder) _rigBuilder.Build();

//            if (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse)
//                AssignRigToFollowTargetController(target, _rig);
//        }

//        // =====================================================================
//        // Play init para Distance/Inverse
//        // =====================================================================
//        void ApplyDistanceRuntimeInit()
//        {
//            var target = GetTargetForMode(preset.systemType);
//            if (target == null || _generatedTip == null) return;

//            target.position = _generatedTip.position;
//            target.rotation = _generatedTip.rotation;

//            if (_chain != null) _chain.weight = 1f;
//            if (_rig != null) _rig.weight = 0f; // FollowTargetController maneja el weight
//        }

//        // Vincula el rig al FollowTargetController en el target (si existe)
//        static void AssignRigToFollowTargetController(Transform target, Rig rig)
//        {
//            if (!target || !rig) return;
//            var ft = target.GetComponent<FollowTargetController>();
//            if (!ft) return;
//            ft.rig = rig;
//        }

//        // =====================================================================
//        // Reusar cadena existente en Play (seguro)
//        // =====================================================================
//        bool TryBindExistingGeneratedInPlay()
//        {
//            if (!Application.isPlaying) return false;
//            if (!root || !rigObject) return false;
//            if (!root.gameObject.scene.IsValid()) return false;

//            var tags = root.GetComponentsInChildren<IKGeneratedTag>(true);
//            if (tags == null || tags.Length == 0) return false;

//            Transform firstBone = null;
//            Transform tip = null;

//            foreach (var t in tags)
//            {
//                if (!t) continue;
//                if (t.name == "TIP") tip = t.transform;
//            }
//            foreach (var t in tags)
//            {
//                if (!t) continue;
//                if (t.transform.parent == root && t.name.StartsWith("Bone_"))
//                {
//                    firstBone = t.transform;
//                    break;
//                }
//            }

//            if (!firstBone || !tip) return false;

//            EnsureRigComponents();
//            if (!rigObject) return false;

//            if (_chain == null) _chain = rigObject.gameObject.GetComponent<ChainIKConstraint>();
//            if (_chain == null) _chain = rigObject.gameObject.AddComponent<ChainIKConstraint>();

//            if (_rig == null) _rig = rigObject.gameObject.GetComponent<Rig>();
//            if (_rig == null) _rig = rigObject.gameObject.AddComponent<Rig>();

//            var target = GetTargetForMode(preset.systemType);

//            var data = _chain.data;
//            data.root = firstBone;
//            data.tip = tip;
//            data.target = target;
//            _chain.data = data;

//            if (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse)
//            {
//                _chain.weight = 1f;
//                _rig.weight = 0f;
//                AssignRigToFollowTargetController(target, _rig);
//            }
//            else
//            {
//                _chain.weight = 1f;
//                _rig.weight = 1f;
//            }

//            EnsureRigComponents();
//            if (_rigBuilder) _rigBuilder.Build();

//            _generatedBones.Clear();
//            foreach (var t in tags) if (t && t.name.StartsWith("Bone_")) _generatedBones.Add(t.transform);
//            _generatedTip = tip;

//            return true;
//        }

//        // =====================================================================
//        // Purge
//        // =====================================================================
//#if UNITY_EDITOR
//        void PurgeGeneratedEditor()
//        {
//            if (!root) return;
//            var tags = root.GetComponentsInChildren<IKGeneratedTag>(true);
//            foreach (var t in tags) if (t) Undo.DestroyObjectImmediate(t.gameObject);
//            _generatedBones.Clear();
//            _generatedTip = null;
//        }
//#endif

//        void PurgeGeneratedRuntime()
//        {
//            if (!root) return;
//            var tags = root.GetComponentsInChildren<IKGeneratedTag>(true);
//            foreach (var t in tags) if (t) Destroy(t.gameObject);
//            _generatedBones.Clear();
//            _generatedTip = null;
//        }

//        // =====================================================================
//        // Instantiation helpers (Prefab Asset parenting safe)
//        // =====================================================================
//#if UNITY_EDITOR
//        Transform GetSafeParent(Transform desiredParent)
//        {
//            if (!root || !root.gameObject.scene.IsValid())
//                return desiredParent ? desiredParent : null;

//            if (desiredParent && desiredParent.gameObject.scene == root.gameObject.scene)
//                return desiredParent;

//            return root;
//        }
//#endif

//        Transform InstantiateBone(GameObject prefab, Transform parent, bool immediate)
//        {
//#if UNITY_EDITOR
//            if (!Application.isPlaying && prefab && PrefabUtility.IsPartOfPrefabAsset(prefab))
//            {
//                var parentT = GetSafeParent(parent != null ? parent : root);
//                var obj = PrefabUtility.InstantiatePrefab(prefab, parentT) as GameObject;
//                if (obj == null) return null;
//                obj.AddComponent<IKGeneratedTag>();
//                Undo.RegisterCreatedObjectUndo(obj, "Create Bone");
//                return obj.transform;
//            }
//#endif
//            var go = Instantiate(prefab, parent ? parent : root);
//            go.AddComponent<IKGeneratedTag>();
//            return go.transform;
//        }

//        Transform InstantiateTip(GameObject prefab, Transform parent, bool immediate)
//        {
//#if UNITY_EDITOR
//            if (!Application.isPlaying && prefab && PrefabUtility.IsPartOfPrefabAsset(prefab))
//            {
//                var parentT = GetSafeParent(parent != null ? parent : root);
//                var obj = PrefabUtility.InstantiatePrefab(prefab, parentT) as GameObject;
//                if (obj == null) return null;
//                obj.AddComponent<IKGeneratedTag>();
//                Undo.RegisterCreatedObjectUndo(obj, "Create TIP");
//                return obj.transform;
//            }
//#endif
//            var go = Instantiate(prefab, parent ? parent : root);
//            go.AddComponent<IKGeneratedTag>();
//            return go.transform;
//        }

//        // =====================================================================
//        // Hook AddElementsToRenderer
//        // =====================================================================
//        void CallAddElementsWithTemporaryParent(Component controller)
//        {
//            if (controller == null || root == null) return;
//            var ctrlT = controller.transform;
//            var origParent = root.parent;
//            var wp = root.position; var wr = root.rotation; var ws = root.lossyScale;

//            root.SetParent(ctrlT, true);
//            SafeCallAddElements(controller);
//            root.SetParent(origParent, true);
//            root.position = wp; root.rotation = wr; SetWorldScale(root, ws);
//        }

//        static void SafeCallAddElements(Component controller)
//        {
//            if (controller == null) return;
//            var m = controller.GetType().GetMethod("AddElementsToRenderer",
//                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
//            if (m != null && m.GetParameters().Length == 0)
//            {
//                try { m.Invoke(controller, null); }
//                catch (System.SystemException ex) { Debug.LogError("[IKObjectController] AddElementsToRenderer error: " + ex.Message, controller); }
//            }
//        }

//        // =====================================================================
//        // Editor helpers (visibilidad y tunables)
//        // =====================================================================
//        void ApplySystemVisibilityInEditor()
//        {
//            if (!preset) return;
//            Toggle(sys_IK_Distance, preset.systemType == IKSystemType.IK_Distance);
//            Toggle(sys_IK_DistanceInverse, preset.systemType == IKSystemType.IK_DistanceInverse);
//            Toggle(sys_IK_PlatformMovement, preset.systemType == IKSystemType.IK_PlatformMovement);
//            Toggle(sys_IK_PlatformRotation, preset.systemType == IKSystemType.IK_PlatformRotation);
//            Toggle(sys_IK_PlatformRotation_Stairs, preset.systemType == IKSystemType.IK_PlatformRotation_Stairs);
//        }

//        static void Toggle(Transform t, bool on)
//        {
//            if (!t) return;
//            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
//        }

//        void ApplyTunablesInEditor()
//        {
//#if UNITY_EDITOR
//            if (!preset) return;
//            switch (preset.systemType)
//            {
//                case IKSystemType.IK_Distance:
//                case IKSystemType.IK_DistanceInverse:
//                    break;

//                case IKSystemType.IK_PlatformMovement:
//                    ApplyToPathFollower1(target_IK_PlatformMovement,
//                        preset.movement_speed,
//                        preset.movement_distanceThreshold);
//                    break;

//                case IKSystemType.IK_PlatformRotation:
//                    ApplyToFollowMultipleTargetController(target_IK_PlatformRotation,
//                        preset.rotation_remapLerp,
//                        preset.rotation_arcHeight,
//                        preset.rotation_moveDelay,
//                        preset.rotation_travelTime,
//                        preset.rotation_stopDuration);
//                    break;

//                case IKSystemType.IK_PlatformRotation_Stairs:
//                    if (preset.stairs_useRotationValues)
//                    {
//                        ApplyToFollowMultipleTargetController(target_IK_PlatformRotation_Stairs,
//                            preset.rotation_remapLerp,
//                            preset.rotation_arcHeight,
//                            preset.rotation_moveDelay,
//                            preset.rotation_travelTime,
//                            preset.rotation_stopDuration);
//                    }
//                    else
//                    {
//                        ApplyToFollowMultipleTargetController(target_IK_PlatformRotation_Stairs,
//                            preset.stairs_remapLerp,
//                            preset.stairs_arcHeight,
//                            preset.stairs_moveDelay,
//                            preset.stairs_travelTime,
//                            preset.stairs_stopDuration);
//                    }
//                    break;
//            }
//#endif
//        }

//#if UNITY_EDITOR
//        void ApplyEditorFreePoseState()
//        {
//            EnsureRigComponents();
//            if (Application.isPlaying)
//            {
//                // En Play SIEMPRE activos; pesos por sistema
//                if (_anim) _anim.enabled = true;
//                if (_chain) _chain.enabled = true;
//                if (_rig)
//                {
//                    _rig.enabled = true;
//                    _rig.weight = (preset != null &&
//                        (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse))
//                        ? 0f : 1f;
//                }
//                if (_rigBuilder) { _rigBuilder.enabled = true; _rigBuilder.Build(); }
//                return;
//            }

//            // En Editor: modo pose libre opcional
//            if (editorFreePose)
//            {
//                if (_anim) _anim.enabled = false;
//                if (_rigBuilder) { _rigBuilder.Clear(); _rigBuilder.enabled = false; }
//                if (_rig) { _rig.enabled = false; _rig.weight = 0f; }
//                if (_chain) _chain.enabled = false;
//            }
//            else
//            {
//                if (_anim) _anim.enabled = true;
//                if (_rig)
//                {
//                    _rig.enabled = true; _rig.weight = (preset != null &&
//            (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse)) ? 0f : 1f;
//                }
//                if (_chain) _chain.enabled = true;
//                if (_rigBuilder) { _rigBuilder.enabled = true; _rigBuilder.Build(); }
//            }
//        }
//#endif

//        // Mantener escala mundo al reparentar
//        static void SetWorldScale(Transform t, Vector3 worldScale)
//        {
//            var p = t.parent;
//            if (!p) { t.localScale = worldScale; return; }
//            var inv = new Vector3(
//                p.lossyScale.x != 0f ? 1f / p.lossyScale.x : 1f,
//                p.lossyScale.y != 0f ? 1f / p.lossyScale.y : 1f,
//                p.lossyScale.z != 0f ? 1f / p.lossyScale.z : 1f);
//            t.localScale = Vector3.Scale(worldScale, inv);
//        }

//        // Pasar StasisTipController a cada bone (sin .Find)
//        void TryAssignTipControllerToBone(GameObject boneGO, Component tipController)
//        {
//            if (!boneGO || !tipController) return;
//            var receivers = boneGO.GetComponentsInChildren<MonoBehaviour>(true);
//            foreach (var r in receivers)
//                if (r is IStasisPartIK iface) iface.SetTipController(tipController);

//            string[] names = { "stasisTipController", "StasisTipController", "tipController", "TipController", "stasisTipCtrl", "StasisTipCtrl" };
//            foreach (var r in receivers)
//            {
//                var t = r.GetType();
//                foreach (var n in names)
//                {
//                    var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
//                    if (f != null && (tipController == null || f.FieldType.IsAssignableFrom(tipController.GetType())))
//                        f.SetValue(r, tipController);

//                    var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
//                    if (p != null && p.CanWrite && (tipController == null || p.PropertyType.IsAssignableFrom(tipController.GetType())))
//                        p.SetValue(r, tipController, null);
//                }
//            }
//        }

//        // =====================================================================
//        // Rig / RigBuilder guards
//        // =====================================================================
//        void EnsureRigComponents()
//        {
//            _rigBuilder = arm ? arm.GetComponent<RigBuilder>() : null;
//            if (arm && _rigBuilder == null) _rigBuilder = arm.gameObject.AddComponent<RigBuilder>();

//            _rig = rigObject ? rigObject.GetComponent<Rig>() : null;
//            if (rigObject && _rig == null) _rig = rigObject.gameObject.AddComponent<Rig>();

//            _anim = arm ? arm.GetComponent<Animator>() : null;

//            if (_rigBuilder != null)
//            {
//                // Dejar una sola capa, apuntando a nuestro Rig
//                for (int i = _rigBuilder.layers.Count - 1; i >= 0; i--)
//                {
//                    var layer = _rigBuilder.layers[i];
//                    if (layer.rig == null || (_rig != null && layer.rig != _rig))
//                        _rigBuilder.layers.RemoveAt(i);
//                }
//                bool listed = false;
//                foreach (var l in _rigBuilder.layers) { if (l.rig == _rig) { listed = true; break; } }
//                if (!listed && _rig != null) _rigBuilder.layers.Add(new RigLayer(_rig, true));
//            }
//        }

//        // =====================================================================
//        // Helpers de modo
//        // =====================================================================
//        GameObject GetBonePrefabForMode(IKSystemType mode)
//        {
//            switch (mode)
//            {
//                case IKSystemType.IK_Distance:
//                case IKSystemType.IK_DistanceInverse: return bonePrefab_Distance;
//                case IKSystemType.IK_PlatformMovement: return bonePrefab_PlatformMovement;
//                case IKSystemType.IK_PlatformRotation:
//                case IKSystemType.IK_PlatformRotation_Stairs: return bonePrefab_Rotation;
//            }
//            return null;
//        }

//        GameObject GetTipPrefabForMode(IKSystemType mode)
//        {
//            switch (mode)
//            {
//                case IKSystemType.IK_Distance:
//                case IKSystemType.IK_DistanceInverse: return tipPrefab_Distance_MeshOn;
//                default: return tipPrefab_MeshOff;
//            }
//        }

//        Transform GetTargetForMode(IKSystemType mode)
//        {
//            switch (mode)
//            {
//                case IKSystemType.IK_Distance: return target_IK_Distance;
//                case IKSystemType.IK_DistanceInverse: return target_IK_DistanceInverse;
//                case IKSystemType.IK_PlatformMovement: return target_IK_PlatformMovement;
//                case IKSystemType.IK_PlatformRotation: return target_IK_PlatformRotation;
//                case IKSystemType.IK_PlatformRotation_Stairs: return target_IK_PlatformRotation_Stairs;
//            }
//            return null;
//        }

//        Component GetStasisTipControllerForMode(IKSystemType mode)
//        {
//            switch (mode)
//            {
//                case IKSystemType.IK_Distance: return stasisTipCtrl_Distance;
//                case IKSystemType.IK_DistanceInverse: return stasisTipCtrl_Inverse ? stasisTipCtrl_Inverse : stasisTipCtrl_Distance;
//                case IKSystemType.IK_PlatformMovement: return stasisTipCtrl_Movement;
//                case IKSystemType.IK_PlatformRotation: return stasisTipCtrl_Rotation;
//                case IKSystemType.IK_PlatformRotation_Stairs: return stasisTipCtrl_Stairs ? stasisTipCtrl_Stairs : stasisTipCtrl_Rotation;
//            }
//            return null;
//        }

//        // =====================================================================
//        // Validaci�n
//        // =====================================================================
//        bool ValidateSetup()
//        {
//            if (!preset) return false;
//            if (!arm) return false;
//            if (!root) return false;
//            if (!rigObject) return false;
//            if (!GetBonePrefabForMode(preset.systemType)) return false;
//            if (!GetTipPrefabForMode(preset.systemType)) return false;

//            // --- NUEVO: si es un prefab asset, no validar ---
//            if (!gameObject.scene.IsValid())
//            {
//                return false;
//            }

//            // --- resto igual ---
//            if (!IsDescendantOf(root, arm) || !root.gameObject.scene.IsValid())
//            {
//                Debug.LogError("[IK] 'root' must be a Scene instance and a descendant of 'arm' (Animator).", this);
//                return false;
//            }
//            if (!IsDescendantOf(rigObject, arm) || !rigObject.gameObject.scene.IsValid())
//            {
//                Debug.LogError("[IK] 'rigObject' (Rig + ChainIKConstraint) must be a Scene instance and a descendant of 'arm'.", this);
//                return false;
//            }
//            return true;
//        }

//        static bool IsDescendantOf(Transform child, Transform potentialAncestor)
//        {
//            if (!child || !potentialAncestor) return false;
//            var t = child;
//            while (t != null)
//            {
//                if (t == potentialAncestor) return true;
//                t = t.parent;
//            }
//            return false;
//        }

//#if UNITY_EDITOR
//        int ComputePresetHash()
//        {
//            if (!preset) return 0;
//            unchecked { return (int)preset.systemType * 23 + preset.boneCount; }
//        }

//        static void ApplyToPathFollower1(Transform target, float speed, float distanceThreshold)
//        {
//            if (!target) return;
//            var mb = target.GetComponent<MonoBehaviour>();
//            if (!mb) return;
//            var so = new SerializedObject(mb);
//            TrySetFloat(so, "speed", speed);
//            TrySetFloat(so, "distanceThreshold", distanceThreshold);
//            so.ApplyModifiedProperties();
//        }

//        static void ApplyToFollowMultipleTargetController(Transform target, AnimationCurve remap, float arcHeight, float moveDelay, float travelTime, float stopDuration)
//        {
//            if (!target) return;
//            var mb = target.GetComponent<MonoBehaviour>();
//            if (!mb) return;
//            var so = new SerializedObject(mb);
//            TrySetCurve(so, "remapLerp", remap);
//            TrySetFloat(so, "arcHeight", arcHeight);
//            TrySetFloat(so, "moveDelay", moveDelay);
//            TrySetFloat(so, "travelTime", travelTime);
//            TrySetFloat(so, "stopDuration", stopDuration);
//            so.ApplyModifiedProperties();
//        }

//        static bool TrySetFloat(SerializedObject so, string prop, float value)
//        {
//            var p = so.FindProperty(prop);
//            if (p == null) return false;
//            p.floatValue = value;
//            return true;
//        }

//        static bool TrySetCurve(SerializedObject so, string prop, AnimationCurve curve)
//        {
//            var p = so.FindProperty(prop);
//            if (p == null) return false;
//            p.animationCurveValue = curve;
//            return true;
//        }
//#endif
//    }
//}
