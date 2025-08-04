namespace VarietyMatters.New.Performance
{
    using System.Collections.Concurrent;
    using System.Text;

    /// <summary>
    /// Object pool for StringBuilder instances to reduce allocations
    /// </summary>
    public static class StringBuilderPool
    {
        private static readonly ConcurrentQueue<StringBuilder> pool = new ConcurrentQueue<StringBuilder>();
        private const int MaxCapacity = 1024; // Prevent memory bloat
        private const int InitialCapacity = 256;

        public static StringBuilder Get()
        {
            if (pool.TryDequeue(out StringBuilder sb))
            {
                sb.Clear();
                return sb;
            }
            return new StringBuilder(InitialCapacity);
        }

        public static void Return(StringBuilder sb)
        {
            if (sb != null && sb.Capacity <= MaxCapacity)
            {
                pool.Enqueue(sb);
            }
        }

        /// <summary>
        /// Convenience method for single-use StringBuilder operations
        /// </summary>
        public static string Build(System.Action<StringBuilder> buildAction)
        {
            var sb = Get();
            try
            {
                buildAction(sb);
                return sb.ToString();
            }
            finally
            {
                Return(sb);
            }
        }
    }
}