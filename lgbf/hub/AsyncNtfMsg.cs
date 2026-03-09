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
        
        foreach(var item in data.GetValue("list").AsBsonArray)
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
            { "list", itemList }
        };
        return doc;
    }

    public void SendOfflineMsg(string msg)
    {
        AsyncNtfMsgList.Add(msg);
    }

    public void SetDirty(Action ifDirty)
    {
        ifDirty.Invoke();
    }
}