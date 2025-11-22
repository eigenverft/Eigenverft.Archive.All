// File: CooperativeWorkerBase.cs
// Target: .NET 2.0 BCL; no Tasks, no async, no lambdas.
// Reviewer note: Public API avoids thread terms; names express intent (request stop, wait until stopped, terminate immediately).

using System;
using System.Diagnostics;
using System.Reflection;
// File: CooperativeWorkerBase.cs
// Target: .NET 2.0 BCL; no Tasks, no async, no lambdas.
// Reviewer note: Public surface uses intent-first names; callers see Start, RequestStop, WaitUntilStopped,
// StopGracefully, TerminateImmediately, RequestPause/RequestResume. Implementers can exit from deeply
// nested code with EndIterationNow() without threading jargon.

using System.Runtime.Serialization;
using System.Threading;

namespace Eigenverft.AirGap
{


    /// <summary>
    /// Inheritable worker base with a cooperative loop and caller-friendly lifecycle methods.
    /// </summary>
    /// <remarks>
    /// Implement <see cref="OnIteration"/> for your unit of work. Call <see cref="RequestStop"/> to finish gracefully,
    /// or <see cref="EndIterationNow"/> from deep code paths to request stop and unwind immediately.
    /// Callers use <see cref="Start"/>, <see cref="StopGracefully"/>, or <see cref="TerminateImmediately(int, bool)"/>.
    /// </remarks>
    public abstract class CooperativeWorkerBase : IDisposable
    {
        // --- sync primitives ---
        private readonly ManualResetEvent _pauseEvent = new ManualResetEvent(true);   // set = running; reset = paused

        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);  // set = stop requested
        private readonly ManualResetEvent _stoppedEvent = new ManualResetEvent(false);  // set = loop finished

        // --- state & config ---
        private readonly object _gate = new object();

        private Thread _thread;
        private volatile bool _isRunning;
        private volatile bool _isPaused;
        private bool _disposed;

        private string _name = "Worker";
        private ThreadPriority _priority = ThreadPriority.Normal;
        private bool _isBackground = true;

        // --- diagnostics (call-site) ---
        private string _startedByType = "unknown";

        private string _startedByMethod = "unknown";
        private string _startedByAssembly = "unknown";
        private DateTime _startedUtc = DateTime.MinValue;
        private int _startedThreadId = -1;

        // ---------------- Public properties ----------------

        /// <summary>
        /// True while the worker thread is alive and the loop is active.
        /// </summary>
        public bool IsRunning
        { get { return _isRunning; } }

        /// <summary>
        /// True while a cooperative pause is in effect.
        /// </summary>
        public bool IsPaused
        { get { return _isPaused; } }

        /// <summary>
        /// True when a cooperative stop has been requested.
        /// </summary>
        public bool IsStopRequested
        { get { return _stopEvent.WaitOne(0, false); } }

        /// <summary>
        /// Friendly name applied to the worker thread; set before <see cref="Start"/>.
        /// </summary>
        public string Name
        { get { lock (_gate) { return _name; } } set { lock (_gate) { EnsureNotRunning(); _name = value ?? "Worker"; } } }

        /// <summary>
        /// Priority applied to the worker thread.
        /// </summary>
        public ThreadPriority Priority
        { get { lock (_gate) { return _priority; } } set { lock (_gate) { _priority = value; if (_thread != null) _thread.Priority = value; } } }

        /// <summary>
        /// Whether the worker runs as a background thread; set before <see cref="Start"/>.
        /// </summary>
        public bool IsBackground
        { get { lock (_gate) { return _isBackground; } } set { lock (_gate) { EnsureNotRunning(); _isBackground = value; } } }

        /// <summary>
        /// Type that initiated <see cref="Start"/> or <see cref="RunInlineUntilStopRequested"/>.
        /// </summary>
        public string StartedByType
        { get { return _startedByType; } }

        /// <summary>
        /// Method that initiated startup.
        /// </summary>
        public string StartedByMethod
        { get { return _startedByMethod; } }

        /// <summary>
        /// Assembly of the invoker captured at startup.
        /// </summary>
        public string StartedByAssembly
        { get { return _startedByAssembly; } }

        /// <summary>
        /// UTC timestamp when startup was requested.
        /// </summary>
        public DateTime StartedUtc
        { get { return _startedUtc; } }

        /// <summary>
        /// Managed thread id used to run the loop (or current thread for inline mode).
        /// </summary>
        public int StartedThreadId
        { get { return _startedThreadId; } }

        // ---------------- Caller-centric methods ----------------

        /// <summary>
        /// Starts the worker loop on a dedicated thread. No-op if already running.
        /// </summary>
        /// <remarks>
        /// Applies <see cref="Name"/>, <see cref="Priority"/>, <see cref="IsBackground"/> and records caller identity for diagnostics.
        /// </remarks>
        /// <example>
        /// <code>
        /// var w = new MyWorker();
        /// w.Start();
        /// </code>
        /// </example>
        public void Start()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_isRunning) return;

                _stopEvent.Reset();
                _pauseEvent.Set();
                _stoppedEvent.Reset();
                _isPaused = false;

                CaptureStartCaller();

                _thread = new Thread(ThreadMain);
                _thread.IsBackground = _isBackground;
                _thread.Name = _name;
                _thread.Priority = _priority;

                _isRunning = true;
                _thread.Start();
            }
        }

        /// <summary>
        /// Requests a graceful shutdown and returns immediately.
        /// </summary>
        /// <remarks>
        /// Safe from any thread, including inside <see cref="OnIteration"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// worker.RequestStop();
        /// </code>
        /// </example>
        public void RequestStop()
        {
            _stopEvent.Set();
            _pauseEvent.Set(); // release a paused loop so it can finish
        }

        /// <summary>
        /// Waits until the worker has fully stopped.
        /// </summary>
        /// <remarks>
        /// Use after <see cref="RequestStop"/> or <see cref="TerminateImmediately(int, bool)"/>.
        /// Works for dedicated thread and inline modes.
        /// </remarks>
        /// <example>
        /// <code>
        /// worker.RequestStop();
        /// worker.WaitUntilStopped();
        /// </code>
        /// </example>
        public void WaitUntilStopped()
        {
            Thread t = _thread;
            if (t != null) t.Join();
            else _stoppedEvent.WaitOne();
        }

        /// <summary>
        /// Waits until the worker has fully stopped or the timeout elapses.
        /// </summary>
        /// <param name="timeoutMilliseconds">Milliseconds to wait; zero or negative means infinite.</param>
        /// <returns>True if the worker stopped; otherwise false on timeout.</returns>
        /// <example>
        /// <code>
        /// worker.RequestStop();
        /// if (!worker.WaitUntilStopped(2000)) { /* reviewer: handle timeout */ }
        /// </code>
        /// </example>
        public bool WaitUntilStopped(int timeoutMilliseconds)
        {
            Thread t = _thread;
            if (t != null)
            {
                if (timeoutMilliseconds <= 0) { t.Join(); return true; }
                return t.Join(timeoutMilliseconds);
            }
            if (timeoutMilliseconds <= 0) { _stoppedEvent.WaitOne(); return true; }
            return _stoppedEvent.WaitOne(timeoutMilliseconds, false);
        }

        /// <summary>
        /// Convenience: requests graceful shutdown and waits until the worker has stopped.
        /// </summary>
        /// <example>
        /// <code>
        /// worker.StopGracefully();
        /// </code>
        /// </example>
        public void StopGracefully()
        {
            RequestStop();
            WaitUntilStopped();
        }

        /// <summary>
        /// Convenience overload with timeout for graceful shutdown.
        /// </summary>
        /// <param name="timeoutMilliseconds">Milliseconds to wait; zero or negative means infinite.</param>
        /// <returns>True if the worker stopped; false on timeout.</returns>
        public bool StopGracefully(int timeoutMilliseconds)
        {
            RequestStop();
            return WaitUntilStopped(timeoutMilliseconds);
        }

        /// <summary>
        /// Requests an urgent termination sequence for the worker.
        /// </summary>
        /// <param name="timeoutMilliseconds">Time to wait after signaling stop and interrupting; zero or negative means no wait.</param>
        /// <param name="allowThreadAbort">When true and still not stopped after the wait, uses <see cref="Thread.Abort"/> as a last resort.</param>
        /// <returns>True if the worker is confirmed stopped when this returns; otherwise false.</returns>
        /// <remarks>
        /// This path is for emergencies. It first calls <see cref="RequestStop"/>, then <see cref="Thread.Interrupt"/> to break blocking waits.
        /// If still alive after the timeout and <paramref name="allowThreadAbort"/> is true, it calls <see cref="Thread.Abort"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// bool stopped = worker.TerminateImmediately(1000, true);
        /// </code>
        /// </example>
        public bool TerminateImmediately(int timeoutMilliseconds, bool allowThreadAbort)
        {
            Thread t = _thread;
            if (t == null)
            {
                return WaitUntilStopped(timeoutMilliseconds);
            }

            RequestStop();

            try { t.Interrupt(); } catch { /* reviewer: ignore if thread not in a wait */ }

            if (timeoutMilliseconds > 0)
            {
                if (t.Join(timeoutMilliseconds)) return true;
            }
            else if (!t.IsAlive)
            {
                return true;
            }

            if (allowThreadAbort && t.IsAlive)
            {
                try { t.Abort(); } catch { }
                try { t.Join(250); } catch { }
            }

            return !t.IsAlive;
        }

        /// <summary>
        /// Runs the worker loop synchronously on the current thread until <see cref="RequestStop"/> is called.
        /// </summary>
        /// <remarks>
        /// Deterministic hosting mode for simple service shells or tests.
        /// </remarks>
        /// <example>
        /// <code>
        /// worker.RunInlineUntilStopRequested();
        /// </code>
        /// </example>
        public void RunInlineUntilStopRequested()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_isRunning) throw new InvalidOperationException("Already running on a dedicated thread.");

                _stopEvent.Reset();
                _pauseEvent.Set();
                _stoppedEvent.Reset();
                _isPaused = false;

                CaptureStartCaller();
                _isRunning = true;
            }

            try { ThreadMainCore(); }
            finally { _isRunning = false; _stoppedEvent.Set(); }
        }

        /// <summary>
        /// Pauses the loop cooperatively (idempotent).
        /// </summary>
        /// <remarks>
        /// The loop blocks until <see cref="RequestResume"/> is called or a stop is requested.
        /// </remarks>
        public void RequestPause()
        { _pauseEvent.Reset(); _isPaused = true; }

        /// <summary>
        /// Resumes the loop if paused (idempotent).
        /// </summary>
        public void RequestResume()
        { _pauseEvent.Set(); _isPaused = false; }

        /// <summary>
        /// Disposes the worker by requesting a cooperative stop and waiting until it finishes.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                RequestStop();
                WaitUntilStopped();
            }
            finally
            {
                _pauseEvent.Close();
                _stopEvent.Close();
                _stoppedEvent.Close();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        // ---------------- Implementer-facing helpers ----------------

        /// <summary>
        /// True when a cooperative stop has been requested; use in loop conditions.
        /// </summary>
        /// <remarks>
        /// Intended to keep code inside <see cref="OnIteration"/> readable in simple loops.
        /// </remarks>
        /// <example>
        /// <code>
        /// for (int i = 1; i &lt;= 100 &amp;&amp; !Stopping; i++) { /* ... */ }
        /// </code>
        /// </example>
        protected bool Stopping
        {
            get { return IsStopRequested; }
        }

        /// <summary>
        /// Immediately ends the current iteration path by requesting stop and unwinding the call stack.
        /// </summary>
        /// <remarks>
        /// Throws a private sentinel exception caught by the base loop. Use for deeply nested logic where multiple returns
        /// would harm clarity. Prefer simple <see cref="RequestStop"/> and <c>return</c> when not deeply nested.
        /// </remarks>
        /// <example>
        /// <code>
        /// if (i == 50) EndIterationNow(); // request stop and unwind immediately
        /// </code>
        /// </example>
        protected void EndIterationNow()
        {
            RequestStop();
            throw new _EndIterationException();
        }

        // ---------------- Overridables for subclasses ----------------

        /// <summary>
        /// Called once on the worker thread before the loop starts.
        /// </summary>
        protected virtual void OnStart()
        { /* implementation omitted */ }

        /// <summary>
        /// Called on each iteration of the worker loop. Keep work short and cooperative.
        /// </summary>
        protected abstract void OnIteration();

        /// <summary>
        /// Called once when the loop is finishing.
        /// </summary>
        protected virtual void OnStop()
        { /* implementation omitted */ }

        /// <summary>
        /// Called when an exception escapes <see cref="OnIteration"/>. Return true to continue the loop; false to end it.
        /// </summary>
        /// <param name="error">The exception encountered.</param>
        /// <returns>True to continue; false to end the loop.</returns>
        protected virtual bool OnError(Exception error)
        { /* implementation omitted */ return false; }

        /// <summary>
        /// Milliseconds to sleep between iterations. Default 0.
        /// </summary>
        protected virtual int GetIterationDelayMs()
        { /* implementation omitted */ return 0; }

        // ---------------- Internals ----------------

        private void ThreadMain()
        {
            try
            {
                try { _startedThreadId = Thread.CurrentThread.ManagedThreadId; } catch { _startedThreadId = -1; }
                ThreadMainCore();
            }
            finally
            {
                _isRunning = false;
                _stoppedEvent.Set();
            }
        }

        private void ThreadMainCore()
        {
            try
            {
                OnStart();

                while (!IsStopRequested)
                {
                    // Pause gate (returns immediately if not paused)
                    _pauseEvent.WaitOne(Timeout.Infinite, false);
                    if (IsStopRequested) break;

                    try
                    {
                        OnIteration();
                    }
                    catch (_EndIterationException)
                    {
                        // Reviewer: implementer requested an immediate, graceful end from deep nested code.
                        break;
                    }
                    catch (Exception ex)
                    {
                        bool handled = false;
                        try { handled = OnError(ex); } catch { handled = false; }
                        if (!handled) break;
                    }

                    int delay = 0;
                    try { delay = GetIterationDelayMs(); } catch { /* reviewer: defensive */ }
                    if (delay > 0) Thread.Sleep(delay);
                }
            }
            finally
            {
                try { OnStop(); }
                catch (Exception ex) { Trace.WriteLine("CooperativeWorkerBase.OnStop error: " + ex); }
            }
        }

        private void CaptureStartCaller()
        {
            _startedUtc = DateTime.UtcNow;
            try
            {
                StackTrace st = new StackTrace(1, false);
                MethodBase m = (st.FrameCount > 0) ? st.GetFrame(0).GetMethod() : null;
                Type dt = (m != null) ? m.DeclaringType : null;

                _startedByMethod = (m != null) ? m.Name : "unknown";
                _startedByType = (dt != null) ? dt.FullName : "unknown";
                _startedByAssembly = (dt != null) ? dt.Assembly.FullName : "unknown";
            }
            catch { _startedByMethod = _startedByType = _startedByAssembly = "unknown"; }
        }

        private void EnsureNotRunning()
        {
            if (_isRunning) throw new InvalidOperationException("Cannot modify this property while running.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
        }

        // Private sentinel exception used by EndIterationNow()
        [Serializable]
        private sealed class _EndIterationException : Exception
        {
            public _EndIterationException()
            { }

            private _EndIterationException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }
    }

    /// <summary>
    /// Generic thread-safe box using a private lock. Works for reference and value types.
    /// </summary>
    /// <remarks>
    /// Minimal API for .NET 2.0: Get/Set and a simple update function type.
    /// </remarks>
    public sealed class ThreadSafe<T>
    {
        public delegate T Updater(T current);

        private readonly object _sync = new object();
        private T _value;

        /// <summary>Creates a box with an initial value.</summary>
        public ThreadSafe(T initial)
        { _value = initial; }

        /// <summary>Gets or sets the value under a lock.</summary>
        public T Value
        {
            get { lock (_sync) { return _value; } }
            set { lock (_sync) { _value = value; } }
        }

        /// <summary>Atomically updates the value by applying <paramref name="updater"/> under the lock.</summary>
        public T Update(Updater updater)
        {
            if (updater == null) throw new ArgumentNullException("updater");
            lock (_sync)
            {
                _value = updater(_value);
                return _value;
            }
        }
    }
}