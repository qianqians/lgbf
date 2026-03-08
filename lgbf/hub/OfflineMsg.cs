using System.Diagnostics.Contracts;
using MongoDB.Bson;
namespace hub;

public class OfflineMsg : IHostingData
{
    public required List<string> OfflineMsgList;

    private bool _isDirty = false;
    
    public static string Type()
    {
        return "OfflineMsg";
    }

    public static IHostingData? Create()
    {
        return new OfflineMsg()
        {
            OfflineMsgList = new()
        };
    }

    public static IHostingData? Load(BsonDocument data)
    {
        var msgList = new OfflineMsg()
        {
            OfflineMsgList = new()
        };
        
        foreach(var item in data.GetValue("list").AsBsonArray)
        {
            msgList.OfflineMsgList.Add(item.AsString);
        }

        return msgList;
    }

    public BsonDocument Store()
    {
        var itemList = new BsonArray();
        foreach (var item in OfflineMsgList)
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
        OfflineMsgList.Add(msg);
        _isDirty = true;
    }

    public bool IsDirty()
    {
        return _isDirty;
    }
}