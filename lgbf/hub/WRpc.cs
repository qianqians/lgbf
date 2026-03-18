using System.Text;
using Google.Protobuf;

namespace hub;

public class WRpc
{
    private readonly Dictionary<string, Delegate> callbackNtf = new();

    public WRpc(string uri)
    {
        HttpService.Post(uri, (rsp) =>
        {
            try
            {
                var req = Request.Parser.ParseFrom(rsp.Data);
                if (req == null)
                {
                    throw new Exception("rpc request failed!");
                }

                return Main.Redis!.GetData(key: string.Format(RedisHelp.EntityTokenConvertGuidKey, req.Token)).ContinueWith(avatarTask => {
                    var avatarId = avatarTask.Result;
                    if (avatarId == null)
                    {
                        throw new Exception("rpc request failed! wrong avatarId is nil!");
                    }

                    callbackNtf.TryGetValue(req.ProtoName, out var callback);
                    var t = callback?.DynamicInvoke(rsp, Encoding.UTF8.GetString(avatarId), req.CallGuid, req.Content);

                    return t is Task task ? task : Task.CompletedTask;
                }).Unwrap();
            }
            catch (Exception ex)
            {
                Log.Err("rpc request failed! {0}", ex);
                return Task.FromException(ex);
            }
        });
    }
        
    public void RegisterNtf<T>(Action<Context, T> callback) where T : IMessage<T>, new()
    {
        var parser = new MessageParser<T>(() => new T());
        callbackNtf.Add(typeof(T).Name, async (HttpRsp rsp, string? avatarId, string callGuid, byte[] data) =>
        {
            var r = new Response();
            try
            {
                var t = parser.ParseFrom(data);
                if (avatarId == null)
                {
                    throw new Exception("avatarId is nil!");
                }
                else
                {
                    callback(Context.New(avatarId), t);

                    r.CallGuid = callGuid;
                    r.ErrMsg = "OK";
                    r.Content = ByteString.CopyFromUtf8("OK");
                }
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
    
    public void RegisterAsyncNtf<T>(Func<Context, T, Task> callback) where T : IMessage<T>, new()
    {
        var parser = new MessageParser<T>(() => new T());
        callbackNtf.Add(typeof(T).Name, async (HttpRsp rsp, string? avatarId, string callGuid, byte[] data) =>
        {
            var r = new Response();
            try
            {
                var t = parser.ParseFrom(data);
                if (avatarId == null)
                {
                    throw new Exception("avatarId is nil!");
                }
                else
                {
                    await callback(Context.New(avatarId), t);

                    r.CallGuid = callGuid;
                    r.ErrMsg = "OK";
                    r.Content = ByteString.CopyFromUtf8("OK");
                }
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

    public void RegisterRequest<T1, T2>(Func<Context, T1, T2> callback) 
        where T1 : IMessage<T1>, new()
        where T2 : IMessage<T2>, new()
    {
        var parser1 = new MessageParser<T1>(() => new T1());
        callbackNtf.Add(typeof(T1).Name, async (HttpRsp rsp, string? avatarId, string callGuid, byte[] data) =>
        {
            var r = new Response();
            try
            {
                if (avatarId == null)
                {
                    throw new Exception("avatarId is nil!");
                }
                else
                {
                    var t = parser1.ParseFrom(data);
                    var back = callback(Context.New(avatarId), t);
                    
                    r.CallGuid = callGuid;
                    r.ErrMsg = "OK";
                    r.Content = back.ToByteString();
                }
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

    public void RegisterAsyncRequest<T1, T2>(Func<Context, T1, Task<T2>> callback) 
        where T1 : IMessage<T1>, new()
        where T2 : IMessage<T2>, new()
    {
        var parser1 = new MessageParser<T1>(() => new T1());
        callbackNtf.Add(typeof(T1).Name, async (HttpRsp rsp, string? avatarId, string callGuid, byte[] data) =>
        {
            var r = new Response();
            try
            {
                if (avatarId == null)
                {
                    throw new Exception("avatarId is nil!");
                }
                else
                {
                    var t = parser1.ParseFrom(data);
                    var back = await callback(Context.New(avatarId), t);

                    r.CallGuid = callGuid;
                    r.ErrMsg = "OK";
                    r.Content = back.ToByteString();
                }
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