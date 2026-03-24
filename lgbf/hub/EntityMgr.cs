namespace hub;

public class EntityMgr
{
    private const int RetryDelayMs = 8;

    private async Task UnlockFunc(List<Func<Task>> unlockList)
    {
        foreach (var ul in unlockList)
        {
            await ul();
        }
        unlockList.Clear();
    }
    
    public async Task CallLockAndGetEntity(Context ctx, string[] entityIdes, Action<Entity, Entity[]> callback)
    {
        var lockEntities = new SortedSet<string>(){ctx.Guid};
        foreach (var entity in entityIdes)
        {
            lockEntities.Add(entity);
        }

        var self = new Entity(ctx);
        var retryDelay = RetryDelayMs;

        ReTry:
        var unlockList = new List<Func<Task>>(lockEntities.Count);
        try
        {
            var entities = new List<Entity>(entityIdes.Length);
            foreach (var entityId in lockEntities)
            {
                var token = Guid.NewGuid().ToString();
                var lockKey = string.Format(RedisHelp.EntityLockKey, entityId);
                var lockSuccess = await ctx.Redis!.TryLock(lockKey, token, 10_000);
                if (lockSuccess)
                {
                    unlockList.Add(async () => { await ctx.Redis.UnLock(lockKey, token); });
                    if (entityId != ctx.Guid)
                    {
                        entities.Add(new Entity(ctx.From(entityId)));
                    }
                }
                else
                {
                    break;
                }
            }

            if (unlockList.Count != lockEntities.Count)
            {
                await UnlockFunc(unlockList);
                await Task.Delay(retryDelay);
                retryDelay = Math.Min(retryDelay * 2, 256);
                goto ReTry;
            }
            callback(self, entities.ToArray());
        }
        catch (Exception ex)
        {
            Log.Trace("CallLockAndGetEntity failed:{0}", ex.Message);
        }
        finally
        {
            await UnlockFunc(unlockList);
        }
    }
}
