using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DSDK.Notifications
{
    /// <summary>
    /// Events, Circuit Breaker, and Logging for NotificationServices
    /// </summary>
    /// <remarks>
    /// This partial class contains:
    /// - Event Aggregator - Event dispatch system
    /// - Circuit Breaker - Error handling and resilience
    /// - ThreadLocal StringBuilder - Zero-allocation logging
    /// - Logging - Log methods (LogInfo, LogWarning, LogError)
    /// </remarks>
    public partial class NotificationServices
    {
        #region ThreadLocal StringBuilder

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static StringBuilder GetThreadLogBuilder()
        {
            var builder = threadLogBuilder.Value;

            // Auto-trim if capacity grows too large
            if (builder.Capacity > PoolSizes.StringBuilderMaxCapacity)
                builder.Capacity = PoolSizes.StringBuilderCapacity;

            builder.Length = 0; // Slightly faster than Clear()
            return builder;
        }

        #endregion

        #region Circuit Breaker

        private void CheckCircuitBreaker()
        {
            lock (circuitLock)
            {
                if (circuitBreakerOpen && Time.realtimeSinceStartup - circuitBreakerOpenTime > Timeouts.CircuitBreaker)
                {
                    circuitBreakerOpen = false;
                    consecutiveErrors = 0;
                    Debug.Log("[NotificationServices] Circuit breaker closed");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCircuitBreakerOpen()
        {
            lock (circuitLock)
            {
                return circuitBreakerOpen;
            }
        }

        private void RecordError(string operation, Exception ex)
        {
            // Update atomic counter (no lock!)
            Interlocked.Increment(ref _ctrTotalErrors);

            lock (circuitLock)
            {
                consecutiveErrors++;
                if (consecutiveErrors >= RetryConfig.CircuitBreakerThreshold)
                {
                    circuitBreakerOpen = true;
                    circuitBreakerOpenTime = Time.realtimeSinceStartup; // Use realtime to work even with timeScale=0
                    Debug.LogError($"[NotificationServices] Circuit breaker OPEN after {consecutiveErrors} errors");
                }
            }

            // Dispatch user callback OUTSIDE locks to prevent deadlock
            // Always queue to main thread to ensure UI safety and avoid deadlocks
            RunOnMainThread(() => DispatchErrorEvent(operation, ex));
        }

        private void RecordSuccess()
        {
            lock (circuitLock)
            {
                if (consecutiveErrors > 0) consecutiveErrors = 0;
            }
        }

        private void DispatchErrorEvent(string operation, Exception ex)
        {
            Action<string, Exception> handler;
            lock (errorEventLock)
            {
                handler = _onError;
            }

            if (handler == null) return;

            // OPTIMIZED: Invoke directly (zero allocation)
            // Note: MulticastDelegate automatically invokes ALL handlers sequentially
            // If one handler throws, execution stops but that's acceptable for error events
            // which are infrequent and typically have only 1-2 subscribers
            try
            {
                handler(operation, ex);
            }
            catch (Exception e)
            {
                // If handler throws, we still log the original error being dispatched
                Debug.LogError($"[NotificationServices] Error event handler failed: {e.Message}");
            }
        }

        #endregion

        #region Event Aggregator

        private void DispatchEvent(NotificationEvent.EventType type, string title, string body)
        {
            Action<NotificationEvent> handler;
            lock (eventLock)
            {
                handler = _onNotificationEvent;
            }

            if (handler == null) return;

            // Get invocation list to invoke each delegate separately for resilience
            // This ensures one handler failing doesn't affect others
            var invocationList = handler.GetInvocationList();

            // Optimization: Reuse single event for all handlers to reduce allocations
            var evt = GetPooledEvent();
            evt.Type = type;
            evt.Title = title;
            evt.Body = body;
            evt.Timestamp = DateTime.UtcNow;

            foreach (var h in invocationList)
            {
                try
                {
                    ((Action<NotificationEvent>)h).Invoke(evt);
                }
                catch (Exception e)
                {
                    LogError($"Event handler exception in {h.Method.Name}", e.Message);
                }
            }

            // Return event to pool once after all handlers
            ReturnEventToPool(evt);
        }

        #endregion

        #region Logging - Zero Allocation

        private const string LOG_PREFIX = "[NotificationServices] ";
        private const string LOG_ERROR_PREFIX = "[NotificationServices] ERROR - ";
        private const string LOG_WARNING_PREFIX = "[NotificationServices] WARNING - ";

        /// <summary>
        /// Sets the logging verbosity level
        /// </summary>
        public void SetLogLevel(LogLevel level)
        {
            currentLogLevel = level;
            if (level >= LogLevel.Info) Debug.Log($"{LOG_PREFIX}Log level set to {level}");
        }

        /// <summary>
        /// Gets the current logging level
        /// </summary>
        public LogLevel GetLogLevel() => currentLogLevel;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogInfo(string message, int value)
        {
            if (currentLogLevel < LogLevel.Info) return;
            var builder = GetThreadLogBuilder(); // Already cleared in GetThreadLogBuilder()
            builder.Append(LOG_PREFIX).Append(message).Append(": ").Append(value);
            Debug.Log(builder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogInfo(string message, string value)
        {
            if (currentLogLevel < LogLevel.Info) return;
            if (string.IsNullOrEmpty(value))
            {
                Debug.Log($"{LOG_PREFIX}{message}: (null)");
                return;
            }

            var builder = GetThreadLogBuilder(); // Already cleared
            builder.Append(LOG_PREFIX).Append(message).Append(": ").Append(value);
            Debug.Log(builder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogError(string message, string error)
        {
            // Always log errors regardless of log level for system health monitoring
            // This ensures critical issues are visible even in production
            if (string.IsNullOrEmpty(error))
            {
                Debug.LogError($"{LOG_ERROR_PREFIX}{message}: (null)");
                return;
            }

            var builder = GetThreadLogBuilder(); // Already cleared
            builder.Append(LOG_ERROR_PREFIX).Append(message).Append(": ").Append(error);
            Debug.LogError(builder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LogWarning(string message)
        {
            if (currentLogLevel < LogLevel.Warning) return;
            Debug.LogWarning($"{LOG_WARNING_PREFIX}{message}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogWarning(string message, int value)
        {
            if (currentLogLevel < LogLevel.Warning) return;
            var builder = GetThreadLogBuilder(); // Already cleared
            builder.Append(LOG_WARNING_PREFIX).Append(message).Append(": ").Append(value);
            Debug.LogWarning(builder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogWarning(string message, string value)
        {
            if (currentLogLevel < LogLevel.Warning) return;
            var builder = GetThreadLogBuilder(); // Already cleared
            builder.Append(LOG_WARNING_PREFIX).Append(message).Append(": ").Append(value);
            Debug.LogWarning(builder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogWarning(string message, int value1, int value2)
        {
            if (currentLogLevel < LogLevel.Warning) return;
            var builder = GetThreadLogBuilder(); // Already cleared
            builder.Append(LOG_WARNING_PREFIX).Append(message).Append(": ").Append(value1).Append('/').Append(value2);
            Debug.LogWarning(builder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogNotificationScheduled(string title, int seconds, int id)
        {
            if (currentLogLevel < LogLevel.Info) return;
            var builder = GetThreadLogBuilder(); // Already cleared
            builder.Append(LOG_PREFIX)
                .Append("Scheduled '")
                .Append(title)
                .Append("' in ")
                .Append(seconds)
                .Append("s (ID: ")
                .Append(id)
                .Append(')');
            Debug.Log(builder);
        }

        #endregion
    }
}