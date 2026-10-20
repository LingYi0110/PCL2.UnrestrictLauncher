using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace PCL2.UnrestrictLauncher
{
    internal enum PclLogBehavior
    {
        None,
        ToastIfDebug,
        Toast,
        Alert,
        AlertThenFeedback,
        AlertThenCrash
    }

    internal static class PclLog
    {
        private sealed class Entry
        {
            public string Level;
            public string Message;
            public string Source;
            public Exception Exception;
            public PclLogBehavior Behavior;
        }

        public const string LauncherSource = "PCL2.UnrestrictLauncher";
        private const int BufferLimit = 256;
        private static readonly object Sync = new object();
        private static readonly Queue<Entry> Pending = new Queue<Entry>();
        private static PropertyInfo _instance;
        private static MethodInfo _info;
        private static MethodInfo _warn;
        private static MethodInfo _error;
        private static Type _behaviorType;
        private static Timer _timer;

        public static void Info(string message, string source = LauncherSource, PclLogBehavior behavior = PclLogBehavior.None) =>
            Write("Info", message, null, source, behavior);

        public static void Warn(string message, string source = LauncherSource, PclLogBehavior behavior = PclLogBehavior.None) =>
            Write("Warn", message, null, source, behavior);

        public static void Error(string message, Exception exception = null, string source = LauncherSource, PclLogBehavior behavior = PclLogBehavior.None) =>
            Write("Error", message, exception, source, behavior);

        private static void Write(string level, string message, Exception exception, string source, PclLogBehavior behavior)
        {
            var entry = new Entry { Level = level, Message = message, Exception = exception, Source = source, Behavior = behavior };
            lock (Sync)
            {
                // Retain the most recent messages while PCL initializes its logger.
                if (Pending.Count == BufferLimit) Pending.Dequeue();
                Pending.Enqueue(entry);
                Flush();
            }
        }

        // Called under Sync so queued and newly arriving messages retain their order.
        private static void Flush()
        {
            if (IsReady())
            {
                while (Pending.Count > 0)
                {
                    var entry = Pending.Peek();
                    try
                    {
                        var method = entry.Level == "Info" ? _info : entry.Level == "Warn" ? _warn : _error;
                        var behavior = Enum.Parse(_behaviorType, entry.Behavior.ToString());
                        var arguments = entry.Level == "Error"
                            ? new object[] { entry.Exception, entry.Message, behavior, entry.Source }
                            : new object[] { entry.Message, behavior, entry.Source };
                        method.Invoke(null, arguments);
                        Pending.Dequeue();
                    }
                    catch
                    {
                        // Keep the entry queued if PCL is temporarily unable to log.
                        break;
                    }
                }

                if (Pending.Count == 0)
                {
                    StopRetry();
                    return;
                }
            }

            if (_timer == null) _timer = new Timer(Retry, null, 150, 150);
        }

        private static bool IsReady()
        {
            try
            {
                if (_info == null)
                {
                    var logger = AccessTools.TypeByName("MeloongCore.Logger");
                    var behavior = AccessTools.TypeByName("MeloongCore.LogBehavior");
                    if (logger == null || behavior == null) return false;

                    var textSignature = new[] { typeof(string), behavior, typeof(string) };
                    var info = AccessTools.Method(logger, "Info", textSignature);
                    var warn = AccessTools.Method(logger, "Warn", textSignature);
                    var error = AccessTools.Method(logger, "Error",
                        new[] { typeof(Exception), typeof(string), behavior, typeof(string) });
                    var instance = AccessTools.Property(logger, "Instance");
                    if (info == null || warn == null || error == null || instance == null) return false;

                    // Severity comes from Info/Warn/Error; behavior controls UI side effects.
                    _behaviorType = behavior;
                    _instance = instance;
                    _warn = warn;
                    _error = error;
                    _info = info;
                }

                var current = _instance.GetValue(null, null);
                if (current == null) return false;

                var writer = AccessTools.Field(current.GetType(), "writer");
                var available = AccessTools.Field(current.GetType(), "available");
                // The initial BaseLogger has no file writer. Wait until rotation and Init finish.
                return writer?.GetValue(current) != null
                    && available?.GetValue(current) is bool ready && ready;
            }
            catch
            {
                return false;
            }
        }

        private static void Retry(object state)
        {
            lock (Sync)
            {
                // Disposing a Timer does not cancel callbacks already queued on the thread pool.
                if (Pending.Count > 0) Flush();
            }
        }

        private static void StopRetry()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
