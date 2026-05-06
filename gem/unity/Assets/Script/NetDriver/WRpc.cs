using Google.Protobuf;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Script.NetDriver
{
    public class Result<T>
    {
        public string ErrMsg;
        public T Content;
    }
    
    public class Result
    {
        public string ErrMsg;
    }
    
    public class WRpc
    {
        private const int DefaultTimeoutMs = 10000;
        private readonly string _token;
        private readonly string _uri;
        private readonly int _timeoutMs;
        
        public WRpc(string uri, string token, int timeoutMs = DefaultTimeoutMs)
        {
            _token = token;
            _uri = uri;
            _timeoutMs = timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs;
        }

        private async Task<Response> Post<T>(string method, T argv, string actionName) where T : IMessage<T>, new()
        {
            var request = new Request()
            {
                Token = _token,
                ProtoName = method,
                Content = argv.ToByteString()
            };
            var requestBytes = request.ToByteArray();
            using UnityWebRequest webRequest = new UnityWebRequest(_uri, UnityWebRequest.kHttpVerbPOST);
            webRequest.uploadHandler = new UploadHandlerRaw(requestBytes);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.timeout = Math.Max(1, (int)Math.Ceiling(_timeoutMs / 1000.0));
            webRequest.SetRequestHeader("Content-Type", "application/octet-stream");

            var operation = webRequest.SendWebRequest();
            var begin = Time.realtimeSinceStartupAsDouble;
            while (!operation.isDone)
            {
                var elapsedMs = (Time.realtimeSinceStartupAsDouble - begin) * 1000.0;
                if (elapsedMs >= _timeoutMs)
                {
                    webRequest.Abort();
                    throw new TimeoutException($"{actionName} failed: timeout after {_timeoutMs}ms");
                }

                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"{actionName} failed: {webRequest.error}");
            }

            var responseBytes = webRequest.downloadHandler.data;
            if (responseBytes == null || responseBytes.Length == 0)
            {
                throw new InvalidOperationException($"{actionName} failed: empty response body.");
            }
            
            return Response.Parser.ParseFrom(responseBytes);
        }

        public async Task<Result> Notify<T>(string method, T argv) where T : IMessage<T>, new()
        {
            var response = await Post(method, argv, $"WRpc.Notify({method})");
            if (!string.IsNullOrEmpty(response.ErrMsg))
            {
                Debug.Log($"WRpc.Notify response error: {response.ErrMsg}");
            }
            return new Result()
            {
                ErrMsg = response.ErrMsg,
            };
        }
        
        public async Task<Result<T1>> Request<T1, T2>(string method, T2 argv) 
            where T1 : IMessage<T1>, new()
            where T2 : IMessage<T2>, new()
        {
            var ret = new Result<T1>();
            var response = await Post(method, argv, $"WRpc.Request({method})");
            if (!string.IsNullOrEmpty(response.ErrMsg))
            {
                Debug.LogError($"WRpc.Request response error: {response.ErrMsg}");
                ret.ErrMsg = response.ErrMsg;
            }
            else
            {
                var parser = new MessageParser<T1>(() => new T1());
                ret.Content = parser.ParseFrom(response.Content);
            }
            return ret;
        }
    }
}
