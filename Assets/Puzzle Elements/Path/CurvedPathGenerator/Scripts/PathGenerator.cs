//=========================================================================================================================================
//      ,------.   ,---. ,--------.,--.  ,--.     ,----.   ,------.,--.  ,--.,------.,------.   ,---. ,--------. ,-----. ,------.
//      |  .--. ' /  O  \'--.  .--'|  '--'  |    '  .-./   |  .---'|  ,'.|  ||  .---'|  .--. ' /  O  \'--.  .--''  .-.  '|  .--. '
//      |  '--' ||  .-.  |  |  |   |  .--.  |    |  | .---.|  `--, |  |' '  ||  `--, |  '--'.'|  .-.  |  |  |   |  | |  ||  '--'.'
//      |  | --' |  | |  |  |  |   |  |  |  |    '  '--'  ||  `---.|  | `   ||  `---.|  |\  \ |  | |  |  |  |   '  '-'  '|  |\  \
//      `--'     `--' `--'  `--'   `--'  `--'     `------' `------'`--'  `--'`------'`--' '--'`--' `--'  `--'    `-----' `--' '--'
//=========================================================================================================================================
//
//  PATH GENERATOR CLASS  (dynamic / parent-following)
//  Script to make followable path based on Bézier curve.
//  This version rebuilds world-space nodes each time the path is calculated,
//  so the path follows its moving parent in Play mode (and optionally in Editor).
//
//  2023.11.04 _ KimYC1223
//  2025.10.09 _ Patch: Rebuild world positions from local each UpdatePath; optional TRS change detection.
//=========================================================================================================================================

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
#endif

namespace Puzzle_Elements.Path.CurvedPathGenerator.Scripts
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [Serializable]
    public class PathGenerator : MonoBehaviour
    {
        //============================== Public Settings ==============================

        /// <summary>Is this path closed?</summary>
        public bool IsClosed;

        /// <summary>Recalculate every frame (runtime)?</summary>
        public bool IsLivePath;

        /// <summary>Show icons in the editor?</summary>
        public bool IsShowingIcons = true;

        /// <summary>Density of guide points between nodes (>=2)</summary>
        public int PathDensity = 5;

        /// <summary>(Editor Only) edit mode flag</summary>
        public int EditMode;

        /// <summary>Create the mesh of the path</summary>
        public bool CreateMeshFlag = true;

        /// <summary>Line mesh width</summary>
        public float LineMehsWidth = 0.2f;

        /// <summary>Texture opacity</summary>
        public float LineOpacity = 0.7f;

        /// <summary>Texture scrolling speed</summary>
        public float LineSpeed = 10f;

        /// <summary>Y-Axis tiling</summary>
        public float LineTiling = 20f;

        /// <summary>Filling amount of material (0..1)</summary>
        public float LineFilling = 1f;

        /// <summary>Render queue</summary>
        public int LineRenderQueue = 2500;

        /// <summary>Line mesh texture</summary>
        public Texture2D LineTexture;

        /// <summary>World path points (calculated)</summary>
        public List<Vector3> PathList = new List<Vector3>();

        /// <summary>Cumulative lengths along path (calculated)</summary>
        public List<float> PathLengths = new List<float>();

        /// <summary>Local-space Node list (authoring)</summary>
        [SerializeField] public List<Vector3> NodeList = new List<Vector3>();

        /// <summary>Local-space Handle/Angle list (authoring)</summary>
        [SerializeField] public List<Vector3> AngleList = new List<Vector3>();

        /// <summary>World-space Node list (calculated)</summary>
        public List<Vector3> NodeList_World = new List<Vector3>();

        /// <summary>World-space Handle/Angle list (calculated)</summary>
        public List<Vector3> AngleList_World = new List<Vector3>();

        //============================== Private Cache ==============================
        // Optional: only recalc when TRS changes (if IsLivePath is false)
        Matrix4x4 _lastLocalToWorld;

        //============================== Lifecycle ==============================

        private void Awake()
        {
            _lastLocalToWorld = transform.localToWorldMatrix;
            UpdatePath();
        }

        private void Update()
        {
            // If live, always recalc. Otherwise, recalc only when TRS changed.
            if (IsLivePath || _lastLocalToWorld != transform.localToWorldMatrix)
            {
                _lastLocalToWorld = transform.localToWorldMatrix;
                UpdatePath();
            }
        }

        //============================== Core ==============================

        /// <summary>
        /// Calculate & Generate Path
        /// </summary>
        public void UpdatePath()
        {
            try
            {
                // NEW: rebuild world lists from local each time (so path follows parent motion)
                RebuildWorldFromLocal();

                PathList = new List<Vector3>();
                PathLengths = new List<float>();

                // Safety check
                if (PathDensity < 2)
                {
#if UNITY_EDITOR
                    Debug.LogError("Path Density is too small. (must >= 2)");
                    EditorApplication.isPlaying = false;
#elif UNITY_WEBPLAYER
                    Application.OpenURL("about:blank");
#else
                    Application.Quit();
#endif
                    return;
                }

                // Generate path with quadratic Bézier between each node pair
                for (int i = 0; i < NodeList_World.Count; i++)
                {
                    Vector3 startPoint = NodeList_World[i];
                    Vector3 middlePoint;
                    Vector3 endPoint;

                    if (i == NodeList_World.Count - 1)
                    {
                        if (IsClosed)
                        {
                            middlePoint = AngleList_World[i];
                            endPoint = NodeList_World[0];
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        middlePoint = AngleList_World[i];
                        endPoint = NodeList_World[i + 1];
                    }

                    for (int j = 0; j < PathDensity; j++)
                    {
                        float t = (float)j / PathDensity;

                        Vector3 curve =
                            (1f - t) * (1f - t) * startPoint +
                            2f * (1f - t) * t * middlePoint +
                            t * t * endPoint;

                        PathList.Add(curve);

                        if (PathList.Count == 2)
                        {
                            float length = (PathList[0] - curve).magnitude;
                            PathLengths.Add(length);
                        }
                        else if (PathList.Count > 2)
                        {
                            float length = (PathList[PathList.Count - 2] - curve).magnitude;
                            PathLengths.Add(PathLengths[PathLengths.Count - 1] + length);
                        }
                    }
                }

                // Close or cap the path
                if (IsClosed)
                    PathList.Add(NodeList_World[0]);
                else
                    PathList.Add(NodeList_World[NodeList_World.Count - 1]);

                // Visualize the calculated path
                CreateMesh(PathList);

                float l = (PathList[PathList.Count - 2] - PathList[PathList.Count - 1]).magnitude;
                PathLengths.Add(PathLengths[PathLengths.Count - 1] + l);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Get total length of the calculated path.</summary>
        public float GetLength()
        {
            if (PathLengths != null && PathLengths.Count > 0)
                return PathLengths[PathLengths.Count - 1];
            return 0f;
        }

        //============================== Editor Gizmos ==============================

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            UnityEditor.Tools.hidden = (EditMode != 0);

            // Keep icons following the parent in Editor as well
            RebuildWorldFromLocal();

            if (IsShowingIcons)
            {
                Gizmos.DrawIcon(transform.position, "PathGenerator/PG_Anchor.png", true);

                if (NodeList_World != null && NodeList_World.Count > 0)
                {
                    for (int i = 0; i < NodeList_World.Count; i++)
                    {
                        if (i == 0)
                        {
                            Gizmos.DrawIcon(NodeList_World[i], "PathGenerator/PG_Start.png", (EditMode != 0));
                        }
                        else if (!IsClosed && i == NodeList_World.Count - 1)
                        {
                            Gizmos.DrawIcon(NodeList_World[i], "PathGenerator/PG_End.png", (EditMode != 0));
                        }
                        else
                        {
                            Gizmos.DrawIcon(NodeList_World[i], "PathGenerator/PG_Node.png", (EditMode != 0));
                        }
                    }
                }

                if (AngleList_World != null && AngleList_World.Count > 0)
                {
                    for (int i = 0; i < AngleList_World.Count; i++)
                    {
                        Gizmos.DrawIcon(AngleList_World[i], "PathGenerator/PG_Handler.png", (EditMode != 0));
                    }
                }
            }
#endif
        }

        public void ResetTools()
        {
#if UNITY_EDITOR
            UnityEditor.Tools.hidden = false;
#endif
        }

        //============================== Mesh Creation ==============================

        private void CreateMesh(List<Vector3> pathVec)
        {
            if (!CreateMeshFlag) return;

            Quaternion rotation = transform.rotation;
            Matrix4x4 m_reverse = Matrix4x4.Rotate(Quaternion.Inverse(rotation));

            int verNum = 2 * pathVec.Count;
            int triNum = 6 * (pathVec.Count - 1);
            Vector3[] vertices = new Vector3[verNum];
            int[] triangles = new int[triNum];
            Vector2[] uvs = new Vector2[verNum];

            float MaxLength = 0, currentLength = 0;
            for (int i = 1; i < pathVec.Count; i++)
                MaxLength += (pathVec[i] - pathVec[i - 1]).magnitude;

            for (int i = 0; i < pathVec.Count - 1; i++)
            {
                Vector3 dir = (pathVec[i + 1] - pathVec[i]).normalized;
                Vector3 new_dir1 = new Vector3(dir.z, 0, -dir.x);
                Vector3 new_dir2 = new Vector3(-dir.z, 0, dir.x);

                if (i == 0)
                {
                    vertices[2 * i] = ReverseTransformPoint(pathVec[i] + (new_dir1 * (LineMehsWidth / 2)), m_reverse);
                    vertices[2 * i + 1] = ReverseTransformPoint(pathVec[i] + (new_dir2 * (LineMehsWidth / 2)), m_reverse);
                    uvs[2 * i] = new Vector2(0.5f, -0.5f);
                    uvs[2 * i + 1] = new Vector2(-0.5f, -0.5f);
                }
                else
                {
                    currentLength += (pathVec[i] - pathVec[i - 1]).magnitude;

                    vertices[2 * i] = ReverseTransformPoint(pathVec[i] + (new_dir1 * (LineMehsWidth / 2)), m_reverse);
                    vertices[2 * i + 1] = ReverseTransformPoint(pathVec[i] + (new_dir2 * (LineMehsWidth / 2)), m_reverse);
                    uvs[2 * i] = new Vector2(0.5f, -0.5f + (currentLength) / (MaxLength));
                    uvs[2 * i + 1] = new Vector2(-0.5f, -0.5f + (currentLength) / (MaxLength));
                }

                if (i == pathVec.Count - 2)
                {
                    vertices[2 * i + 2] = ReverseTransformPoint(pathVec[i + 1] + (new_dir1 * (LineMehsWidth / 2)), m_reverse);
                    vertices[2 * i + 3] = ReverseTransformPoint(pathVec[i + 1] + (new_dir2 * (LineMehsWidth / 2)), m_reverse);
                    uvs[2 * i + 2] = new Vector2(0.5f, 0.5f);
                    uvs[2 * i + 3] = new Vector2(-0.5f, 0.5f);
                }
            }

            for (int i = 0; i < pathVec.Count - 1; i++)
            {
                triangles[6 * i] = 2 * i + 3;
                triangles[6 * i + 1] = 2 * i + 2;
                triangles[6 * i + 2] = 2 * i;
                triangles[6 * i + 3] = 2 * i + 3;
                triangles[6 * i + 4] = 2 * i;
                triangles[6 * i + 5] = 2 * i + 1;
            }

            MeshFilter pathMesh = transform.GetComponent<MeshFilter>();
            Mesh newMesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles,
                uv = uvs
            };
            newMesh.RecalculateBounds();
            newMesh.RecalculateNormals();
            pathMesh.mesh = newMesh;
        }

        //============================== Helpers ==============================

        /// <summary>
        /// Convert local Node/Angle lists to world lists using current Transform.
        /// This makes the path follow its moving parent.
        /// </summary>
        private void RebuildWorldFromLocal()
        {
            if (NodeList_World == null) NodeList_World = new List<Vector3>();
            if (AngleList_World == null) AngleList_World = new List<Vector3>();
            NodeList_World.Clear();
            AngleList_World.Clear();

            int count = Mathf.Min(NodeList.Count, AngleList.Count);

            for (int i = 0; i < count; i++)
            {
                NodeList_World.Add(transform.TransformPoint(NodeList[i]));
                AngleList_World.Add(transform.TransformPoint(AngleList[i]));
            }

            // If there is a trailing node without a handle (common in open paths), include it
            if (NodeList.Count > count)
                NodeList_World.Add(transform.TransformPoint(NodeList[count]));
        }

        /// <summary>
        /// Convert world -> local relative to this transform (reverse of TransformPoint).
        /// </summary>
        private Vector3 ReverseTransformPoint(Vector3 points, Matrix4x4 m_reverse)
        {
            Vector3 result = points;

            result -= transform.position;                   // Move
            result = m_reverse.MultiplyPoint3x4(result);    // Rotate
            result = new Vector3(                           // Scale
                result.x / transform.lossyScale.x,
                result.y / transform.lossyScale.y,
                result.z / transform.lossyScale.z
            );
            return result;
        }
    }
}
