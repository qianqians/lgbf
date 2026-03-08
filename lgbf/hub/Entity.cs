using MongoDB.Bson;
namespace hub;

public interface IHostingData
{
    public static virtual string Type()
    {
        return string.Empty;
    }

    public static virtual IHostingData? Create()
    {
        return null;
    }

    public static virtual IHostingData? Load(BsonDocument data)
    {
        return null;
    }

    public abstract BsonDocument Store();

    public abstract bool IsDirty();
}

public interface IDataAgent<T> where T : IHostingData
{
    public T Data { get; set; }

    public abstract void WriteBack();
}

internal class DataAgent<T> : IDataAgent<T> where T : IHostingData
{
    private readonly Entity _entity;

    internal DataAgent(Entity entity)
    {
        _entity = entity;
    }

    public required T Data { get; set; }
    
    public void WriteBack()
    {
        _entity.Ctx.Redis.SetData(string.Format(RedisHelp.EntityStoreKey, T.Type(), _entity.Ctx.Guid), Data.Store().ToBson());
        _entity.Ctx.Redis.PushList(RedisHelp.EntityStoreMongodbList, _entity.Ctx.Guid);
    }
}

public record class Entity(Context Ctx)
{
    public async Task<T?> Get<T>() where T : IHostingData
    {
        var bin = await Ctx.Redis.GetData(string.Format(RedisHelp.EntityStoreKey, T.Type(), Ctx.Guid));
        var doc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<MongoDB.Bson.BsonDocument>(bin);
        var data = T.Load(doc);
        if (data != null)
        {
            return (T)data;
        }

        var t = new TaskCompletionSource<T?>();
        
        var _query = new DBQueryHelper();
        _query.Condition("player_guid", Ctx.Guid);
        var doc1 = await Ctx.Mongo.Find("game", "offline_msg", _query.query().ToBson(), 0, 0, "", false);
        var data1 = T.Load(doc1.ToBsonDocument());
        if (data1 != null)
        {
            return (T)data1;
        }
        
        return await t.Task;
    }
}