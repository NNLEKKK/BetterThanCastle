using System;
using System.Threading;

namespace BetterThanCastle
{
    /// <summary>
    /// Minimal parallel-for for net35 (no TPL): a few persistent background threads plus the calling thread split [0, count)
    /// into fixed chunks. Only for pure data work (arrays in, own index range out), never Unity API. One job at a time:
    /// callers on other threads or nested calls run serially.
    /// </summary>
    internal static class BtcParallel
    {
        internal delegate void RangeBody(int from, int to, int worker);

        private const int Chunk = 2048;
        private static readonly object Gate = new object();
        private static Thread[] _threads;
        private static int _mainId = -1;
        private static RangeBody _body;
        private static int _count, _next, _busy, _gen;
        private static Exception _error;
        private static bool _running;

        /// <summary>Worker slots including the caller (index 0); bodies may keep per-worker scratch indexed by it.</summary>
        internal static int Workers { get { return (_threads == null ? 0 : _threads.Length) + 1; } }

        internal static int PlannedWorkers { get { return Math.Max(1, Math.Min(Environment.ProcessorCount - 1, 8)) + 1; } }

        /// <summary>Call once from the main thread (creates the threads lazily on first use too).</summary>
        internal static void Init()
        {
            if (_threads != null) return;
            _mainId = Thread.CurrentThread.ManagedThreadId;
            int n = PlannedWorkers - 1;
            _threads = new Thread[n];
            for (int i = 0; i < n; i++)
            {
                int slot = i + 1;
                _threads[i] = new Thread(() => Loop(slot)) { IsBackground = true, Name = "BtcParallel" + slot };
                _threads[i].Start();
            }
        }

        /// <summary>Runs body over [0, count). Returns false (nothing run) when not on the main thread or already running.</summary>
        internal static bool For(int count, RangeBody body)
        {
            if (_threads == null) Init();
            if (Thread.CurrentThread.ManagedThreadId != _mainId || _running) return false;
            _running = true;
            try
            {
                lock (Gate)
                {
                    _body = body; _count = count; _next = 0; _error = null;
                    _busy = _threads.Length;
                    _gen++;
                    Monitor.PulseAll(Gate);
                }
                Work(0);
                lock (Gate) { while (_busy > 0) Monitor.Wait(Gate); _body = null; }
                if (_error != null) throw new Exception("BtcParallel worker failed", _error);
                return true;
            }
            finally { _running = false; }
        }

        private static void Loop(int slot)
        {
            int seen = 0;
            while (true)
            {
                lock (Gate) { while (_gen == seen) Monitor.Wait(Gate); seen = _gen; }
                Work(slot);
                lock (Gate) { if (--_busy == 0) Monitor.PulseAll(Gate); }
            }
        }

        private static void Work(int slot)
        {
            RangeBody body = _body;
            int count = _count;
            while (true)
            {
                int from = Interlocked.Add(ref _next, Chunk) - Chunk;
                if (from >= count) return;
                int to = Math.Min(count, from + Chunk);
                try { body(from, to, slot); }
                catch (Exception e) { _error = e; Interlocked.Exchange(ref _next, count); return; }
            }
        }
    }
}
