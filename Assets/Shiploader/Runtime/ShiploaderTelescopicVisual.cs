using UnityEngine;

namespace ZCJ.Shiploader
{
    // Fixed-size nested sections. Their displacement, not mesh scale, follows the engineering axis.
    [ExecuteAlways]
    public sealed class ShiploaderTelescopicVisual : MonoBehaviour
    {
        public ShiploaderRigController rig;
        public Transform[] sections;
        public float sectionLength;

        public void Refresh()
        {
            if (rig == null || sections == null || sections.Length == 0) return;
            float step = rig.Pose.boomExtension / sections.Length;
            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i] == null) continue;
                sections[i].localPosition = new Vector3(0, 0,
                    i == 0 ? rig.Config.fixedBoomLength - sectionLength + step : step);
                sections[i].localScale = Vector3.one;
            }
        }

        private void LateUpdate() => Refresh();
        private void OnEnable() => Refresh();
    }
}
