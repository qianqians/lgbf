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
    private readonly string _storeKey;
    private readonly string _dirtyFlagKey;

    internal DataAgent(Entity entity)
    {
        _entity = entity;
        _storeKey = string.Format(RedisHelp.EntityStoreKey, T.Type(), _entity.Ctx.Guid);
        _dirtyFlagKey = string.Format(RedisHelp.EntityTickFlagKey, T.Type(), _entity.Ctx.Guid);
    }

    public required T Data { get; set; }
    
    public void WriteBack()
    {
        var bson = Data.Store().ToBson();
        _ = WriteBackAsync(bson);
    }

    private async Task WriteBackAsync(byte[] bson)
    {
        try
        {
            var redis = _entity.Ctx.Redis!;
            await redis.SetData(_storeKey, bson);

            var firstDirty = await redis.SetDataIfNotExists(_dirtyFlagKey, 1, 10 * 60 * 1000);
            if (!firstDirty)
            {
                return;
            }

            await redis.PushList(RedisHelp.EntityStoreMongodbList, new DirtyData()
            {
                Type = T.Type(),
                Guid = _entity.Ctx.Guid
            });
        }
        catch (Exception ex)
        {
            Log.Err("entity write back failed: {0}", ex.Message);
        }
    }
}

public record Entity(Context Ctx)
{
    public async Task<IDataAgent<T>?> Get<T>() where T : IHostingData
    {
        var storeKey = string.Format(RedisHelp.EntityStoreKey, T.Type(), Ctx.Guid);
        var bin = await Ctx.Redis!.GetData(storeKey);
        if (bin != null && bin.Length > 0)
        {
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
        }

        var query = new DBQueryHelper();
        query.Condition("player_guid", Ctx.Guid);
        var doc1 = await Ctx.Mongo!.Find(
            "game", T.Type(), query.query().ToBson(),
            0, 0, "", false);
        if (doc1 == null || doc1.Count == 0)
        {
            return null;
        }
        var data1 = T.Load(doc1[0].AsBsonDocument);
        if (data1 != null)
        {
            await Ctx.Redis.SetData(storeKey, data1.Store().ToBson());
            var agent = new DataAgent<T>(this)
            {
                Data = (T)data1
            };
            return agent;
        }
        
        return null;
    }
}
