namespace hub;

public class EntityMgr
{
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

        ReTry:
        var unlockList = new List<Func<Task>>();
        try
        {
            Entity? self = null;
            var entities = new List<Entity>();
            foreach (var entityId in lockEntities)
            {
                var token = Guid.NewGuid().ToString();
                var lockSuccess = await ctx.Redis.TryLock(string.Format(RedisHelp.EntityLockKey, entityId), token, 10_000);
                if (lockSuccess)
                {
                    unlockList.Add(async () => { await ctx.Redis.UnLock(entityId, token); });
                    entities.Add(new Entity(ctx.From(entityId)));
                }
                else
                {
                    break;
                }
            }
            self = new Entity(ctx);

            if (entities.Count != entityIdes.Length)
            {
                await UnlockFunc(unlockList);
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