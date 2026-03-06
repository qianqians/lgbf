
namespace hub;

public record class Context(string Guid, RedisHandle Redis, TimerService Timer)
{
    public Context From(string guid)
    {
        return new Context(guid, Redis, Timer);
    }
}