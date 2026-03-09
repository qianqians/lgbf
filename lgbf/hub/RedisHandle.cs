using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace hub;

public class RedisHandle
{
    private ConnectionMultiplexer? _connectionMultiplexer;
    private IDatabase? _database;
    private readonly RedisConnectionHelper _connHelper;

    public RedisHandle(string connUrl, string pwd)
    {
        _connHelper = new RedisConnectionHelper(connUrl, "RedisForCache", pwd);
        _connHelper.ConnectOnStartup(ref _connectionMultiplexer, ref _database);
    }

    private void Recover(System.Exception e)
    {
        if (_connectionMultiplexer == null || _database == null)
        {
            return;
        }
        _connHelper.Recover(ref _connectionMultiplexer, ref _database, e);
    }

    public Task<bool> Expire(string key, int timeout)
    {
        while (true)
        {
            try
            {
                if (_database == null)
                {
                    return Task.FromResult(false);
                }
                return _database.KeyExpireAsync(key, System.TimeSpan.FromMilliseconds(timeout));
            }
            catch (RedisTimeoutException e)
            {
                Recover(e);
            }
        }
    }
    
    private Task<bool> SetStrData(string key, string data, int timeout)
    {
        while (true)
        {
            try
            {
                if (_database == null)
                {
                    return Task.FromResult(false);
                }
                
                if (timeout != 0)
                {
                    return _database.StringSetAsync(key, data, System.TimeSpan.FromMilliseconds(timeout));
                }
                else
                {
                    return _database.StringSetAsync(key, data);
                }
            }
            catch (RedisTimeoutException e)
            {
                Recover(e);
            }
        }
    }

    public Task<bool> SetData<T>(string key, T data, int timeout = 0)
    {
        return SetStrData(key, JsonConvert.SerializeObject(data), timeout);
    }

    private Task<RedisValue> GetStrData(string key)
    {
        while (true)
        {
            try
            {
                if (_database == null)
                {
                    return Task.FromResult(default(RedisValue));
                }

                return _database.StringGetAsync(key);
            }
            catch (RedisTimeoutException e)
            {
                Recover(e);
            }
        }
    }

    public async Task<byte[]?> GetData(string key)
    {
        byte[]? bin = await GetStrData(key);
        return bin;
    }

    public async ValueTask<T?> GetData<T>(string key)
    {
        string? json= await GetStrData(key);
        if (string.IsNullOrEmpty(json))
        {
            return default(T);
        }
        return JsonConvert.DeserializeObject<T>(json);
    }

    public bool DelData(string key)
    {
        while (true)
        {
            try
            {
                if (_database == null)
                {
                    return false;
                }
                
                return _database.KeyDelete(key);
            }
            catch (RedisTimeoutException e)
            {
                Recover(e);
            }
        }
    }

    public Task<long> PushList<T>(string key, T data)
    {
        while (true)
        {
            try
            {
                if (_database == null)
                {
                    return Task.FromResult((long)0);
                }
                
                return _database.ListLeftPushAsync(key, JsonConvert.SerializeObject(data));
            }
            catch (RedisTimeoutException e)
            {
                Recover(e);
            }
        }
    }

    public async Task<T?> PopList<T>(string key)
    {
        while (true)
        {
            try
            {
                if (_database == null)
                {
                    return default(T);
                }
                
                var data = await _database.ListLeftPopAsync(key);
                if (data.IsNull)
                {
                    return default(T);
                }
                
                return JsonConvert.DeserializeObject<T>(data.ToString());
            }
            catch (RedisTimeoutException e)
            {
                Recover(e);
            }
        }
    }

    public async ValueTask Lock(string key, string token, uint timeout)
    {
        var waitTime = 8;
        while (true)
        {
            try
            {
                if (_database == null)
                {
                    return;
                }
                
                var ret = await _database.LockTakeAsync(key, token, System.TimeSpan.FromMilliseconds(timeout));
                if (!ret)
                {
                    await Task.Delay(waitTime);
                    waitTime *= 2;
                    continue;
                }
                break;
            }
            catch (RedisTimeoutException e)
            {
                Recover(e);
            }
        }
    }

    public async ValueTask<bool> TryLock(string key, string token, uint timeout)
    {
        try
        {
            if (_database == null)
            {
                return false;
            }
            
            return await _database.LockTakeAsync(key, token, System.TimeSpan.FromMilliseconds(timeout));
        }
        catch (RedisTimeoutException e)
        {
            Recover(e);
        }
        
        return false;
    }

    public async ValueTask UnLock(string key, string token)
    {
        while (true)
        {
            try
            {
                if (_database == null)
                {
                    return;
                }
                
                await _database.LockReleaseAsync(key, token);
                break;
            }
            catch (RedisTimeoutException e)
            {
                Recover(e);
            }
        }
    }
}