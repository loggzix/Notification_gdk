using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DSDK.Notifications
{
    /// <summary>
    /// Main thread dispatcher for NotificationServices
    /// </summary>
    /// <remarks>
    /// This partial class contains:
    /// - ProcessMainThreadActions() - Batch processing with time budget
    /// - RunOnMainThread() - Queue actions for main thread
    /// - TryRunOnMainThread() - Try queue with timeout protection
    /// </remarks>
    public partial class NotificationServices
    {
        #region Main Thread Dispatcher

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RunOnMainThread(Action action)
        {
            TryRunOnMainThread(action, dropOldestIfFull: true);
        }

        /// <summary>
        /// Async version of RunOnMainThread - waits for action to complete
        /// </summary>
        internal async Task RunOnMainThreadAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            RunOnMainThread(() =>
            {
                try
                {
                    action?.Invoke();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Tries to enqueue an action with capacity guard and policy
        /// </summary>
        /// <param name="action">Action to execute on main thread</param>
        /// <param name="dropOldestIfFull">If true, drops oldest when full; otherwise rejects new action</param>
        /// <returns>True if action was enqueued, false if rejected or queue was full</returns>
        internal bool TryRunOnMainThread(Action action, bool dropOldestIfFull = true)
        {
            if (action == null) return false;

            bool droppedOldest = false;

            lock (mainThreadLock)
            {
                if (mainThreadActions.Count >= Limits.MainThreadQueueCapacity)
                {
                    if (dropOldestIfFull && mainThreadActions.Count > 0)
                    {
                        // Remove oldest to make room for new action
                        mainThreadActions.Dequeue();
                        droppedOldest = true;
                    }
                    else
                    {
                        // Reject new action (use for critical operations)
                        return false;
                    }
                }

                mainThreadActions.Enqueue(action);
            }

            // Track drops OUTSIDE lock to avoid contention (only when we dropped the oldest)
            if (droppedOldest) Interlocked.Increment(ref _ctrQueueDrops);

            return true;
        }

        private void ProcessMainThreadActions()
        {
            // Lazy init reusable batch array to avoid allocation
            if (_mainThreadActionBatch == null) _mainThreadActionBatch = new Action[16];

            const int BATCH_SIZE = 16; // Process in batches to reduce time checks
            var start = Time.realtimeSinceStartup;
            int processed = 0;

            // Batch process with time budget to prevent frame drops
            while (processed < Limits.MaxActionsPerFrame)
            {
                int batchCount = 0;

                // Batch dequeue to reduce lock contention
                lock (mainThreadLock)
                {
                    while (batchCount < BATCH_SIZE && mainThreadActions.Count > 0)
                    {
                        _mainThreadActionBatch[batchCount++] = mainThreadActions.Dequeue();
                    }
                }

                if (batchCount == 0) break;

                // Execute batch
                for (int i = 0; i < batchCount; i++)
                {
                    // Clear BEFORE invoke to prevent reference retention if exception occurs
                    var action = _mainThreadActionBatch[i];
                    _mainThreadActionBatch[i] = null;

                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        LogError("Main thread action failed", e.Message);
                        Interlocked.Increment(ref _ctrTotalErrors);
                    }

                    processed++;
                }

                // Check time budget AFTER processing batch (reduced overhead)
                if ((Time.realtimeSinceStartup - start) * 1000f >= Timeouts.MaxProcessBudgetMs)
                    break; // Out of time budget for this frame
            }
        }

        #endregion
    }
}