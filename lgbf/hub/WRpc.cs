using Google.ProtocolBuffers;

namespace hub;

public class WRpc
{
    private readonly Dictionary<string, Action<decimal>> callbackNtf = new();
    private readonly Dictionary<string, Func<decimal, decimal>> callbackRequest = new();
    private readonly Dictionary<string, Func<decimal, Task<decimal>>> callbackAsyncRequest = new();

    public void RegisterNtf(Action<decimal> callback)
    {
        callbackNtf.Add(callback.Method.Name, callback);
    }

    public void RegisterRequest(Func<decimal, decimal> callback)
    {
        callbackRequest.Add(callback.Method.Name, callback);
    }

    public void RegisterAsyncRequest(Func<decimal, Task<decimal>> callback)
    {
        callbackAsyncRequest.Add(callback.Method.Name, callback);
    }

    public void CallNtf(T argv) where T : global::ProtoBuf.IExtensible
    {
        callbackNtf.TryGetValue(T)
    }
}