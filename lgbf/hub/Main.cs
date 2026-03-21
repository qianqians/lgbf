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
    public static RedisHandle? Redis
    {
        get; private set;
    }

    public static MongodbProxy? Mongo
    {
        get; private set;
    }
    
    public static async Task Run(Config cfg)
    {
        Redis = new RedisHandle(cfg.RedisUrl, cfg.RedisPwd);
        Mongo = new MongodbProxy(cfg.MongoUrl);
        
        TimerService.Ins!.AddTickTime(5 * 60 * 1000, Save);
        
        var service = new HttpService(cfg.Host, cfg.Port);
        service.Run();

        await service.Close();
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

            for (var i = 0; i < 10; i++)
            {
                var data = await Redis.PopList<DirtyData>(RedisHelp.EntityStoreMongodbList);
                if (data == null)
                {
                    break;
                }

                var data1 = await Redis.GetData(string.Format(RedisHelp.EntityStoreKey, data.Type, data.Guid));
                if (data1 == null)
                {
                    break;
                }

                var query = new DBQueryHelper();
                query.Condition("player_guid", data.Guid);
                var result = await Mongo.Update("game", "player", query.query().ToBson(), data1, true);
                if (!result)
                {
                    Log.Err("Save mongodb error");
                    break;
                }
            }

            back:
            TimerService.Ins!.AddTickTime(5 * 60 * 1000, Save);
        }
        catch (Exception ex)
        {
            Log.Err(ex.Message);
        }
    }
}