using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Buffers;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;

namespace core;

public class HttpRsp(HttpResponse rsp)
{
    /*Microsoft.AspNetCore.Http.StatusCodes*/
    public ValueTask Response(int status, Dictionary<string, string> headers, byte[] buf) {
        try {
            foreach (var h in headers) {
                rsp.Headers[h.Key] = h.Value;
            }

            rsp.StatusCode = status;

            return rsp.Body.WriteAsync(buf);

        } catch (Exception ex) {
            Log.err("Response Exception:{0}", ex);
        } finally {
        }
        return ValueTask.CompletedTask;
    }
}

public class Startup {
    public void ConfigureServices(IServiceCollection services) {
    }

    private static int lcount = 0;
    private static long _recvStatTick = TimerService.Tick + 1000;
    private static long _lastStatTick = TimerService.Tick + 1000;
    public void Configure(IApplicationBuilder app) {
        app.Run(async (context) =>
        {
            var begin = TimerService.Tick;

            int count = Interlocked.Add(ref lcount, 1);
            if (TimerService.Tick >= _recvStatTick) {
                Interlocked.And(ref lcount, -count);
                Log.info("Connect statistics: {0} messages in {1} ms", count, TimerService.Tick - _lastStatTick);
                _lastStatTick = TimerService.Tick;
                _recvStatTick = TimerService.Tick + 1000;
            }
            
            string[] segments = context.Request.Path.Value!.TrimStart('/').Split('/');
            string version = segments.Length > 0 ? segments[0] : "unknown";
            string endpoint = segments.Length > 1 ? segments[1] : "unknown";

            Func<HttpRsp, Task>? cb = null;
            if (context.Request.Method == HttpMethods.Get)
            {
                HttpService.TryGetGetCallback(version, endpoint, out cb);
            }
            else if (context.Request.Method == HttpMethods.Post)
            {
                HttpService.TryGetPostCallback(version, endpoint, out cb);
            }
            else if (context.Request.Method == HttpMethods.Options)
            {
                foreach (var h in HttpService.BuildCrossHeaders())
                {
                    context.Response.Headers[h.Key] = h.Value;
                }
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.Body.Write(Encoding.UTF8.GetBytes(""));
                return;
            }
            if (cb == null) {
                return;
            }

            byte[] buf = null;
            int length = 0;
            try {
                if (context.Request.ContentLength != null) {
                    length = (int)context.Request.ContentLength;
                    buf = ArrayPool<byte>.Shared.Rent(length);
                    int offset = 0;
                    while (true) {
                        var len = await context.Request.Body.ReadAsync(buf, offset, length - offset);
                        if (len == 0) {
                            break;
                        }
                        offset += len;
                    }
                }
                await cb(new HttpRsp(context.Response));
            } catch (Exception ex) {
                Log.err("process http req ex:{0}", ex);
            } finally {
                if (buf != null) {
                    ArrayPool<byte>.Shared.Return(buf);
                }

                var tick = TimerService.Tick - begin;
                if (tick > 1000) {
                    Log.err("Timeout: elapsed_ticks={0}", tick);
                }
            }
        });
    }
}

public class HttpService
{
    private class VerHandle
    {
        public Dictionary<string, Func<HttpRsp, Task>> GetCB = new Dictionary<string, Func<HttpRsp, Task>>();
        public Dictionary<string, Func<HttpRsp, Task>> PostCB = new Dictionary<string, Func<HttpRsp, Task>>();
    }
    private static Dictionary<string, VerHandle> VerCB;

    private readonly int _port;
    private IHost? _h;

    public HttpService(string host, int port)
    {
        _port = port;
    }

    private static readonly Dictionary<string, string>  Headers = new Dictionary<string, string> { 
        { "Content-Type", "application/json; charset=utf-8" },
        { "Access-Control-Allow-Origin", "*" }, {"Access-Control-Allow-Headers", "XL-Token, Content-Type" },
        { "Access-Control-Allow-Methods", "POST, GET, OPTIONS"} };
    public static Dictionary<string, string> BuildCrossHeaders()
    {
        return Headers;
    }
    
    public static void Get(string ver, string uri, Func<HttpRsp, Task> callback)
    {
        if (string.IsNullOrEmpty(ver))
        {
            Log.err("process http get req uri:{0} with no ver info", uri);
            return;
        }
        
        if (!VerCB.TryGetValue(ver, out var cbDict))
        {
            cbDict = new VerHandle();
            VerCB.Add(ver, cbDict);
        }
        
        cbDict.GetCB.Add(ver, callback);
    }

    public static void Post(string ver, string uri, Func<HttpRsp, Task> callback)
    {
        if (string.IsNullOrEmpty(ver))
        {
            Log.err("process http get req uri:{0} with no ver info", uri);
            return;
        }

        if (!VerCB.TryGetValue(ver, out var cbDict))
        {
            cbDict = new VerHandle();
            VerCB.Add(ver, cbDict);
        }

        cbDict.PostCB.Add(ver, callback);
    }

    public static bool TryGetGetCallback(string ver, string uri, out Func<HttpRsp, Task>? cb)
    {
        cb = null;
        if (string.IsNullOrEmpty(ver))
        {
            Log.err("process http get req uri:{0} with no ver info", uri);
            return false;
        }
        
        if (!VerCB.TryGetValue(ver, out var cbDict))
        {
            Log.err("process http get req uri:{0} with no ver cb", uri);
            return false;
        }

        return cbDict.GetCB.TryGetValue(uri, out cb);
    }

    public static bool TryGetPostCallback(string ver, string uri, out Func<HttpRsp, Task>? cb)
    {
        cb = null;
        if (string.IsNullOrEmpty(ver))
        {
            Log.err("process http get req uri:{0} with no ver info", uri);
            return false;
        }
        
        if (!VerCB.TryGetValue(ver, out var cbDict))
        {
            Log.err("process http get req uri:{0} with no ver cb", uri);
            return false;
        }

        return cbDict.PostCB.TryGetValue(uri, out cb);
    }

    private void RunServerAsync()
    {
        var hostBuilder = new HostBuilder().ConfigureWebHostDefaults((webHostBuilder) => {
            webHostBuilder
                .UseKestrel()
                .ConfigureKestrel((context, options) => {

                    options.ListenAnyIP(_port, (listenOptions) => {
                        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    });

                })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>();
        });

        _h = hostBuilder.Build();
        _h.Run();
    }

    public void Run() {
        _ = Task.Factory.StartNew(RunServerAsync, TaskCreationOptions.LongRunning);
    }

    public async Task Close() {
        if (_h != null)
        {
            await _h.StopAsync();
        }
    }
}