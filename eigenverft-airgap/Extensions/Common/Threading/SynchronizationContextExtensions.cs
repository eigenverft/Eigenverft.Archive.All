using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eigenverft.AirGap.Extensions.Common.Threading
{
    /// <summary>
    /// Erweiterungen für &lt;see cref="SynchronizationContext"/&gt;, &lt;see cref="Task"/&gt; und &lt;see cref="CancellationTokenSource"/&gt;.
    /// </summary>
    public static class SynchronizationContextExtensions
    {
        /// <summary>
        /// Postet eine stark typisierte Aktion auf den angegebenen &lt;see cref="SynchronizationContext"/&gt;.
        /// </summary>
        public static void Post<T>(this SynchronizationContext context, Action<T> action, T state)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (action == null) throw new ArgumentNullException("action");

            context.Post(delegate (object boxed)
            {
                action((T)boxed);
            }, state);
        }

        /// <summary>
        /// Registriert eine synchrone Fortsetzung für einen &lt;see cref="Task"/&gt; auf einem &lt;see cref="SynchronizationContext"/&gt;.
        /// </summary>
        public static void ContinueWithOnContext(this Task task, SynchronizationContext context, Action<Task> continuation)
        {
            if (task == null) throw new ArgumentNullException("task");
            if (context == null) throw new ArgumentNullException("context");
            if (continuation == null) throw new ArgumentNullException("continuation");

            task.ContinueWith(delegate (Task t)
            {
                context.Post(delegate (object boxed)
                {
                    continuation((Task)boxed);
                }, t);
            });
        }

        /// <summary>
        /// Registriert eine synchrone Fortsetzung für einen &lt;see cref="Task{TResult}"/&gt; auf einem &lt;see cref="SynchronizationContext"/&gt;.
        /// </summary>
        public static void ContinueWithOnContext<TResult>(this Task<TResult> task, SynchronizationContext context, Action<Task<TResult>> continuation)
        {
            if (task == null) throw new ArgumentNullException("task");
            if (context == null) throw new ArgumentNullException("context");
            if (continuation == null) throw new ArgumentNullException("continuation");

            task.ContinueWith(delegate (Task<TResult> t)
            {
                context.Post(delegate (object boxed)
                {
                    continuation((Task<TResult>)boxed);
                }, t);
            });
        }
    }

    /// <summary>
    /// Ergänzende Erweiterungen für &lt;see cref="CancellationTokenSource"/&gt;.
    /// </summary>
    public static class CancellationTokenSourceExtensions
    {
        /// <summary>
        /// Versucht, die Quelle zu abbrechen; fängt Disposed-Situationen ab.
        /// </summary>
        public static bool TryCancel(this CancellationTokenSource cts)
        {
            if (cts == null) return false;
            try { cts.Cancel(); return true; }
            catch (ObjectDisposedException) { return false; }
        }
    }
}

