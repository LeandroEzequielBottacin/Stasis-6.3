using UnityEngine;

namespace Resources
{
    public class CatmullRom
    {
        public static Vector3 CatmullRomCalc(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            // t: [0, 1]
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }
    }
}
