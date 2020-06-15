﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Blueprint.Tasks.Provider;

namespace Blueprint.Tasks
{
    /// <summary>
    /// A wrapper for a <see cref="IBackgroundTaskScheduleProvider" /> implementation that will use <see cref="Activity" /> to
    /// track the scheduling of a task in a request, to give a new ID to each task enqueued (see <see cref="BackgroundTaskMetadata.ActivityId" />) and
    /// add any <see cref="Activity.Baggage" /> to <see cref="BackgroundTaskMetadata.ActivityBaggage" />.
    /// </summary>
    public class ActivityTrackingBackgroundTaskScheduleProvider : IBackgroundTaskScheduleProvider
    {
        private readonly IBackgroundTaskScheduleProvider innerProvider;

        /// <summary>
        /// Initialises a new instance of the <see cref="ActivityTrackingBackgroundTaskScheduleProvider" /> class.
        /// </summary>
        /// <param name="innerProvider">The <see cref="IBackgroundTaskScheduleProvider" /> to wrap.</param>
        public ActivityTrackingBackgroundTaskScheduleProvider(IBackgroundTaskScheduleProvider innerProvider)
        {
            this.innerProvider = innerProvider;
        }

        /// <summary>
        /// The wrapped <see cref="IBackgroundTaskScheduleProvider" />.
        /// </summary>
        public IBackgroundTaskScheduleProvider InnerProvider => innerProvider;

        /// <inheritdoc />
        /// <remarks>
        /// This method will set the request ID and baggage from the current ambient <see cref="Activity" /> on to the
        /// background task.
        /// </remarks>
        public Task<string> EnqueueAsync(BackgroundTaskEnvelope task)
        {
            return WithActivityAsync(task, () => innerProvider.EnqueueAsync(task));
        }

        /// <inheritdoc />
        /// <remarks>
        /// This method will set the request ID and baggage from the current ambient <see cref="Activity" /> on to the
        /// background task.
        /// </remarks>
        public Task<string> ScheduleAsync(BackgroundTaskEnvelope task, TimeSpan delay)
        {
            return WithActivityAsync(task, () => innerProvider.ScheduleAsync(task, delay));
        }

        /// <inheritdoc />
        /// <remarks>
        /// This method will set the request ID and baggage from the current ambient <see cref="Activity" /> on to the
        /// background task.
        /// </remarks>
        public Task<string> EnqueueChildAsync(BackgroundTaskEnvelope task, string parentId, BackgroundTaskContinuationOptions continuationOptions)
        {
            return WithActivityAsync(task, () => innerProvider.EnqueueChildAsync(task, parentId, continuationOptions));
        }

        private static async Task<string> WithActivityAsync(BackgroundTaskEnvelope task, Func<Task<string>> fn)
        {
            var activity = new Activity("Task_Out").Start();

            task.Metadata.ActivityId = activity.Id;
            task.Metadata.ActivityBaggage = activity.Baggage;

            var id = await fn();

            activity.Stop();

            return id;
        }
    }
}
