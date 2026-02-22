using System.Diagnostics;
using System;

namespace core;

public class Log
{
    public enum EnLogMode
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Err = 4,
    }

    public static void Trace(string log, params object[] agrvs)
    {
        if (logMode > EnLogMode.Trace)
        {
            return;
        }
        output(new StackFrame(1), TimerService.Tick, "trace", log, agrvs);
    }

    static public void debug(string log, params object[] agrvs)
    {
        if (logMode > EnLogMode.Debug)
        {
            return;
        }
        output(new System.Diagnostics.StackFrame(1), TimerService.Tick, "debug", log, agrvs);
    }

    static public void info(string log, params object[] agrvs)
    {
        if (logMode > EnLogMode.Info)
        {
            return;
        }
        output(new System.Diagnostics.StackFrame(1), TimerService.Tick, "info", log, agrvs);
    }

    static public void warn(string log, params object[] agrvs)
    {
        if (logMode > EnLogMode.Warn)
        {
            return;
        }
        output(new System.Diagnostics.StackFrame(1), TimerService.Tick, "warn", log, agrvs);
    }

    static public void err(string log, params object[] agrvs)
    {
        output(new System.Diagnostics.StackFrame(1), TimerService.Tick, "err", log, agrvs);
    }

    static private void output(StackFrame sf, long tmptime, string level, string log, params object[] agrvs)
    {
        var startTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var time = startTime.AddMilliseconds(tmptime);

        lock (logFile)
        {
            var realLogFile = $"{logPath}/{logFile}";
            {
                if (!System.IO.File.Exists(realLogFile))
                {
                    var tmp = System.IO.File.Create(realLogFile);
                    tmp.Close();
                    fs = new (realLogFile, true)
                    {
                        AutoFlush = true
                    };
                }
                if (fs == null)
                {
                    fs = new (realLogFile, true)
                    {
                        AutoFlush = true
                    };
                }
                System.IO.FileInfo finfo = new(realLogFile);
                if (finfo.Length > 1024 * 1024 * 32)
                {
                    fs.Close();
                    var tmpfile = $"{realLogFile}.{time:yyyy_MM_dd_h_m_s}";
                    finfo.MoveTo(tmpfile);
                    var tmp = System.IO.File.Create(realLogFile);
                    tmp.Close();
                    fs = new (realLogFile, true)
                    {
                        AutoFlush = true
                    };
                }
            }
            fs.WriteLine($"[{time}] [{level}] [{sf.GetMethod().DeclaringType.FullName}] [{sf.GetMethod().Name}]:{log}", agrvs);
        }
    }

    public static void close()
    {
        fs?.Close();
    }

    private static StreamWriter? fs = null;
    public static EnLogMode logMode = EnLogMode.Debug;
    public static string logPath = Environment.CurrentDirectory;
    public static string logFile = "log.txt";
}