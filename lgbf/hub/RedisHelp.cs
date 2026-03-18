
namespace hub;

public static class RedisHelp
{
    public static readonly string EntityLockKey = "EntityLock:{0}";
    
    public static readonly string EntityTokenConvertGuidKey = "EntityTokenGuid:{0}";
    
    public static readonly string EntityStoreKey = "EntityStore{0}:{1}";

    public static readonly string EntityStoreMongodbList = "EntityStoreMongodbList";
}