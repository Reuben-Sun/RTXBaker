using UnityEngine;

namespace Reuben.RTXBaker.Editor
{
    internal struct ProbeInfo
    {
        public Vector3 Position;
    }

    internal struct CubemapInfo
    {
        public Color[] colors;
    }

    public class SH9Color
    {
        public Color[] c = new Color[9];
    }

    public class SH9
    {
        public float[] c = new float[9];
    }
}