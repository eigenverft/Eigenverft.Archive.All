using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eigenverft.AirGap.Extensions.Common.Threading
{
    /// <summary>
    /// Baut eine Task-Kette, deren Fortsetzungen auf einem angegebenen &lt;see cref="SynchronizationContext"/&gt; ausgeführt werden.
    /// </summary>
    /// <remarks>
    /// Für .NET 2.0 + Theraot.Core. Unterstützt Start ohne Anfangs-Task, direkten Einstieg mit einem vorhandenen Task
    /// und Fortsetzungen als Action/Func(Task)/Func(Task,Task).
    /// </remarks>
    public sealed class AsyncChain
    {
        /// <summary>Der Ziel-&lt;see cref="SynchronizationContext"/&gt; (z. B. WindowsFormsSynchronizationContext).</summary>
        public SynchronizationContext Context { get; private set; }

        /// <summary>Der aktuelle Ende-Task der Kette; kann initial &lt;c&gt;null&lt;/c&gt; sein.</summary>
        public Task Current { get; private set; }

        private AsyncChain(SynchronizationContext context, Task current)
        {
            Context = context;
            Current = current;
        }

        /// <summary>
        /// Erzeugt eine neue Kette ausschließlich mit dem Zielkontext (kein initialer Task).
        /// </summary>
        /// <param name="context">Ziel-&lt;see cref="SynchronizationContext"/&gt;.</param>
        /// <returns>Neue &lt;see cref="AsyncChain"/&gt;.</returns>
        /// <example>
        /// <code>
        /// var chain = AsyncChain.StartWithUiContext(ui);
        /// </code>
        /// </example>
        public static AsyncChain StartWithUiContext(SynchronizationContext context)
        {
            if (context == null) throw new ArgumentNullException("context");
            return new AsyncChain(context, null);
        }

        /// <summary>
        /// Erzeugt eine neue Kette mit Zielkontext und erstem Task.
        /// </summary>
        /// <param name="context">Ziel-&lt;see cref="SynchronizationContext"/&gt;.</param>
        /// <param name="first">Erster Task.</param>
        /// <returns>Neue &lt;see cref="AsyncChain"/&gt;.</returns>
        public static AsyncChain StartWithContext(SynchronizationContext context, Task first)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (first == null) throw new ArgumentNullException("first");
            return new AsyncChain(context, first);
        }

        // -------------------------
        // Fluent Overloads (UI-Kontext)
        // -------------------------

        /// <summary>
        /// Hängt einen bereits erzeugten Task als nächsten Schritt an.
        /// </summary>
        /// <param name="nextTask">Bereits existierender Task.</param>
        /// <returns>Dieselbe &lt;see cref="AsyncChain"/&gt;.</returns>
        /// <example>
        /// <code>
        /// chain.AssignNextAsync(RunWorkAsync(...));
        /// </code>
        /// </example>
        public AsyncChain AssignNextAsync(Task nextTask)
        {
            if (nextTask == null) throw new ArgumentNullException("nextTask");

            if (Current == null)
            {
                // Kein vorheriger Schritt → beginne die Kette mit dem existierenden Task.
                Current = nextTask;
                return this;
            }

            // Es gibt bereits einen Current: erst Current beenden, dann nextTask an das Ende "spiegeln".
            var tcs = new TaskCompletionSource<object>();
            Current.ContinueWith(delegate (Task _)
            {
                Context.Post(delegate
                {
                    try
                    {
                        nextTask.ContinueWith(ContinuationTailCallback, tcs);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }, null);
            });

            Current = tcs.Task;
            return this;
        }

        /// <summary>
        /// Hängt eine synchrone Fortsetzung ohne Eingangsparameter an (läuft auf UI-Kontext).
        /// </summary>
        public AsyncChain AssignNext(Action continuation)
        {
            if (continuation == null) throw new ArgumentNullException("continuation");
            return AssignNextInternal(delegate (Task _) { continuation(); return CreateCompletedTask(); });
        }

        /// <summary>
        /// Hängt eine asynchrone Fortsetzung ohne Eingangsparameter an (läuft auf UI-Kontext).
        /// </summary>
        public AsyncChain AssignNextAsync(Func<Task> continuation)
        {
            if (continuation == null) throw new ArgumentNullException("continuation");
            return AssignNextInternal(delegate (Task _) { return continuation(); });
        }

        /// <summary>
        /// Hängt eine synchrone Fortsetzung mit Eingangstask an (läuft auf UI-Kontext).
        /// </summary>
        public AsyncChain AssignNext(Action<Task> continuation)
        {
            if (continuation == null) throw new ArgumentNullException("continuation");
            return AssignNextInternal(delegate (Task t) { continuation(t); return CreateCompletedTask(); });
        }

        /// <summary>
        /// Hängt eine asynchrone Fortsetzung mit Eingangstask an (läuft auf UI-Kontext).
        /// </summary>
        public AsyncChain AssignNextAsync(Func<Task, Task> continuation)
        {
            if (continuation == null) throw new ArgumentNullException("continuation");
            return AssignNextInternal(continuation);
        }

        /// <summary>
        /// Liefert den aktuellen Ende-Task der Kette.
        /// </summary>
        public Task AsTask()
        {
            return Current ?? CreateCompletedTask();
        }

        // -------------------------
        // intern
        // -------------------------

        private AsyncChain AssignNextInternal(Func<Task, Task> continuation)
        {
            if (continuation == null) throw new ArgumentNullException("continuation");

            if (Current == null)
            {
                // Erste Fortsetzung: direkt auf UI-Kontext starten.
                var tcs = new TaskCompletionSource<object>();
                Context.Post(delegate
                {
                    try
                    {
                        Task next = continuation(null);
                        if (next == null) next = CreateCompletedTask();
                        next.ContinueWith(ContinuationTailCallback, tcs);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }, null);

                Current = tcs.Task;
                return this;
            }

            var outerTcs = new TaskCompletionSource<object>();

            Current.ContinueWith(delegate (Task completed)
            {
                Context.Post(delegate
                {
                    try
                    {
                        Task next = continuation(completed);
                        if (next == null) next = CreateCompletedTask();
                        next.ContinueWith(ContinuationTailCallback, outerTcs);
                    }
                    catch (Exception ex)
                    {
                        outerTcs.TrySetException(ex);
                    }
                }, null);
            });

            Current = outerTcs.Task;
            return this;
        }

        private static void ContinuationTailCallback(Task tail, object tcsObj)
        {
            TaskCompletionSource<object> tcs = (TaskCompletionSource<object>)tcsObj;
            if (tail == null)
            {
                tcs.TrySetResult(null);
                return;
            }

            if (tail.IsFaulted) tcs.TrySetException(tail.Exception);
            else if (tail.IsCanceled) tcs.TrySetCanceled();
            else tcs.TrySetResult(null);
        }

        private static Task CreateCompletedTask()
        {
            TaskCompletionSource<object> t = new TaskCompletionSource<object>();
            t.SetResult(null);
            return t.Task;
        }
    }
}