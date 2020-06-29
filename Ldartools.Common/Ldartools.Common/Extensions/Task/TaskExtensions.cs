using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Ldartools.Common.Extensions.Task
{
    public static class TaskExtensions
    {
        [ExcludeFromCodeCoverage]
        public static void DontWait(this System.Threading.Tasks.Task task)
        {
            task.ContinueWith(t => { if( t.Exception != null) throw t.Exception; },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        [ExcludeFromCodeCoverage]
        public static void DontWait<TResult>(this Task<TResult> task)
        {
            task.ContinueWith(t => { if (t.Exception != null) throw t.Exception; },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        #region Taken from https://github.com/Microsoft/vs-threading/blob/b2994a1faa1e3303daf21637f5470b571c2c74c5/src/Microsoft.VisualStudio.Threading/ThreadingTools.cs

        /// <summary>
        /// Wraps a task with one that will complete as cancelled based on a cancellation token,
        /// allowing someone to await a task but be able to break out early by cancelling the token.
        /// </summary>
        /// <typeparam name="T">The type of value returned by the task.</typeparam>
        /// <param name="task">The task to wrap.</param>
        /// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
        /// <returns>The wrapping task.</returns>
        public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                return task;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return TaskFromCanceled<T>(cancellationToken);
            }

            return WithCancellationSlow(task, cancellationToken);
        }

        internal static Task<T> TaskFromCanceled<T>(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<T>();
            tcs.TrySetCanceled(cancellationToken);
            return tcs.Task;
        }

        /// <summary>
        /// Wraps a task with one that will complete as cancelled based on a cancellation token,
        /// allowing someone to await a task but be able to break out early by cancelling the token.
        /// </summary>
        /// <typeparam name="T">The type of value returned by the task.</typeparam>
        /// <param name="task">The task to wrap.</param>
        /// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
        /// <returns>The wrapping task.</returns>
        private static async Task<T> WithCancellationSlow<T>(Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (task != await System.Threading.Tasks.Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            // Rethrow any fault/cancellation exception, even if we awaited above.
            // But if we skipped the above if branch, this will actually yield
            // on an incompleted task.
            return await task.ConfigureAwait(false);
        }

        #endregion
    }
}
