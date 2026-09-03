using System;
using UnityEngine;

namespace ZCJ.Shiploader
{
    [Serializable]
    public struct ShiploaderKeyPoints
    {
        public Vector3 slewCenter;
        public Vector3 boomRoot;
        public Vector3 fixedBoomTip;
        public Vector3 boomTip;
        public Vector3 chuteTop;
        public Vector3 chuteBottom;
        public Vector3 dischargePoint;
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ShiploaderRigController : MonoBehaviour
    {
        [SerializeField] private SL15ModelConfig config;
        [SerializeField] private ShiploaderPose pose = new(0f, 0f, 10f, 0f, 0f);
        [SerializeField] private Vector3 rootBasePosition;

        [Header("Motion nodes")]
        [SerializeField] private Transform upperSlewAssembly;
        [SerializeField] private Transform boomLuffPivot;
        [SerializeField] private Transform telescopicScaleRoot;
        [SerializeField] private Transform boomHeadAttachment;
        [SerializeField] private Transform chuteAssembly;
        [SerializeField] private Transform chuteYawAssembly;
        [SerializeField] private Transform luffRopeFront;
        [SerializeField] private Transform mastTopMarker;

        [Header("Key point markers")]
        [SerializeField] private Transform slewCenterMarker;
        [SerializeField] private Transform boomRootMarker;
        [SerializeField] private Transform fixedBoomTipMarker;
        [SerializeField] private Transform boomTipMarker;
        [SerializeField] private Transform chuteTopMarker;
        [SerializeField] private Transform chuteBottomMarker;
        [SerializeField] private Transform dischargeMarker;

        private ShiploaderPose lastAppliedPose;
        private bool hasAppliedPose;
        private bool remoteControlLocked;

        public SL15ModelConfig Config => config;
        public ShiploaderPose Pose => pose;
        public Transform BoomHeadAttachment => boomHeadAttachment;
        public Transform DischargeMarker => dischargeMarker;
        public bool RemoteControlLocked => remoteControlLocked;

        public void SetRemoteControlLocked(bool locked)
        {
            remoteControlLocked = locked;
        }

        public void Configure(
            SL15ModelConfig modelConfig,
            Transform upper,
            Transform luffPivot,
            Transform telescope,
            Transform boomHead,
            Transform chute,
            Transform chuteYaw,
            Transform luffRope,
            Transform mastTop,
            Transform slewCenter,
            Transform boomRoot,
            Transform fixedBoomTip,
            Transform boomTip,
            Transform chuteTop,
            Transform chuteBottom,
            Transform discharge)
        {
            config = modelConfig;
            rootBasePosition = transform.localPosition;
            upperSlewAssembly = upper;
            boomLuffPivot = luffPivot;
            telescopicScaleRoot = telescope;
            boomHeadAttachment = boomHead;
            chuteAssembly = chute;
            chuteYawAssembly = chuteYaw;
            luffRopeFront = luffRope;
            mastTopMarker = mastTop;
            slewCenterMarker = slewCenter;
            boomRootMarker = boomRoot;
            fixedBoomTipMarker = fixedBoomTip;
            boomTipMarker = boomTip;
            chuteTopMarker = chuteTop;
            chuteBottomMarker = chuteBottom;
            dischargeMarker = discharge;
            ApplyPose(modelConfig != null ? modelConfig.homePose : pose);
        }

        public void ApplyPose(ShiploaderPose requestedPose)
        {
            if (config == null || upperSlewAssembly == null || boomLuffPivot == null ||
                telescopicScaleRoot == null || boomHeadAttachment == null ||
                chuteAssembly == null || chuteYawAssembly == null)
            {
                return;
            }

            pose = config.ClampPose(requestedPose);
            transform.localPosition = rootBasePosition + Vector3.right * pose.travel;
            upperSlewAssembly.localRotation = Quaternion.Euler(0f, -pose.slew, 0f);
            boomLuffPivot.localRotation = Quaternion.Euler(-pose.boomLuff, 0f, 0f);

            float telescopeLength = config.telescopicOverlap + pose.boomExtension;
            telescopicScaleRoot.localScale = new Vector3(1f, 1f, telescopeLength);
            boomHeadAttachment.localPosition = new Vector3(0f, 0f, config.fixedBoomLength + pose.boomExtension);

            chuteAssembly.position = boomHeadAttachment.position;
            chuteAssembly.rotation = Quaternion.Euler(0f, -pose.slew, 0f);
            chuteYawAssembly.localRotation = Quaternion.Euler(0f, -pose.chuteRotate, 0f);
            UpdateLuffRope();

            lastAppliedPose = pose;
            hasAppliedPose = true;
        }

        public void ResetPose()
        {
            if (config != null)
            {
                ApplyPose(config.homePose);
            }
        }

        public ShiploaderKeyPoints GetActualKeyPoints()
        {
            return new ShiploaderKeyPoints
            {
                slewCenter = MarkerPosition(slewCenterMarker),
                boomRoot = MarkerPosition(boomRootMarker),
                fixedBoomTip = MarkerPosition(fixedBoomTipMarker),
                boomTip = MarkerPosition(boomTipMarker),
                chuteTop = MarkerPosition(chuteTopMarker),
                chuteBottom = MarkerPosition(chuteBottomMarker),
                dischargePoint = MarkerPosition(dischargeMarker),
            };
        }

        public ShiploaderKeyPoints GetExpectedKeyPoints()
        {
            if (config == null)
            {
                return default;
            }

            Vector3 root = transform.position;
            Vector3 slewCenter = root + Vector3.up * config.baseHeight;
            Vector3 boomRoot = slewCenter + Vector3.up * config.boomBaseHeightOffset;
            Quaternion directionRotation = Quaternion.Euler(0f, -pose.slew, 0f) *
                                           Quaternion.Euler(-pose.boomLuff, 0f, 0f);
            Vector3 direction = directionRotation * Vector3.forward;
            Vector3 fixedTip = boomRoot + direction * config.fixedBoomLength;
            Vector3 boomTip = boomRoot + direction * (config.fixedBoomLength + pose.boomExtension);
            Vector3 chuteBottom = boomTip + Vector3.down * config.chuteLength;

            return new ShiploaderKeyPoints
            {
                slewCenter = slewCenter,
                boomRoot = boomRoot,
                fixedBoomTip = fixedTip,
                boomTip = boomTip,
                chuteTop = boomTip,
                chuteBottom = chuteBottom,
                dischargePoint = chuteBottom + Vector3.down * config.dischargeDrop,
            };
        }

        public float GetMaximumKeyPointError()
        {
            ShiploaderKeyPoints actual = GetActualKeyPoints();
            ShiploaderKeyPoints expected = GetExpectedKeyPoints();
            float maximum = 0f;
            maximum = Mathf.Max(maximum, Vector3.Distance(actual.slewCenter, expected.slewCenter));
            maximum = Mathf.Max(maximum, Vector3.Distance(actual.boomRoot, expected.boomRoot));
            maximum = Mathf.Max(maximum, Vector3.Distance(actual.fixedBoomTip, expected.fixedBoomTip));
            maximum = Mathf.Max(maximum, Vector3.Distance(actual.boomTip, expected.boomTip));
            maximum = Mathf.Max(maximum, Vector3.Distance(actual.chuteTop, expected.chuteTop));
            maximum = Mathf.Max(maximum, Vector3.Distance(actual.chuteBottom, expected.chuteBottom));
            maximum = Mathf.Max(maximum, Vector3.Distance(actual.dischargePoint, expected.dischargePoint));
            return maximum;
        }

        private void OnEnable()
        {
            ApplyPose(pose);
        }

        private void OnValidate()
        {
            ApplyPose(pose);
        }

        private void Update()
        {
            if (!hasAppliedPose || !pose.Approximately(lastAppliedPose))
            {
                ApplyPose(pose);
            }
        }

        private void UpdateLuffRope()
        {
            if (luffRopeFront == null || mastTopMarker == null || boomLuffPivot == null)
            {
                return;
            }

            Vector3 start = mastTopMarker.position;
            Vector3 end = boomLuffPivot.TransformPoint(new Vector3(0f, 0.85f, 10f));
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length < 0.0001f)
            {
                return;
            }

            luffRopeFront.position = (start + end) * 0.5f;
            luffRopeFront.rotation = Quaternion.FromToRotation(Vector3.up, direction / length);
            luffRopeFront.localScale = new Vector3(0.055f, length, 0.055f);
        }

        private static Vector3 MarkerPosition(Transform marker)
        {
            return marker != null ? marker.position : new Vector3(float.NaN, float.NaN, float.NaN);
        }
    }
}
