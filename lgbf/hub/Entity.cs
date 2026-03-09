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

    public BsonDocument Store();
    
    public void SetDirty(Action ifDirty)
    {
        ifDirty.Invoke();
    }
}

public interface IDataAgent<T> where T : IHostingData
{
    public T Data { get; set; }

    public void WriteBack();
}

public class DirtyData
{
    public required string Type;
    public required string Guid;
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
        _entity.Ctx.Redis.PushList(RedisHelp.EntityStoreMongodbList, new DirtyData(){
            Type = T.Type(),
            Guid = _entity.Ctx.Guid
        });
    }
}

public record Entity(Context Ctx)
{
    public async Task<IDataAgent<T>?> Get<T>() where T : IHostingData
    {
        var bin = await Ctx.Redis.GetData(string.Format(RedisHelp.EntityStoreKey, T.Type(), Ctx.Guid));
        var doc = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(bin);
        var data = T.Load(doc);
        if (data != null)
        {
            var agent = new DataAgent<T>(this)
            {
                Data = (T)data
            };
            return agent;
        }

        var query = new DBQueryHelper();
        query.Condition("player_guid", Ctx.Guid);
        var doc1 = await Ctx.Mongo.Find(
            "game", "offline_msg", query.query().ToBson(),
            0, 0, "", false);
        if (doc1 == null || doc1.Count == 0)
        {
            return null;
        }
        var data1 = T.Load(doc1.IndexOf(0).ToBsonDocument());
        if (data1 != null)
        {
            var agent = new DataAgent<T>(this)
            {
                Data = (T)data1
            };
            return agent;
        }
        
        return null;
    }
}