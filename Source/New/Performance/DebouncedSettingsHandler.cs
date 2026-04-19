namespace VarietyMatters.New.Performance
{
    using System;
    using System.Threading;
    using VarietyMatters.New;
    using Verse;

    /// <summary>
    /// Handles debounced recalculation of variety records when settings change
    /// to prevent UI freezing during rapid setting changes
    /// </summary>
    public static class DebouncedSettingsHandler
    {
        private static Timer debounceTimer;
        private static readonly object lockObj = new object();
        private static bool recalculationPending = false;
        private const int DebounceDelayMs = 500; // Wait 500ms after last change

        /// <summary>
        /// Queue a recalculation with debouncing to prevent excessive calls
        /// </summary>
        public static void QueueRecalculation()
        {
            lock (lockObj)
            {
                recalculationPending = true;
                
                // Dispose existing timer if it exists
                debounceTimer?.Dispose();
                
                // Create new timer that will fire after the debounce delay
                debounceTimer = new Timer(ExecuteRecalculation, null, DebounceDelayMs, Timeout.Infinite);
            }
        }

        private static void ExecuteRecalculation(object state)
        {
            lock (lockObj)
            {
                if (!recalculationPending)
                    return;

                recalculationPending = false;
                
                try
                {
                    // Perform the expensive recalculation
                    PerformVarietyRecalculation();
                }
                catch (Exception ex)
                {
                    Log.Error($"Error during debounced variety recalculation: {ex}");
                }
                finally
                {
                    debounceTimer?.Dispose();
                    debounceTimer = null;
                }
            }
        }

        private static void PerformVarietyRecalculation()
        {
            if (VarietyRecord.PawnRecords == null)
                return;

            // Batch the recalculation to reduce individual overhead.
            // US-015: UpdateMaxFoodInMemory() picks up lowExpectationFloor changes so
            // existing trackers don't keep load-time maxEatenFoodSourceInMemory values.
            foreach (DietTracker dietTracker in VarietyRecord.PawnRecords)
            {
                if (dietTracker?.Pawn != null && !dietTracker.Pawn.Dead)
                {
                    dietTracker.UpdateMaxFoodInMemory();
                    dietTracker.ReCount();
                    VarietyAdjuster.AdjustVarietyLevel(dietTracker);
                }
            }
        }

        /// <summary>
        /// Force immediate recalculation (for critical setting changes)
        /// </summary>
        public static void ForceImmediateRecalculation()
        {
            lock (lockObj)
            {
                debounceTimer?.Dispose();
                debounceTimer = null;
                recalculationPending = false;
                
                PerformVarietyRecalculation();
            }
        }

        /// <summary>
        /// Clean up resources when the mod is unloaded
        /// </summary>
        public static void Cleanup()
        {
            lock (lockObj)
            {
                debounceTimer?.Dispose();
                debounceTimer = null;
                recalculationPending = false;
            }
        }
    }
}