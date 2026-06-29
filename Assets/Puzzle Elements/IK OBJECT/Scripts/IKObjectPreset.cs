using UnityEngine;

namespace Puzzle_Elements.IK_OBJECT.Scripts
{
    public enum IKSystemType
    {
        IK_Distance,
        IK_DistanceInverse,
        IK_PlatformMovement,
        IK_PlatformRotation,
        IK_PlatformRotation_Stairs
    }

    [CreateAssetMenu(fileName = "IKObjectPreset", menuName = "IK/IKObjectPreset", order = 0)]
    public class IKObjectPreset : ScriptableObject
    {
        // modo actual
        public IKSystemType systemType = IKSystemType.IK_Distance;

        // comun a todos
        [Min(1)]
        public int boneCount = 3;

        // SOLO se usan en PlatformMovement
        public float movement_speed = 2.0f;
        public float movement_distanceThreshold = 0.05f;

        // SOLO se usan en PlatformRotation
        public AnimationCurve rotation_remapLerp = AnimationCurve.Linear(0, 0, 1, 1);
        public float rotation_arcHeight = 1.0f;
        public float rotation_moveDelay;
        public float rotation_travelTime = 1.0f;
        public float rotation_stopDuration;

        // SOLO se usan en PlatformRotation_Stairs
        public bool stairs_useRotationValues;
        public AnimationCurve stairs_remapLerp = AnimationCurve.Linear(0, 0, 1, 1);
        public float stairs_arcHeight = 1.0f;
        public float stairs_moveDelay;
        public float stairs_travelTime = 1.0f;
        public float stairs_stopDuration;
    }
}
