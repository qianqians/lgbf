using MongoDB.Bson;
namespace hub;

public class Config
{
    public required string Host = string.Empty;
    public required int Port = 0;
    public required string RedisUrl = string.Empty;
    public required string RedisPwd = string.Empty;
    public required string MongoUrl = string.Empty;
}

public class Main
{
    private const int SaveBatchSize = 64;

    public static RedisHandle? Redis
    {
        get; private set;
    }

    public static MongodbProxy? Mongo
    {
        get; private set;
    }

    private static HttpService? _service;
    
    public static void Start(Config cfg)
    {
        Redis = new RedisHandle(cfg.RedisUrl, cfg.RedisPwd);
        Mongo = new MongodbProxy(cfg.MongoUrl);
        
        TimerService.Ins!.AddTickTime(5 * 60 * 1000, Save);
        
        _service = new HttpService(cfg.Host, cfg.Port);
        _service.Run();
    }

    public static void Run()
    {
        TimerService.Ins!.Poll();
    }

    public static async Task WaitClose()
    {
        if (_service != null)
        {
            await _service.Close();
        }
    }

    private static async void Save(long _)
    {
        try
        {
            if (Redis == null)
            {
                throw new Exception("internal error! redis is nil");
            }

            if (Mongo == null)
            {
                throw new Exception("internal error! mongo is nil");
            }

            for (var i = 0; i < SaveBatchSize; i++)
            {
                var data = await Redis.PopList<DirtyData>(RedisHelp.EntityStoreMongodbList);
                if (data == null)
                {
                    break;
                }

                var storeKey = string.Format(RedisHelp.EntityStoreKey, data.Type, data.Guid);
                var dirtyFlagKey = string.Format(RedisHelp.EntityTickFlagKey, data.Type, data.Guid);

                var data1 = await Redis.GetData(storeKey);
                if (data1 == null)
                {
                    Redis.DelData(dirtyFlagKey);
                    continue;
                }

                var query = new DBQueryHelper();
                query.Condition("player_guid", data.Guid);
                var update = new UpdateDataHelper();
                update.Set(MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(data1));

                var result = await Mongo.Update("game", data.Type, query.query().ToBson(), update.Data().ToBson(), true);
                if (!result)
                {
                    Log.Err("Save mongodb error");
                    continue;
                }

                Redis.DelData(dirtyFlagKey);
                var latestData = await Redis.GetData(storeKey);
                if (latestData != null && !latestData.SequenceEqual(data1))
                {
                    await Redis.SetData(dirtyFlagKey, 1, 10 * 60 * 1000);
                    await Redis.PushList(RedisHelp.EntityStoreMongodbList, data);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Err(ex.Message);
        }
        finally
        {
            TimerService.Ins!.AddTickTime(5 * 60 * 1000, Save);
        }
    }
}
