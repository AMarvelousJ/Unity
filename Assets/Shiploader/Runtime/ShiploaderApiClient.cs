using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZCJ.Shiploader
{
    public sealed class ShiploaderApiException : Exception
    {
        public long StatusCode { get; }
        public string ErrorCode { get; }

        public ShiploaderApiException(long statusCode, string errorCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }

    public sealed class ShiploaderApiClient
    {
        private readonly SL15BackendConfig config;
        private readonly IShiploaderApiTransport transport;
        private readonly string prefix;

        public ShiploaderApiClient(SL15BackendConfig config, IShiploaderApiTransport transport, string prefix = "")
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.prefix = prefix;
        }

        public Task<JObject> HealthAsync(CancellationToken token) =>
            SendAsync<JObject>("GET", "/health", null, token);

        public Task<SimulationClockStatusDto> RegisterClockAsync(
            string clientId,
            CancellationToken token) =>
            SendAsync<SimulationClockStatusDto>(
                "PUT",
                $"/api/simulation/clock/clients/{Uri.EscapeDataString(clientId)}",
                new { client_name = "unity" },
                token);

        public async Task ReleaseClockAsync(string clientId, CancellationToken token)
        {
            await SendRawAsync(
                "DELETE",
                $"/api/simulation/clock/clients/{Uri.EscapeDataString(clientId)}",
                null,
                token);
        }

        public Task<LoadingPlanEnvelopeDto> GetLoadingPlanAsync(CancellationToken token) =>
            SendAsync<LoadingPlanEnvelopeDto>("GET", "/api/loading-plans/current", null, token);

        public Task<LoadingTaskDto> GetCurrentTaskAsync(CancellationToken token) =>
            SendAsync<LoadingTaskDto>("GET", "/api/loading-tasks/current", null, token);

        public Task<SimulationSnapshotDto> GetSnapshotAsync(CancellationToken token) =>
            SendAsync<SimulationSnapshotDto>("GET", "/api/state", null, token);

        public Task<SceneConfigDto> GetSceneAsync(CancellationToken token) =>
            SendAsync<SceneConfigDto>("GET", "/api/scene", null, token);

        public Task<LoadingTaskDto> StartTaskAsync(
            string requestId,
            string planId,
            CancellationToken token) =>
            SendAsync<LoadingTaskDto>(
                "POST",
                "/api/loading-tasks",
                new { request_id = requestId, plan_id = planId, mode = "demo" },
                token);

        public Task<LoadingTaskDto> PauseTaskAsync(string taskId, CancellationToken token) =>
            TaskCommandAsync(taskId, "pause", token);

        public Task<LoadingTaskDto> ResumeTaskAsync(string taskId, CancellationToken token) =>
            TaskCommandAsync(taskId, "resume", token);

        public Task<LoadingTaskDto> CancelTaskAsync(string taskId, CancellationToken token) =>
            TaskCommandAsync(taskId, "cancel", token);

        public Task<LoadingResetResponseDto> ResetTaskAsync(string taskId, CancellationToken token) =>
            SendAsync<LoadingResetResponseDto>(
                "POST",
                $"/api/loading-tasks/{Uri.EscapeDataString(taskId)}/reset",
                null,
                token);

        public Task<SceneConfigDto> SetHatchCoverAsync(
            string vesselId,
            string holdId,
            bool open,
            CancellationToken token) =>
            SendAsync<SceneConfigDto>(
                "PATCH",
                $"/api/scene/vessels/{Uri.EscapeDataString(vesselId)}/holds/{Uri.EscapeDataString(holdId)}/cover",
                new { cover_state = open ? "open" : "closed" },
                token);

        private Task<LoadingTaskDto> TaskCommandAsync(
            string taskId,
            string command,
            CancellationToken token) =>
            SendAsync<LoadingTaskDto>(
                "POST",
                $"/api/loading-tasks/{Uri.EscapeDataString(taskId)}/{command}",
                null,
                token);

        public async Task<T> SendAsync<T>(
            string method,
            string path,
            object body,
            CancellationToken token)
        {
            ShiploaderHttpResponse response = await SendRawAsync(method, path, body, token);
            if (string.IsNullOrWhiteSpace(response.Body) || response.StatusCode == 204)
            {
                return default;
            }
            return JsonConvert.DeserializeObject<T>(response.Body);
        }

        private async Task<ShiploaderHttpResponse> SendRawAsync(
            string method,
            string path,
            object body,
            CancellationToken token)
        {
            string json = body == null ? null : JsonConvert.SerializeObject(body);
            ShiploaderHttpResponse response = await transport.SendAsync(
                method,
                config.BuildUrl(prefix + path),
                json,
                MathfCeilToInt(config.requestTimeout),
                token);
            if (!response.IsSuccess)
            {
                throw CreateException(response);
            }
            return response;
        }

        private static ShiploaderApiException CreateException(ShiploaderHttpResponse response)
        {
            string code = string.IsNullOrEmpty(response.TransportError)
                ? "HTTP_ERROR"
                : "CONNECTION_ERROR";
            string message = response.TransportError ?? $"HTTP {response.StatusCode}";
            if (!string.IsNullOrWhiteSpace(response.Body))
            {
                try
                {
                    JToken detail = JObject.Parse(response.Body)["detail"];
                    if (detail is JObject detailObject)
                    {
                        code = detailObject.Value<string>("code") ?? code;
                        message = detailObject.Value<string>("message") ?? message;
                    }
                    else if (detail != null)
                    {
                        message = detail.ToString();
                    }
                }
                catch (JsonException)
                {
                    message = response.Body;
                }
            }
            return new ShiploaderApiException(response.StatusCode, code, message);
        }

        private static int MathfCeilToInt(float value) => Math.Max(1, (int)Math.Ceiling(value));
    }
}
