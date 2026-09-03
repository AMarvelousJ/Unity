using System;
using UnityEngine;

namespace ZCJ.Shiploader
{
    [Serializable]
    public struct AxisRange
    {
        public float min;
        public float max;

        public AxisRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public readonly float Clamp(float value) => Mathf.Clamp(value, min, max);
        public readonly bool IsValid => max >= min;
    }

    [CreateAssetMenu(fileName = "SL15ModelConfig", menuName = "SL15/Model Config")]
    public sealed class SL15ModelConfig : ScriptableObject
    {
        [Header("Engineering dimensions (metres)")]
        public float railGauge = 23f;
        public float wheelBase = 22f;
        public float baseHeight = 12f;
        public float boomBaseHeightOffset = 1.8f;
        public float fixedBoomLength = 16.5f;
        public float boomExtensionTravel = 21.25f;
        public float telescopicOverlap = 3f;
        public float chuteLength = 12.6f;
        public float dischargeDrop = 0.6f;
        public float beltWidth = 2.4f;

        [Header("Visual dimensions (metres)")]
        public float portalTopLength = 10f;
        public float portalTopWidth = 8f;
        public float boomTrussWidth = 2.8f;
        public float boomTrussHeight = 2.5f;
        public float chuteTrunkRadius = 0.82f;

        [Header("Axis limits")]
        public AxisRange travelRange = new(-100f, 100f);
        public AxisRange slewRange = new(-35f, 215f);
        public AxisRange luffRange = new(-10f, 24f);
        public AxisRange extensionRange = new(0f, 21.25f);
        public AxisRange chuteRotateRange = new(-180f, 180f);

        [Header("Home pose")]
        public ShiploaderPose homePose = new(0f, 0f, 10f, 0f, 0f);

        public ShiploaderPose ClampPose(ShiploaderPose value)
        {
            value.travel = travelRange.Clamp(value.travel);
            value.slew = slewRange.Clamp(value.slew);
            value.boomLuff = luffRange.Clamp(value.boomLuff);
            value.boomExtension = extensionRange.Clamp(value.boomExtension);
            value.chuteRotate = chuteRotateRange.Clamp(value.chuteRotate);
            return value;
        }

        public bool Validate(out string message)
        {
            if (railGauge <= 0f || wheelBase <= 0f || baseHeight <= 0f)
            {
                message = "Rail gauge, wheel base and base height must be positive.";
                return false;
            }

            if (fixedBoomLength <= 0f || boomExtensionTravel < 0f || chuteLength <= 0f)
            {
                message = "Boom and chute dimensions are invalid.";
                return false;
            }

            if (telescopicOverlap <= 0f || telescopicOverlap > fixedBoomLength)
            {
                message = "Telescopic overlap must be positive and no longer than the fixed boom.";
                return false;
            }

            if (!travelRange.IsValid || !slewRange.IsValid || !luffRange.IsValid ||
                !extensionRange.IsValid || !chuteRotateRange.IsValid)
            {
                message = "One or more axis ranges are invalid.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public void ResetToWebDefaults()
        {
            railGauge = 23f;
            wheelBase = 22f;
            baseHeight = 12f;
            boomBaseHeightOffset = 1.8f;
            fixedBoomLength = 16.5f;
            boomExtensionTravel = 21.25f;
            telescopicOverlap = 3f;
            chuteLength = 12.6f;
            dischargeDrop = 0.6f;
            beltWidth = 2.4f;
            portalTopLength = 10f;
            portalTopWidth = 8f;
            boomTrussWidth = 2.8f;
            boomTrussHeight = 2.5f;
            chuteTrunkRadius = 0.82f;
            travelRange = new AxisRange(-100f, 100f);
            slewRange = new AxisRange(-35f, 215f);
            luffRange = new AxisRange(-10f, 24f);
            extensionRange = new AxisRange(0f, 21.25f);
            chuteRotateRange = new AxisRange(-180f, 180f);
            homePose = new ShiploaderPose(0f, 0f, 10f, 0f, 0f);
        }
    }
}
