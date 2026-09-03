using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace ZCJ.Shiploader
{
    public readonly struct ShiploaderHttpResponse
    {
        public readonly long StatusCode;
        public readonly string Body;
        public readonly string TransportError;

        public ShiploaderHttpResponse(long statusCode, string body, string transportError = null)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
            TransportError = transportError;
        }

        public bool IsSuccess => string.IsNullOrEmpty(TransportError) && StatusCode is >= 200 and < 300;
    }

    public interface IShiploaderApiTransport
    {
        Task<ShiploaderHttpResponse> SendAsync(
            string method,
            string url,
            string jsonBody,
            int timeoutSeconds,
            CancellationToken cancellationToken);
    }

    public sealed class UnityWebRequestShiploaderTransport : IShiploaderApiTransport
    {
        public async Task<ShiploaderHttpResponse> SendAsync(
            string method,
            string url,
            string jsonBody,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            using UnityWebRequest request = new(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Math.Max(1, timeoutSeconds),
            };
            if (!string.IsNullOrEmpty(jsonBody))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }

            string transportError = request.result == UnityWebRequest.Result.ConnectionError
                ? request.error
                : null;
            return new ShiploaderHttpResponse(
                request.responseCode,
                request.downloadHandler?.text,
                transportError);
        }
    }
}
