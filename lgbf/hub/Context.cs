
namespace hub;

public record class Context
{
    public required string Guid;
    public RedisHandle? Redis;
    public MongodbProxy? Mongo;
    public TimerService? Timer;

    public static Context New(string guid)
    {
        var ctx = new Context()
        {
            Guid = guid,
        };
        ctx.Redis = Main.Redis;
        ctx.Mongo = Main.Mongo;
        ctx.Timer = TimerService.Ins;
        
        return ctx;
    }
    
    public Context From(string guid)
    {
        return Context.New(guid);
    }
}