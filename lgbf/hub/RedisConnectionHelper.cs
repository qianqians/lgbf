using System.Globalization;
using StackExchange.Redis;

namespace hub;

public class RedisConnectionHelper
{
    private static readonly int ConnectRetry = 3;
    private static readonly int ConnectTimeout = 5000;
    private static readonly int KeepAlive = 30;
    private static readonly ManualResetEvent WaitNotify = new ManualResetEvent(false);

    private readonly int _waitTimeout = 15000; //15s
    private readonly string _conUrl;
    private readonly string _conName;
    private readonly string _pwd;
    private readonly string _conf;
    private readonly int _db;
    private int _recoverCnt = 0;
    private int _inRecover = 0;


    public RedisConnectionHelper(string conUrl, string conName, string pwd, int db = 0)
    {
        _conUrl = conUrl;
        _conName = conName;
        _pwd = pwd;
        _db = db;
        _conf = BuildConfig(conUrl, conName);
    }

    public void ConnectOnStartup(ref ConnectionMultiplexer? connectionMultiplexer, ref IDatabase? database)
    {
        try
        {
            if (connectionMultiplexer != null)
            {
                connectionMultiplexer.Close(allowCommandsToComplete: false);
            }
            
            connectionMultiplexer = ConnectionMultiplexer.Connect(_conf);
            database = connectionMultiplexer.GetDatabase(_db);
        }
        catch (StackExchange.Redis.RedisConnectionException ex)
        {
            Log.Err("Can NOT connect to Redis! connectRetry={0}, connectTimeout={1}ms, conex:{2}, _conf:{3}", 
                ConnectRetry, ConnectTimeout, ex, _conf);
            throw;
        }
    }

    public void Recover(ref ConnectionMultiplexer connectionMultiplexer, ref IDatabase database, Exception e, Action? afterRecover = null)
    {
        if (Interlocked.CompareExchange(ref _inRecover, 1, 0) == 0)
        {
            Log.Err("Redis Exception: {0}", e);
            Log.Info("Reconnect for {0}, count={1}", _conName, ++_recoverCnt);
            try
            {
                connectionMultiplexer.Close(allowCommandsToComplete: false);
                
                connectionMultiplexer = ConnectionMultiplexer.Connect(_conf);
                database = connectionMultiplexer.GetDatabase(_db);
            }
            catch (StackExchange.Redis.RedisConnectionException)
            {
                Log.Err("Exit due to Recover-Failure! RecoverCount={0}, connectRetry={1}, connectTimeout={2}ms, _conf={3}", 
                    _recoverCnt, ConnectRetry, ConnectTimeout, _conf);
                Thread.Sleep(10);
                Environment.Exit(1);
            }
            
            afterRecover?.Invoke();
            _inRecover = 0;
            if (!WaitNotify.Set())
            {
                Log.Err("_waitNotify.Set() failed");
            }
            Thread.Sleep(10);
            if (!WaitNotify.Reset())
            {
                Log.Err("_waitNotify.ReSet() failed");
            }
        }
        else
        {
            if (!WaitNotify.WaitOne(_waitTimeout))
            {
                var msg = $"_waitNotifyTimeout after {_waitTimeout}ms";
                Log.Err(msg);
                Thread.Sleep(10);
                Environment.Exit(1);
            }
        }
    }


    string BuildConfig(string conUrl, string conName)
    {
        Span<char> buf = stackalloc char[512];
        if (string.IsNullOrEmpty(_pwd))
        {
            return string.Create(CultureInfo.InvariantCulture, buf, $"{conUrl}, " + 
                $"connectRetry={ConnectRetry},connectTimeout={ConnectTimeout}," +
                $"keepAlive={KeepAlive},resolveDns={true},name={conName}");
        }
        return string.Create(CultureInfo.InvariantCulture, buf, $"{conUrl}," +
            $"password={_pwd},connectRetry={ConnectRetry},connectTimeout={ConnectTimeout}," +
            $"keepAlive={KeepAlive},resolveDns={true},name={conName}");
    }
}