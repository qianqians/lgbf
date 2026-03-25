using System;
using System.Collections.Generic;

namespace hub;

public partial class TimerService
{
    private void AddLoopDayTimeHandleImpl()
    {
        lock (pendingLoopDayTimeHandles)
        {
            foreach (var (key, item) in pendingLoopDayTimeHandles)
            {
                if (!loopDayTimeHandles.TryGetValue(key, out var impls))
                {
                    impls = [];
                    loopDayTimeHandles.Add(key, impls);
                }
                impls.AddRange(item);
            }
            pendingLoopDayTimeHandles.Clear();
        }
    }

    private void PollLoopDayTimeHandleImpl(long tick)
    {
        try
        {
            AddLoopDayTimeHandleImpl();

            var t = DateTime.Now;
            if (t.Hour != 0 || t.Minute != 0 || (Tick - loopDayTick) < 24 * 60 * 60 * 1000)
            {
                return;
            }

            foreach (var (key, item) in requeueLoopDayTimeHandles)
            {
                if (!loopDayTimeHandles.TryGetValue(key, out var impls))
                {
                    impls = [];
                    loopDayTimeHandles.Add(key, impls);
                }
                impls.AddRange(item);
            }
            requeueLoopDayTimeHandles.Clear();

            loopDayTick = Tick;
            _dayTimeList.Clear();
            foreach (var (key, item) in loopDayTimeHandles)
            {
                if (key.Hour != t.Hour || key.Minute != t.Minute || key.Second > t.Second)
                {
                    continue;
                }
                _dayTimeList.Add(key);

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
                    catch (Exception e)
                    {
                        Log.Err("System.Exception{0}", e);
                    }
                }
            }

            foreach (var item in _dayTimeList)
            {
                if (!requeueLoopDayTimeHandles.ContainsKey(item))
                {
                    requeueLoopDayTimeHandles.Add(item, new List<HandleImpl>());
                }
                foreach (var impl in loopDayTimeHandles[item])
                {
                    if (impl.IsDel)
                    {
                        continue;
                    }

                    requeueLoopDayTimeHandles[item].Add(impl);
                }
                loopDayTimeHandles.Remove(item);
            }
        }
        finally
        {
            AddTickTime(888, PollLoopDayTimeHandleImpl);
        }
    }

    private void AddLoopWeekDayTimeHandleImpl()
    {
        lock (pendingLoopWeekDayTimeHandles)
        {
            foreach (var (key, item) in pendingLoopWeekDayTimeHandles)
            {
                if (!loopWeekDayTimeHandles.TryGetValue(key, out var impls))
                {
                    impls = [];
                    loopWeekDayTimeHandles.Add(key, impls);
                }
                impls.AddRange(item);
            }
            pendingLoopWeekDayTimeHandles.Clear();
        }
    }

    private void PollLoopWeekDayTimeHandleImpl(long tick)
    {
        try
        {
            AddLoopWeekDayTimeHandleImpl();

            var t = DateTime.Now;
            if (t.DayOfWeek != DayOfWeek.Sunday || t.Hour != 0 || t.Minute != 0 || t.Second != 0 ||
                (Tick - loopWeekDayTick) < 7 * 24 * 60 * 60 * 1000)
            {
                return;
            }

            foreach (var (key, item) in requeueLoopWeekDayTimeHandles)
            {
                if (!loopWeekDayTimeHandles.TryGetValue(key, out var impls))
                {
                    impls = [];
                    loopWeekDayTimeHandles.Add(key, impls);
                }
                impls.AddRange(item);
            }
            requeueLoopWeekDayTimeHandles.Clear();
            loopWeekDayTick = Tick;

            _weekDayTimeList.Clear();
            foreach (var (key, item) in loopWeekDayTimeHandles)
            {
                if (key.Day != t.DayOfWeek || key.Hour != t.Hour || key.Minute != t.Minute || key.Second > t.Second)
                {
                    continue;
                }
                _weekDayTimeList.Add(key);

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
                    catch (Exception e)
                    {
                        Log.Err("System.Exception{0}", e);
                    }
                }
            }

            foreach (var item in _weekDayTimeList)
            {
                if (!requeueLoopWeekDayTimeHandles.TryGetValue(item, out var impls))
                {
                    impls = [];
                    requeueLoopWeekDayTimeHandles.Add(item, impls);
                }
                foreach (var impl in loopWeekDayTimeHandles[item])
                {
                    if (impl.IsDel)
                    {
                        continue;
                    }
                    impls.Add(impl);
                }
                loopWeekDayTimeHandles.Remove(item);
            }
        }
        finally
        {
            AddTickTime(888, PollLoopWeekDayTimeHandleImpl);
        }
    }

    public object AddLoopDayTime(int hour, int minute, int second, Action<DateTime> handle)
    {
        var key = new DayTime
        {
            Hour = hour,
            Minute = minute,
            Second = second,
        };

        var impl = new HandleImpl(handle);
        lock (pendingLoopDayTimeHandles)
        {
            if (!pendingLoopDayTimeHandles.ContainsKey(key))
            {
                pendingLoopDayTimeHandles.Add(key, new List<HandleImpl>());
            }
            pendingLoopDayTimeHandles[key].Add(impl);
        }

        return impl;
    }

    public object AddLoopWeekDayTime(DayOfWeek day, int hour, int minute, int second, Action<DateTime> handle)
    {
        var key = new WeekDayTime
        {
            Day = day,
            Hour = hour,
            Minute = minute,
            Second = second,
        };

        var impl = new HandleImpl(handle);
        lock (pendingLoopWeekDayTimeHandles)
        {
            if (!pendingLoopWeekDayTimeHandles.ContainsKey(key))
            {
                pendingLoopWeekDayTimeHandles.Add(key, new List<HandleImpl>());
            }
            pendingLoopWeekDayTimeHandles[key].Add(impl);
        }

        return impl;
    }
}
