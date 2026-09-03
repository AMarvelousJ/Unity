using System;
using UnityEngine;

namespace ZCJ.Shiploader
{
    [Serializable]
    public struct ShiploaderPose
    {
        public float travel;
        public float slew;
        public float boomLuff;
        public float boomExtension;
        public float chuteRotate;

        public ShiploaderPose(float travel, float slew, float boomLuff, float boomExtension, float chuteRotate)
        {
            this.travel = travel;
            this.slew = slew;
            this.boomLuff = boomLuff;
            this.boomExtension = boomExtension;
            this.chuteRotate = chuteRotate;
        }

        public readonly bool Approximately(ShiploaderPose other, float tolerance = 0.0001f)
        {
            return Mathf.Abs(travel - other.travel) <= tolerance &&
                   Mathf.Abs(slew - other.slew) <= tolerance &&
                   Mathf.Abs(boomLuff - other.boomLuff) <= tolerance &&
                   Mathf.Abs(boomExtension - other.boomExtension) <= tolerance &&
                   Mathf.Abs(chuteRotate - other.chuteRotate) <= tolerance;
        }
    }
}
