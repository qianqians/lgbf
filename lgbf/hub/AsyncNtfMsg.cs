using System.Diagnostics.Contracts;
using MongoDB.Bson;
namespace hub;

public class AsyncNtfMsg : IHostingData
{
    public required List<string> AsyncNtfMsgList;

    public static string Type()
    {
        return "AsyncNtfMsg";
    }

    public static IHostingData? Create()
    {
        return new AsyncNtfMsg()
        {
            AsyncNtfMsgList = new()
        };
    }

    public static IHostingData? Load(BsonDocument data)
    {
        var msgList = new AsyncNtfMsg()
        {
            AsyncNtfMsgList = new()
        };
        
        foreach(var item in data.GetValue("Messages").AsBsonArray)
        {
            msgList.AsyncNtfMsgList.Add(item.AsString);
        }

        return msgList;
    }

    public BsonDocument Store()
    {
        var itemList = new BsonArray();
        foreach (var item in AsyncNtfMsgList)
        {
            itemList.Add(item);
        }
        var doc = new BsonDocument
        {
            { "Messages", itemList }
        };
        return doc;
    }

    public async Task SendOfflineMsg(Context ctx, string msg)
    {
        try
        {
            AsyncNtfMsgList.Add(msg);
            await ((IHostingData)this).SetDirty(async () =>
            {
                try
                {
                    var storeKey = string.Format(RedisHelp.EntityStoreKey, Type(), ctx.Guid);
                    if (ctx.Redis == null)
                    {
                        throw new Exception("SendOfflineMsg ctx.Redis is null!");
                    }

                    await ctx.Redis.SetData(storeKey, Store().ToBson());
                    await ctx.Redis.PushList(RedisHelp.EntityStoreMongodbList, new
                    {
                        Type = Type(),
                        Guid = ctx.Guid,
                    });
                }
                catch (Exception ex)
                {
                    Log.Err("SendOfflineMsg SetDirty callback ex:{0}", ex);
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            Log.Err("SendOfflineMsg ex:{0}", ex);
            throw;
        }
    }
}