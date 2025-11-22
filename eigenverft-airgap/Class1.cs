// AsyncFlow.cs
// Targets .NET 2.0 with Theraot.Core (Tasks/CT). No other deps.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eigenverft.AirGap
{
    /// <summary>
    /// Fluent async flow for net20 + Theraot: cancelable workers (via .WithCancellation),
    /// explicit UI steps, and sequential unwrapping helpers (no Task-of-Task hazards).
    /// </summary>
    public sealed class AsyncFlow
    {
        private readonly SynchronizationContext _ui;
        private CancellationToken _ct;
        private Func<SynchronizationContext, CancellationToken, Task> _compose;

        private AsyncFlow(SynchronizationContext ui)
        {
            _ui = ui ?? new SynchronizationContext();
            _ct = CancellationToken.None;
            _compose = delegate { return Completed(); };
        }

        #region Factory

        /// <summary>Creates a new flow bound to a UI <see cref="SynchronizationContext"/>.</summary>
        public static AsyncFlow Create(SynchronizationContext ui) { return new AsyncFlow(ui); }

        #endregion

        #region Ambient UI (optional for workers)

        public static class AmbientUi
        {
            private static SynchronizationContext _ambient;
            public static void Initialize(SynchronizationContext ui) { _ambient = ui; }
            public static void Post(Action action)
            {
                if (action == null) return;
                var ctx = _ambient;
                if (ctx == null) { action(); return; }
                ctx.Post(delegate { action(); }, null);
            }
            public static void Post<T>(Action<T> action, T arg)
            {
                if (action == null) return;
                var ctx = _ambient;
                if (ctx == null) { action(arg); return; }
                ctx.Post(delegate (object boxed) { action((T)boxed); }, arg);
            }
        }

        #endregion

        #region Cancellation

        /// <summary>Sets the implicit <see cref="CancellationToken"/> for subsequent worker steps.</summary>
        public AsyncFlow WithCancellation(CancellationToken ct) { _ct = ct; return this; }

        #endregion

        #region Worker steps (cancelable via .WithCancellation)

        /// <summary>Append a worker step (no args) using the flow token.</summary>
        public AsyncFlow ThenAsync(Func<CancellationToken, Task> step)
        {
            if (step == null) throw new ArgumentNullException("step");
            var prev = _compose;
            _compose = delegate (SynchronizationContext ui, CancellationToken ct)
            {
                var tcs = new TaskCompletionSource<object>();
                prev(ui, ct).ContinueWith(delegate
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try { (step(_ct) ?? Completed()).ContinueWith(Forward, tcs); }
                        catch (Exception ex) { tcs.TrySetException(ex); }
                    });
                });
                return tcs.Task;
            };
            return this;
        }

        /// <summary>Append a worker step (one arg) using the flow token.</summary>
        public AsyncFlow ThenAsync<T1>(Func<T1, CancellationToken, Task> step, T1 a1)
        {
            if (step == null) throw new ArgumentNullException("step");
            var prev = _compose;
            _compose = delegate (SynchronizationContext ui, CancellationToken ct)
            {
                var tcs = new TaskCompletionSource<object>();
                prev(ui, ct).ContinueWith(delegate
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try { (step(a1, _ct) ?? Completed()).ContinueWith(Forward, tcs); }
                        catch (Exception ex) { tcs.TrySetException(ex); }
                    });
                });
                return tcs.Task;
            };
            return this;
        }

        /// <summary>Append a worker returning result (promotes to typed flow).</summary>
        public Flow<TOut> ThenAsync<TOut>(Func<CancellationToken, Task<TOut>> step)
        {
            if (step == null) throw new ArgumentNullException("step");
            var prev = _compose;

            Func<SynchronizationContext, CancellationToken, Task<TOut>> compose =
                delegate (SynchronizationContext ui, CancellationToken ct)
                {
                    var tcs = new TaskCompletionSource<TOut>();
                    prev(ui, ct).ContinueWith(delegate
                    {
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try
                            {
                                (step(_ct) ?? Completed<TOut>()).ContinueWith(delegate (Task<TOut> done)
                                {
                                    if (done.IsFaulted) tcs.TrySetException(done.Exception);
                                    else if (done.IsCanceled) tcs.TrySetCanceled();
                                    else tcs.TrySetResult(done.Result);
                                });
                            }
                            catch (Exception ex) { tcs.TrySetException(ex); }
                        });
                    });
                    return tcs.Task;
                };

            return new Flow<TOut>(_ui, _ct, compose);
        }

        /// <summary>Append a worker with one arg returning result (promotes to typed flow).</summary>
        public Flow<TOut> ThenAsync<T1, TOut>(Func<T1, CancellationToken, Task<TOut>> step, T1 a1)
        {
            if (step == null) throw new ArgumentNullException("step");
            var prev = _compose;

            Func<SynchronizationContext, CancellationToken, Task<TOut>> compose =
                delegate (SynchronizationContext ui, CancellationToken ct)
                {
                    var tcs = new TaskCompletionSource<TOut>();
                    prev(ui, ct).ContinueWith(delegate
                    {
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try
                            {
                                (step(a1, _ct) ?? Completed<TOut>()).ContinueWith(delegate (Task<TOut> done)
                                {
                                    if (done.IsFaulted) tcs.TrySetException(done.Exception);
                                    else if (done.IsCanceled) tcs.TrySetCanceled();
                                    else tcs.TrySetResult(done.Result);
                                });
                            }
                            catch (Exception ex) { tcs.TrySetException(ex); }
                        });
                    });
                    return tcs.Task;
                };

            return new Flow<TOut>(_ui, _ct, compose);
        }

        #endregion

        #region UI steps

        /// <summary>Append a synchronous UI action.</summary>
        public AsyncFlow ThenUi(Action uiAction)
        {
            if (uiAction == null) throw new ArgumentNullException("uiAction");
            var prev = _compose;
            _compose = delegate (SynchronizationContext ui, CancellationToken ct)
            {
                var tcs = new TaskCompletionSource<object>();
                prev(ui, ct).ContinueWith(delegate
                {
                    ui.Post(delegate
                    {
                        try { uiAction(); tcs.TrySetResult(null); }
                        catch (Exception ex) { tcs.TrySetException(ex); }
                    }, null);
                });
                return tcs.Task;
            };
            return this;
        }

        /// <summary>Append an async UI step that receives the completed predecessor task (runs on UI).</summary>
        public AsyncFlow ThenUiAsync(Func<Task, Task> uiContinuation)
        {
            if (uiContinuation == null) throw new ArgumentNullException("uiContinuation");
            var prev = _compose;
            _compose = delegate (SynchronizationContext ui, CancellationToken ct)
            {
                var tcs = new TaskCompletionSource<object>();
                prev(ui, ct).ContinueWith(delegate (Task completed)
                {
                    ui.Post(delegate
                    {
                        try { (uiContinuation(completed) ?? Completed()).ContinueWith(Forward, tcs); }
                        catch (Exception ex) { tcs.TrySetException(ex); }
                    }, null);
                });
                return tcs.Task;
            };
            return this;
        }

        #endregion

        #region Run

        /// <summary>Materializes and starts the composed flow.</summary>
        public Task Run() { return _compose(_ui, _ct); }

        #endregion

        // ===== Typed flow carrying T between steps =====

        /// <summary>Typed async flow carrying a value between steps.</summary>
        public sealed class Flow<T>
        {
            private readonly SynchronizationContext _ui;
            private readonly CancellationToken _ct;
            private Func<SynchronizationContext, CancellationToken, Task<T>> _compose;

            internal Flow(SynchronizationContext ui, CancellationToken ct, Func<SynchronizationContext, CancellationToken, Task<T>> compose)
            {
                _ui = ui ?? new SynchronizationContext();
                _ct = ct;
                _compose = compose;
            }

            /// <summary>Append a worker consuming <typeparamref name="T"/> (cancelable) returning nothing.</summary>
            public Flow<T> ThenAsync(Func<T, CancellationToken, Task> step)
            {
                if (step == null) throw new ArgumentNullException("step");
                var prev = _compose;
                _compose = delegate (SynchronizationContext ui, CancellationToken ct)
                {
                    var tcs = new TaskCompletionSource<T>();
                    prev(ui, ct).ContinueWith(delegate (Task<T> prevTask)
                    {
                        var input = prevTask.Result;
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try
                            {
                                (step(input, _ct) ?? Completed()).ContinueWith(delegate (Task done)
                                {
                                    if (done.IsFaulted) tcs.TrySetException(done.Exception);
                                    else if (done.IsCanceled) tcs.TrySetCanceled();
                                    else tcs.TrySetResult(input);
                                });
                            }
                            catch (Exception ex) { tcs.TrySetException(ex); }
                        });
                    });
                    return tcs.Task;
                };
                return this;
            }

            /// <summary>Append a worker consuming <typeparamref name="T"/> (cancelable) returning <typeparamref name="TNext"/>.</summary>
            public Flow<TNext> ThenAsync<TNext>(Func<T, CancellationToken, Task<TNext>> step)
            {
                if (step == null) throw new ArgumentNullException("step");
                var prev = _compose;

                Func<SynchronizationContext, CancellationToken, Task<TNext>> compose =
                    delegate (SynchronizationContext ui, CancellationToken ct)
                    {
                        var tcs = new TaskCompletionSource<TNext>();
                        prev(ui, ct).ContinueWith(delegate (Task<T> prevTask)
                        {
                            var input = prevTask.Result;
                            ThreadPool.QueueUserWorkItem(delegate
                            {
                                try
                                {
                                    (step(input, _ct) ?? Completed<TNext>()).ContinueWith(delegate (Task<TNext> done)
                                    {
                                        if (done.IsFaulted) tcs.TrySetException(done.Exception);
                                        else if (done.IsCanceled) tcs.TrySetCanceled();
                                        else tcs.TrySetResult(done.Result);
                                    });
                                }
                                catch (Exception ex) { tcs.TrySetException(ex); }
                            });
                        });
                        return tcs.Task;
                    };

                return new Flow<TNext>(_ui, _ct, compose);
            }

            /// <summary>Append a sync UI action using the carried value.</summary>
            public Flow<T> ThenUi(Action<T> uiAction)
            {
                if (uiAction == null) throw new ArgumentNullException("uiAction");
                var prev = _compose;
                _compose = delegate (SynchronizationContext ui, CancellationToken ct)
                {
                    var tcs = new TaskCompletionSource<T>();
                    prev(ui, ct).ContinueWith(delegate (Task<T> prevTask)
                    {
                        var input = prevTask.Result;
                        ui.Post(delegate
                        {
                            try { uiAction(input); tcs.TrySetResult(input); }
                            catch (Exception ex) { tcs.TrySetException(ex); }
                        }, null);
                    });
                    return tcs.Task;
                };
                return this;
            }

            /// <summary>Append an async UI step using the carried value.</summary>
            public Flow<T> ThenUiAsync(Func<T, Task> uiAsync)
            {
                if (uiAsync == null) throw new ArgumentNullException("uiAsync");
                var prev = _compose;
                _compose = delegate (SynchronizationContext ui, CancellationToken ct)
                {
                    var tcs = new TaskCompletionSource<T>();
                    prev(ui, ct).ContinueWith(delegate (Task<T> prevTask)
                    {
                        var input = prevTask.Result;
                        ui.Post(delegate
                        {
                            try
                            {
                                (uiAsync(input) ?? Completed()).ContinueWith(delegate (Task done)
                                {
                                    if (done.IsFaulted) tcs.TrySetException(done.Exception);
                                    else if (done.IsCanceled) tcs.TrySetCanceled();
                                    else tcs.TrySetResult(input);
                                });
                            }
                            catch (Exception ex) { tcs.TrySetException(ex); }
                        }, null);
                    });
                    return tcs.Task;
                };
                return this;
            }

            /// <summary>Run the typed flow and get its final <typeparamref name="T"/>.</summary>
            public Task<T> Run() { return _compose(_ui, _ct); }
        }

        #region Utilities (Delay/Completed/Forward + sequencing)

        /// <summary>Timer-based delay (works on net20). Cancels via <paramref name="ct"/>.</summary>
        public static Task Delay(int milliseconds, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<object>();
            Timer timer = null;

            try
            {
                TimerCallback cb = delegate (object _)
                {
                    if (timer != null) timer.Dispose();
                    tcs.TrySetResult(null);
                };

                timer = new Timer(cb, null, milliseconds, Timeout.Infinite);

                if (ct.CanBeCanceled)
                {
                    ThreadPool.RegisterWaitForSingleObject(
                        ct.WaitHandle,
                        delegate (object __, bool ___)
                        {
                            try { if (timer != null) timer.Dispose(); } catch { }
                            tcs.TrySetCanceled();
                        },
                        null, -1, true);
                }
            }
            catch (Exception ex)
            {
                if (timer != null) timer.Dispose();
                tcs.TrySetException(ex);
            }

            return tcs.Task;
        }

        /// <summary>Convenience: delay that cannot be canceled.</summary>
        public static Task DelayUncancelable(int milliseconds) { return Delay(milliseconds, CancellationToken.None); }

        private static Task Completed()
        {
            var t = new TaskCompletionSource<object>();
            t.SetResult(null);
            return t.Task;
        }

        private static Task<T> Completed<T>()
        {
            var t = new TaskCompletionSource<T>();
            t.SetResult(default(T));
            return t.Task;
        }

        private static void Forward(Task finished, object state)
        {
            var tcs = (TaskCompletionSource<object>)state;
            if (finished == null) { tcs.TrySetResult(null); return; }
            if (finished.IsFaulted) tcs.TrySetException(finished.Exception);
            else if (finished.IsCanceled) tcs.TrySetCanceled();
            else tcs.TrySetResult(null);
        }

        /// <summary>Sequentially run <paramref name="next"/> after <paramref name="antecedent"/>; unwraps Task-of-Task.</summary>
        public static Task Then(Task antecedent, Func<Task> next)
        {
            if (next == null) throw new ArgumentNullException("next");
            var start = antecedent ?? Completed();
            var tcs = new TaskCompletionSource<object>();

            start.ContinueWith(delegate (Task prev)
            {
                if (prev.IsFaulted) { tcs.TrySetException(prev.Exception); return; }
                if (prev.IsCanceled) { tcs.TrySetCanceled(); return; }
                try
                {
                    var inner = next();
                    if (inner == null) { tcs.TrySetResult(null); return; }
                    inner.ContinueWith(Forward, tcs);
                }
                catch (Exception ex) { tcs.TrySetException(ex); }
            });

            return tcs.Task;
        }

        /// <summary>Sequentially run UI action after <paramref name="antecedent"/> (marshalled to UI via AmbientUi).</summary>
        public static Task ThenUi(Task antecedent, Action uiAction)
        {
            if (uiAction == null) throw new ArgumentNullException("uiAction");
            return Then(antecedent, delegate { return Ui(uiAction); });
        }

        /// <summary>Sequentially wait uncancelable delay after <paramref name="antecedent"/>.</summary>
        public static Task ThenDelayUncancelable(Task antecedent, int milliseconds)
        {
            return Then(antecedent, delegate { return Delay(milliseconds, CancellationToken.None); });
        }

        /// <summary>UI marshalling helper that returns a Task (handy for chaining).</summary>
        public static Task Ui(Action action)
        {
            var tcs = new TaskCompletionSource<object>();
            AmbientUi.Post(delegate {
                try { action(); tcs.SetResult(null); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        #endregion
    }
}
