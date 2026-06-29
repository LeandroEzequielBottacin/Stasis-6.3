using System;
using UnityEngine;

namespace Player.Stasis
{
    [ExecuteAlways]
    public class MoveToTrailUV : MonoBehaviour
    {
        [Serializable]
        public struct MaterialData
        {
            public MaterialData(TrailRenderer trailRenderer, Material material, Vector2 uvScale, float move)
            {
                mTrailRenderer = trailRenderer;
                mUVTiling = uvScale;
                mMove = move;
            }
        
            public TrailRenderer mTrailRenderer;
            public Vector2 mUVTiling;
            [HideInInspector] public float mMove;
        }

#if UNITY_EDITOR
        //public bool m_overrideMaterial = true;
#endif
        public Transform mMoveObject;
        public string mShaderPropertyName = "_MoveToMaterialUV";
        public int mShaderPropertyID;
        public MaterialData[] mMaterialData = new MaterialData[1] { new MaterialData ( null, null, new Vector2(1, 1), 0f ) };

        private Vector3 m_beforePosW = Vector3.zero;
        void Start()
        {
            Initialize();
        }

        void LateUpdate()
        {
            if (mMoveObject == null)
                return;
            if (mMaterialData == null || mMaterialData.Length == 0)
                return;

            Vector3 nowPosW = mMoveObject.transform.position;
            if (nowPosW == m_beforePosW)
                return;
        
            float distance = Vector3.Distance(nowPosW, m_beforePosW);
            m_beforePosW = nowPosW;

            for (int i = 0; i < mMaterialData.Length; i++)
            {
                if (mMaterialData[i].mTrailRenderer == null)
                    continue;

                mMaterialData[i].mMove += distance * mMaterialData[i].mUVTiling.x;
                if (mMaterialData[i].mMove > 1f)
                {
                    mMaterialData[i].mMove = mMaterialData[i].mMove % 1f;
                }

                TrailRenderer trailRenderer = mMaterialData[i].mTrailRenderer;
                if (trailRenderer != null)
                {
                    Material mat = trailRenderer.sharedMaterial;
                    if (mat != null)
                    {
                        mat.SetFloat(mShaderPropertyID, mMaterialData[i].mMove);
                    }
                }
            }
        }

        public void Initialize()
        {
            if (mMaterialData == null || mMaterialData.Length == 0)
                return;
        
            mShaderPropertyID = Shader.PropertyToID(mShaderPropertyName);

            for (int i = 0; i < mMaterialData.Length; i++)
            {
                mMaterialData[i].mMove = 0f;
                TrailRenderer trailRenderer = mMaterialData[i].mTrailRenderer;
                if (trailRenderer != null)
                {
                    Material mat = trailRenderer.sharedMaterial;
                    if (mat != null)
                    {
                        mMaterialData[i].mUVTiling = mat.mainTextureScale;
                    }
                }
            }
        }
    }
}
