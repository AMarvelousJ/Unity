using UnityEngine;

namespace ZCJ.Shiploader
{
    [CreateAssetMenu(fileName = "SL15BackendConfig", menuName = "SL15/Backend Config")]
    public sealed class SL15BackendConfig : ScriptableObject
    {
        public string baseUrl = "http://127.0.0.1:8000";
        [Min(0.05f)] public float statePollInterval = 0.1f;
        [Min(0.2f)] public float heartbeatInterval = 1f;
        [Min(1f)] public float requestTimeout = 3f;
        [Min(0.2f)] public float reconnectInterval = 2f;
        [Min(0.2f)] public float staleSnapshotThreshold = 1f;

        public void ResetToDefaults()
        {
            baseUrl = "http://127.0.0.1:8000";
            statePollInterval = 0.1f;
            heartbeatInterval = 1f;
            requestTimeout = 3f;
            reconnectInterval = 2f;
            staleSnapshotThreshold = 1f;
        }

        public string BuildUrl(string path)
        {
            string root = string.IsNullOrWhiteSpace(baseUrl)
                ? "http://127.0.0.1:8000"
                : baseUrl.Trim().TrimEnd('/');
            return $"{root}/{path.TrimStart('/')}";
        }
    }
}
