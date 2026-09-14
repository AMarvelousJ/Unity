using UnityEngine;

namespace ZCJ.Shiploader
{
    // Presentation-only tension member. It follows the existing boom; it does not drive any axis.
    [ExecuteAlways]
    public sealed class ShiploaderVisualStay : MonoBehaviour
    {
        public Transform tower;
        public Transform boom;
        public Vector3 towerOffset;
        public Vector3 boomOffset;
        public float diameter = 0.2f;

        public void Refresh()
        {
            if (tower == null || boom == null) return;
            Vector3 a = tower.TransformPoint(towerOffset);
            Vector3 b = boom.TransformPoint(boomOffset);
            transform.position = (a + b) * .5f;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            transform.localScale = new Vector3(diameter, Vector3.Distance(a, b) * .5f, diameter);
        }
        void LateUpdate() => Refresh();
        void OnEnable() => Refresh();
    }
}
