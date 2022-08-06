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
        public int faceId;
    }
}