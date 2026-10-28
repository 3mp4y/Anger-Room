using System;

namespace AngerRoom.Examples.Timers
{
    /// <summary>
    /// A countdown with no Unity, UI or minigame dependencies.
    /// Its owner must call Tick; creating this object does not start a background task.
    /// </summary>
    public sealed class CountdownTimer
    {
        private enum TimerState { Stopped, Running, Paused, Expired }
        private TimerState state = TimerState.Stopped;

        // Other objects can read these values, but only this class can change them.
        public float RemainingSeconds { get; private set; }
        public bool IsRunning => state == TimerState.Running;
        public bool IsPaused => state == TimerState.Paused;

        /// <summary>Starts a fresh countdown, replacing any previous countdown.</summary>
        public void Start(float durationSeconds)
        {
            // An invalid duration usually means an Inspector/configuration mistake.
            // Reject it rather than silently awarding a failed attempt at startup.
            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be finite and greater than zero.");

            RemainingSeconds = durationSeconds;
            state = TimerState.Running;
        }

        /// <summary>
        /// Advances by the supplied number of seconds. Returns true ONLY on the
        /// call that expires a running timer; subsequent calls return false.
        /// </summary>
        public bool Tick(float elapsedSeconds)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Elapsed time must be finite and non-negative.");

            if (!IsRunning) return false;

            RemainingSeconds = Math.Max(0f, RemainingSeconds - elapsedSeconds);
            if (RemainingSeconds > 0f) return false;

            // Change state BEFORE reporting expiry. The caller can now handle a
            // failed attempt or start a new timer without repeated expiry signals.
            state = TimerState.Expired;
            return true;
        }

        public void Pause()
        {
            if (IsRunning) state = TimerState.Paused;
        }

        public void Resume()
        {
            // Resume cannot accidentally restart a stopped or expired timer.
            if (IsPaused) state = TimerState.Running;
        }

        /// <summary>Cancels the countdown without reporting expiry.</summary>
        public void Stop()
        {
            state = TimerState.Stopped;
            RemainingSeconds = 0f;
        }
    }
}
