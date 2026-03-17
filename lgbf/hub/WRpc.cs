using Google.Protobuf;

namespace hub;

public class WRpc
{
    private readonly Dictionary<string, Delegate> callbackNtf = new();

    public WRpc(string uri)
    {
        HttpService.Post(uri, (rsp) =>
        {
            var req = Request.Parser.ParseFrom(rsp.Data);
            if (req != null)
            {
                callbackNtf.TryGetValue(req.ProtoName, out var callback);
                var t = callback?.DynamicInvoke(rsp, req.CallGuid, req.Content);
                if (t != null)
                {
                    return (Task)t;
                }
            }
            return Task.FromResult("rpc request failed!");
        });
    }
        
    public void RegisterNtf<T>(Action<T> callback) where T : IMessage<T>, new()
    {
        var parser = new MessageParser<T>(() => new T());
        callbackNtf.Add(typeof(T).Name, async (HttpRsp rsp, string callGuid, byte[] data) =>
        {
            var r = new Response();
            try
            {
                var t = parser.ParseFrom(data);
                callback(t);

                r.CallGuid = callGuid;
                r.ErrMsg = "OK";
                r.Content = ByteString.CopyFromUtf8("OK");
            }
            catch (Exception ex)
            {
                r.CallGuid = callGuid;
                r.ErrMsg = "error";
                r.Content = ByteString.CopyFromUtf8(ex.Message);
            }
            finally
            {
                await rsp.Response(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, HttpService.BuildCrossHeaders(), r.ToByteArray());
            }
        });
    }
    
    public void RegisterAsyncNtf<T>(Func<T, Task> callback) where T : IMessage<T>, new()
    {
        var parser = new MessageParser<T>(() => new T());
        callbackNtf.Add(typeof(T).Name, async (HttpRsp rsp, string callGuid, byte[] data) =>
        {
            var r = new Response();
            try
            {
                var t = parser.ParseFrom(data);
                await callback(t);

                r.CallGuid = callGuid;
                r.ErrMsg = "OK";
                r.Content = ByteString.CopyFromUtf8("OK");
            }
            catch (Exception ex)
            {
                r.CallGuid = callGuid;
                r.ErrMsg = "error";
                r.Content = ByteString.CopyFromUtf8(ex.Message);
            }
            finally
            {
                await rsp.Response(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, HttpService.BuildCrossHeaders(), r.ToByteArray());
            }
        });
    }

    public void RegisterRequest<T1, T2>(Func<T1, T2> callback) 
        where T1 : IMessage<T1>, new()
        where T2 : IMessage<T2>, new()
    {
        var parser1 = new MessageParser<T1>(() => new T1());
        var parser2 = new MessageParser<T2>(() => new T2());
        callbackNtf.Add(typeof(T1).Name, async (HttpRsp rsp, string callGuid, byte[] data) =>
        {
            var r = new Response();
            try
            {
                var t = parser1.ParseFrom(data);
                var back = callback(t);

                r.CallGuid = callGuid;
                r.ErrMsg = "OK";
                r.Content = back.ToByteString();
            }
            catch (Exception ex)
            {
                r.CallGuid = callGuid;
                r.ErrMsg = "error";
                r.Content = ByteString.CopyFromUtf8(ex.Message);
            }
            finally
            {
                await rsp.Response(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, HttpService.BuildCrossHeaders(), r.ToByteArray());
            }
        });
    }

    public void RegisterAsyncRequest<T1, T2>(Func<T1, Task<T2>> callback) 
        where T1 : IMessage<T1>, new()
        where T2 : IMessage<T2>, new()
    {
        var parser1 = new MessageParser<T1>(() => new T1());
        var parser2 = new MessageParser<T2>(() => new T2());
        callbackNtf.Add(typeof(T1).Name, async (HttpRsp rsp, string callGuid, byte[] data) =>
        {
            var r = new Response();
            try
            {
                var t = parser1.ParseFrom(data);
                var back = await callback(t);

                r.CallGuid = callGuid;
                r.ErrMsg = "OK";
                r.Content = back.ToByteString();
            }
            catch (Exception ex)
            {
                r.CallGuid = callGuid;
                r.ErrMsg = "error";
                r.Content = ByteString.CopyFromUtf8(ex.Message);
            }
            finally
            {
                await rsp.Response(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, HttpService.BuildCrossHeaders(), r.ToByteArray());
            }
        });
    }
}