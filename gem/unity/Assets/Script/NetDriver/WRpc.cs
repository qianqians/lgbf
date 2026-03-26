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
    
    public class WRpc
    {
        private readonly string _token;
        private readonly string _uri;
        
        public WRpc(string uri, string token)
        {
            _token = token;
            _uri = uri;
        }

        public async Task Notify<T>(T argv) where T : IMessage<T>, new()
        {
            var request = new Request()
            {
                Token = _token,
                ProtoName = typeof(T).Name,
                Content = argv.ToByteString()
            };
            var requestBytes = request.ToByteArray();
            using UnityWebRequest webRequest = new UnityWebRequest(_uri, UnityWebRequest.kHttpVerbPOST);
            webRequest.uploadHandler = new UploadHandlerRaw(requestBytes);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/octet-stream");

            var operation = webRequest.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"WRpc.Notify failed: {webRequest.error}");
            }

            var responseBytes = webRequest.downloadHandler.data;
            if (responseBytes == null || responseBytes.Length == 0)
            {
                throw new InvalidOperationException("WRpc.Notify failed: empty response body.");
            }

            var response = Response.Parser.ParseFrom(responseBytes);
            if (!string.IsNullOrEmpty(response.ErrMsg))
            {
                Debug.Log($"WRpc.Notify response error: {response.ErrMsg}");
            }
        }
        
        public async Task<Result<T1>> Request<T1, T2>(T2 argv) 
            where T1 : IMessage<T1>, new()
            where T2 : IMessage<T2>, new()
        {
            var request = new Request()
            {
                Token = _token,
                ProtoName = typeof(T2).Name,
                Content = argv.ToByteString()
            };
            var requestBytes = request.ToByteArray();
            using UnityWebRequest webRequest = new UnityWebRequest(_uri, UnityWebRequest.kHttpVerbPOST);
            webRequest.uploadHandler = new UploadHandlerRaw(requestBytes);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/octet-stream");

            var operation = webRequest.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"WRpc.Request failed: {webRequest.error}");
            }

            var responseBytes = webRequest.downloadHandler.data;
            if (responseBytes == null || responseBytes.Length == 0)
            {
                throw new InvalidOperationException("WRpc.Request failed: empty response body.");
            }

            var ret = new Result<T1>();
            var response = Response.Parser.ParseFrom(responseBytes);
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
