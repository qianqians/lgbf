using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

namespace hub;

public class TimerService
{
    private static readonly Lock Mutex = new Lock();
    private static TimerService? _ins = null;
    public static TimerService? Ins
    {
        get
        {
            if (_ins != null)
            {
                return _ins;
            }
            lock (Mutex)
            {
                Thread.MemoryBarrier();
                if (_ins != null)
                {
                    return _ins;
                }
                _ins = new TimerService();
            }
            return _ins;
        }
    }
    
	private TimerService()
	{
        tickHandleDict = new SortedDictionary<long, HandleImpl>();
        addTickHandle = new Dictionary<long, HandleImpl>();

        dayTimeHandleDict = new Dictionary<DayTime, List<HandleImpl>>();
        addDayTimeHandle = new Dictionary<DayTime, List<HandleImpl>>();

        timeHandleDict = new Dictionary<WeekDayTime, List<HandleImpl> >();
        addTimeHandle = new Dictionary<WeekDayTime, List<HandleImpl> >();

        monthTimeHandleDict = new Dictionary<MonthDayTime, List<HandleImpl> >();
        addmonthtimeHandle = new Dictionary<MonthDayTime, List<HandleImpl> >();

        loopDayTimeHandleDict = new Dictionary<DayTime, List<HandleImpl> >();
        addLoopDayTimeHandle = new Dictionary<DayTime, List<HandleImpl> >();
        loopDayTimeHandle = new Dictionary<DayTime, List<HandleImpl> >();

        loopWeekDayTimeHandleDict = new Dictionary<WeekDayTime, List<HandleImpl> >();
        addLoopWeekDayTimeHandle = new Dictionary<WeekDayTime, List<HandleImpl> >();
        loopweekdaytimeHandle = new Dictionary<WeekDayTime, List<HandleImpl> >();

        Tick = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;

        loopDayTick = 0;
        loopWeekDayTick = 0;

        AddTickTime(888, PollDayTimeHandleImpl);
        AddTickTime(888, PollTimeHandleImpl);
        AddTickTime(888, PollMonthTimeHandleImpl);
        AddTickTime(888, PollLoopDayTimeHandleImpl);
        AddTickTime(888, PollLoopWeekDayTimeHandleImpl);
    }

    private static long Refresh()
    {
        Tick = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
        return Tick;
    }

    public static long WeekEndTimestamp()
    {
        var now = DateTime.Now;
        var dayOfWeek = Convert.ToInt32(now.DayOfWeek.ToString("d"));
        dayOfWeek = dayOfWeek <= 0 ? 7 : dayOfWeek;
        var endOfWeek = now.AddDays(7 - dayOfWeek).Date;
        endOfWeek = endOfWeek.AddHours(23 - endOfWeek.Hour).AddMinutes(59 - endOfWeek.Minute).AddSeconds(59 - endOfWeek.Second);

        return (long)(endOfWeek - new DateTime(1970, 1, 1)).TotalMilliseconds;
    }

    private void AddTickHandleImpl()
    {
        lock (addTickHandle)
        {
            if (addTickHandle.Count <= 0)
            {
                return;
            }

            foreach (var item in addTickHandle)
            {
                var process = item.Key;
                while (tickHandleDict.ContainsKey(process))
                {
                    process++;
                }

                tickHandleDict.Add(process, item.Value);
            }

            addTickHandle.Clear();
        }
    }

    private readonly List<long> _list = [];
    private void PollTickHandleImpl()
    {
        AddTickHandleImpl();

        foreach (var (key, impl) in tickHandleDict)
        {
            if (key <= Tick)
            {
                _list.Add(key);
                if (impl.IsDel)
                {
                    continue;
                }

                var handle = impl.Handle as Action<long>;
                if (handle == null)
                {
                    continue;
                }
                try
                {
                    handle(Tick);
                }
                catch (System.Exception e)
                {
                    Log.Err("System.Exceptio{0}", e);
                }
            }
            else
            {
                break;
            }
        }

        if (_list.Count <= 0)
        {
            return;
        }
        foreach (var item in _list)
        {
            tickHandleDict.Remove(item);
        }
        _list.Clear();
    }

    private void AddDayTimeHandleImpl()
    {
        lock (addDayTimeHandle)
        {
            foreach (var item in addDayTimeHandle)
            {
                if (!dayTimeHandleDict.TryGetValue(item.Key, out var impls))
                {
                    impls = [];
                    dayTimeHandleDict.Add(item.Key, impls);
                }
                impls.AddRange(item.Value);
            }
            addDayTimeHandle.Clear();
        }
    }

    private void PollDayTimeHandleImpl(long tick)
    {
        AddDayTimeHandleImpl();

        var t = DateTime.Now;
        List<DayTime> list = [];
        foreach (var (key, item) in dayTimeHandleDict)
        {
            if (key.hour != t.Hour || key.minute != t.Minute || key.second > t.Second)
            {
                continue;
            }
            list.Add(key);

            foreach (var impl in item)
            {
                if (impl.IsDel)
                {
                    continue;
                }

                var handle = impl.Handle as Action<DateTime>;
                if (handle == null)
                {
                    continue;
                }
                try
                {
                    handle(t);
                }
                catch (System.Exception e)
                {
                    Log.Err("System.Exception{0}", e);
                }
            }
        }
        foreach (var item in list)
        {
            dayTimeHandleDict.Remove(item);
        }

        AddTickTime(888, PollDayTimeHandleImpl);
    }

    private void AddTimeHandleImpl()
    {
        lock (addTimeHandle)
        {
            foreach (var (key, item) in addTimeHandle)
            {
                if (!timeHandleDict.TryGetValue(key, out var impls))
                {
                    impls = [];
                    timeHandleDict.Add(key, impls);
                }
                impls.AddRange(item);
            }
            addTimeHandle.Clear();
        }
    }

    private void PollTimeHandleImpl(long tick)
    {
        AddTimeHandleImpl();

        List<WeekDayTime> list = [];
        var t = DateTime.Now;
        foreach (var (key, item) in timeHandleDict)
        {
            if (key.day != t.DayOfWeek || key.hour == t.Hour || key.minute == t.Minute || key.second > t.Second)
            {
                continue;
            }
            list.Add(key);
            
            foreach (var impl in item)
            {
                if (impl.IsDel)
                {
                    continue;
                }

                var handle = impl.Handle as Action<DateTime>;
                if (handle == null)
                {
                    continue;
                }
                try
                {
                    handle(t);
                }
                catch (System.Exception e)
                {
                    Log.Err("System.Exception{0}", e);
                }
            }
        }
        foreach (var item in list)
        {
            timeHandleDict.Remove(item);
        }

        AddTickTime(888, PollTimeHandleImpl);
    }

    private void AddMonthTimeHandleImpl()
    {
        lock (addmonthtimeHandle)
        {
            foreach (var (key, item) in addmonthtimeHandle)
            {
                if (!monthTimeHandleDict.TryGetValue(key, out var impls))
                {
                    impls = [];
                    monthTimeHandleDict.Add(key, impls);
                }
                impls.AddRange(item);
            }
            addmonthtimeHandle.Clear();
        }
    }

    private void PollMonthTimeHandleImpl(long tick)
    {
        AddMonthTimeHandleImpl();

        List<MonthDayTime> list = [];
        var t = DateTime.Now;
        foreach (var (key, item) in monthTimeHandleDict)
        {
            if (key.Month != t.Month || key.Day != t.Day || key.Hour != t.Hour || key.Minute != t.Minute ||
                key.Second >= t.Second)
            {
                continue;
            }
            list.Add(key);
            
            foreach (var impl in item)
            {
                if (impl.IsDel)
                {
                    continue;
                }

                var handle = impl.Handle as Action<DateTime>;
                if (handle == null)
                {
                    continue;
                }
                try
                {
                    handle(t);
                }
                catch (System.Exception e)
                {
                    Log.Err("System.Exception{0}", e);
                }
            }
        }
        foreach (var item in list)
        {
            monthTimeHandleDict.Remove(item);
        }

        AddTickTime(888, PollMonthTimeHandleImpl);
    }

    private void AddLoopDayTimeHandleImpl()
    {
        lock (addLoopDayTimeHandle)
        {
            foreach (var (key, item) in addLoopDayTimeHandle)
            {
                if (!loopDayTimeHandleDict.TryGetValue(key, out var impls))
                {
                    impls = [];
                    loopDayTimeHandleDict.Add(key, impls);
                }
                impls.AddRange(item);
            }
            addLoopDayTimeHandle.Clear();
        }
    }

    private void PollLoopDayTimeHandleImpl(long tick)
    {
        AddLoopDayTimeHandleImpl();

        var t = DateTime.Now;
        if (t.Hour != 0 || t.Minute != 0 && (Tick - loopDayTick) < 24 * 60 * 60 * 1000)
        {
            return;
        }

        foreach (var (key, item) in loopDayTimeHandle)
        {
            if (!loopDayTimeHandleDict.TryGetValue(key, out var impls))
            {
                impls = [];
                loopDayTimeHandleDict.Add(key, impls);
            }
            impls.AddRange(item);
        }
        loopDayTimeHandle.Clear();

        loopDayTick = Tick;
        List<DayTime> list = [];
        foreach (var (key, item) in loopDayTimeHandleDict)
        {
            if (key.hour == t.Hour || key.minute == t.Minute || key.second > t.Second)
            {
                continue;
            }
            list.Add(key);
            
            foreach (var impl in item)
            {
                if (impl.IsDel)
                {
                    continue;
                }

                var handle = impl.Handle as Action<DateTime>;
                if (handle == null)
                {
                    continue;
                }
                try
                {
                    handle(t);
                }
                catch (System.Exception e)
                {
                    Log.Err("System.Exception{0}", e);
                }
            }
        }

        foreach (var item in list)
        {
            if (!loopDayTimeHandle.ContainsKey(item))
            {
                loopDayTimeHandle.Add(item, new List<HandleImpl>());
            }
            foreach (var impl in loopDayTimeHandleDict[item])
            {
                if (impl.IsDel)
                {
                    continue;
                }

                loopDayTimeHandle[item].Add(impl);
            }
            loopDayTimeHandleDict.Remove(item);
        }

        AddTickTime(888, PollLoopDayTimeHandleImpl);
    }

    private void AddLoopWeekDayTimeHandleImpl()
    {
        lock (addLoopWeekDayTimeHandle)
        {
            foreach (var (key, item) in addLoopWeekDayTimeHandle)
            {
                if (!loopWeekDayTimeHandleDict.TryGetValue(key, out var impls))
                {
                    impls = [];
                    loopWeekDayTimeHandleDict.Add(key, impls);
                }
                impls.AddRange(item);
            }
            addLoopWeekDayTimeHandle.Clear();
        }
    }

    private void PollLoopWeekDayTimeHandleImpl(long tick)
    {
        AddLoopWeekDayTimeHandleImpl();

        var t = DateTime.Now;
        if (t.DayOfWeek != DayOfWeek.Sunday || t.Hour != 0 || t.Minute != 0 || t.Second != 0 ||
            (Tick - loopWeekDayTick) < 7 * 24 * 60 * 60 * 1000)
        {
            return;
        }
        foreach (var (key, item) in loopweekdaytimeHandle)
        {
            if (!loopWeekDayTimeHandleDict.TryGetValue(key, out  var impls))
            {
                impls = [];
                loopWeekDayTimeHandleDict.Add(key, impls);
            }
            impls.AddRange(item);
        }
        loopweekdaytimeHandle.Clear();
        loopWeekDayTick = Tick;

        List<WeekDayTime> list = [];
        foreach (var (key, item) in loopWeekDayTimeHandleDict)
        {
            if (key.day != t.DayOfWeek || key.hour != t.Hour || key.minute != t.Minute || key.second < t.Second)
            {
                continue;
            }
            list.Add(key);

            foreach (var impl in item)
            {
                if (impl.IsDel)
                {
                    continue;
                }

                var handle = impl.Handle as Action<DateTime>;
                if (handle == null)
                {
                    continue;
                }
                try
                {
                    handle(t);
                }
                catch (System.Exception e)
                {
                    Log.Err("System.Exception{0}", e);
                }
            }
        }

        foreach (var item in list)
        {
            if (!loopweekdaytimeHandle.TryGetValue(item, out var impls))
            {
                impls = [];
                loopweekdaytimeHandle.Add(item, impls);
            }
            foreach (var impl in loopWeekDayTimeHandleDict[item])
            {
                if (impl.IsDel)
                {
                    continue;
                }
                impls.Add(impl);
            }
            loopWeekDayTimeHandleDict.Remove(item);
        }

        AddTickTime(888, PollLoopWeekDayTimeHandleImpl);
    }

    public void Poll()
	{
        Refresh();
        PollTickHandleImpl();
    }

	public object AddTickTime(long process, Action<long> handle)
	{
        process += Tick;
        var impl = new HandleImpl(handle);

        lock (addTickHandle)
        {
            while (addTickHandle.ContainsKey(process)){ process++; }
            addTickHandle.Add(process, impl);
        }

        return impl;
	}

    public object AddDayTime(int hour, int minute, int second, Action<DateTime> handle)
    {
        var key = new DayTime()
        {
            hour = hour,
            minute = minute,
            second = second,
        };

        var impl = new HandleImpl(handle);
        lock (addDayTimeHandle)
        {
            if (!addDayTimeHandle.TryGetValue(key, out var impls))
            {
                impls = [];
                addDayTimeHandle.Add(key, impls);
            }
            impls.Add(impl);
        }

        return impl;
    }

    public object AddWeekDayTime(System.DayOfWeek day, int hour, int minute, int second, Action<DateTime> handle)
    {
        var key = new WeekDayTime()
        {
            day = day,
            hour = hour,
            minute = minute,
            second = second,
        };

        var impl = new HandleImpl(handle);
        lock (addTimeHandle)
        {
            if (!addTimeHandle.TryGetValue(key, out var impls))
            {
                impls = [];
                addTimeHandle.Add(key, impls);
            }
            impls.Add(impl);
        }

        return impl;
    }

    public object AddMonthDayTime(int month, int day, int hour, int minute, int second, Action<DateTime> handle)
    {
        var key = new MonthDayTime()
        {
            Month = month,
            Day = day,
            Hour = hour,
            Minute = minute,
            Second = second,
        };

        var impl = new HandleImpl(handle);
        lock (addmonthtimeHandle)
        {
            if (!addmonthtimeHandle.ContainsKey(key))
            {
                addmonthtimeHandle.Add(key, new List<HandleImpl>());
            }
            addmonthtimeHandle[key].Add(impl);
        }

        return impl;
    }

    public object AddLoopDayTime(int hour, int minute, int second, Action<DateTime> handle)
    {
        var key = new DayTime()
        {
            hour = hour,
            minute = minute,
            second = second,    
        };

        var impl = new HandleImpl(handle);
        lock (addLoopDayTimeHandle)
        {
            if (!addLoopDayTimeHandle.ContainsKey(key))
            {
                addLoopDayTimeHandle.Add(key, new List<HandleImpl>());
            }
            addLoopDayTimeHandle[key].Add(impl);
        }

        return impl;
    }

    public object AddLoopWeekDayTime(System.DayOfWeek day, int hour, int minute, int second, Action<DateTime> handle)
    {
        var key = new WeekDayTime()
        {
            day = day,
            hour = hour,
            minute = minute,
            second = second,
        };

        var impl = new HandleImpl(handle);
        lock (addLoopWeekDayTimeHandle)
        {
            if (!addLoopWeekDayTimeHandle.ContainsKey(key))
            {
                addLoopWeekDayTimeHandle.Add(key, new List<HandleImpl>());
            }
            addLoopWeekDayTimeHandle[key].Add(impl);
        }

        return impl;
    }

    public void DelTimer(object impl)
    {
        var handle = impl as HandleImpl;
        if (handle != null)
        {
            handle.IsDel = true;
        }
    }

    public static long Tick;

    private class HandleImpl
    {
        public HandleImpl(Action<long> handle)
        {
            IsDel = false;
            this.Handle = handle;
        }

        public HandleImpl(Action<DateTime> handle)
        {
            IsDel = false; 
            this.Handle = handle;
        }

        public bool IsDel;
        public readonly object Handle;
    }

    private struct MonthDayTime : IEquatable<MonthDayTime>
    {
        public int Month;
        public int Day;
        public int Hour;
        public int Minute;
        public int Second;

        public override int GetHashCode()
        {
            return (int)Day * 24 * 3600 + Hour * 3600 + Minute * 60 + Second;
        }
        
        public bool Equals(MonthDayTime tmp)
        {
            return Month == tmp.Month && Day == tmp.Day && Hour == tmp.Hour && Minute == tmp.Minute &&
                   Second == tmp.Second;
        }
        
        public override bool Equals(object? obj)
        {
            return obj is MonthDayTime other && Equals(other);
        }
    }

    private struct WeekDayTime : IEquatable<WeekDayTime>
    {
        public DayOfWeek day;
        public int hour;
        public int minute;
        public int second;

        public override int GetHashCode()
        {
            return (int)day * 24 * 3600 + hour * 3600 + minute * 60 + second;
        }

        public bool Equals(WeekDayTime tmp)
        {
            return day == tmp.day && hour == tmp.hour && minute == tmp.minute && second == tmp.second;
        }

        public override bool Equals(object? obj)
        {
            return obj is WeekDayTime other && Equals(other);
        }
    }

    private struct DayTime : IEquatable<DayTime>
    {
        public int hour;
        public int minute;
        public int second;

        public override int GetHashCode()
        {
            return (int)hour * 3600 + minute * 60 + second;
        }

        public bool Equals(DayTime tmp)
        {
            return hour == tmp.hour && minute == tmp.minute && second == tmp.second;
        }
        
        public override bool Equals(object? obj)
        {
            return obj is DayTime other && Equals(other);
        }
    }

    private readonly SortedDictionary<long, HandleImpl> tickHandleDict;
    private readonly Dictionary<long, HandleImpl> addTickHandle;

    private readonly Dictionary<MonthDayTime, List<HandleImpl>> monthTimeHandleDict;
    private readonly Dictionary<MonthDayTime, List<HandleImpl>> addmonthtimeHandle;

    private readonly Dictionary<WeekDayTime, List<HandleImpl>> timeHandleDict;
    private readonly Dictionary<WeekDayTime, List<HandleImpl>> addTimeHandle;

    private readonly Dictionary<DayTime, List<HandleImpl> > loopDayTimeHandleDict;
    private readonly Dictionary<DayTime, List<HandleImpl> > addLoopDayTimeHandle;
    private readonly Dictionary<DayTime, List<HandleImpl> > loopDayTimeHandle;
    private long loopDayTick;

    private readonly Dictionary<DayTime, List<HandleImpl>> dayTimeHandleDict;
    private readonly Dictionary<DayTime, List<HandleImpl>> addDayTimeHandle;

    private readonly Dictionary<WeekDayTime, List<HandleImpl> > loopWeekDayTimeHandleDict;
    private readonly Dictionary<WeekDayTime, List<HandleImpl> > addLoopWeekDayTimeHandle;
    private readonly Dictionary<WeekDayTime, List<HandleImpl> > loopweekdaytimeHandle;
    private long loopWeekDayTick;
}