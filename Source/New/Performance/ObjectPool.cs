namespace VarietyMatters.New.Performance
{
    using System;
    using System.Collections.Generic;
    using VarietyMatters.New;

    /// <summary>
    /// Simple object pool for reducing allocations and GC pressure
    /// Optimized for RimWorld's single-threaded environment
    /// </summary>
    public class ObjectPool<T> where T : class, new()
    {
        private readonly Stack<T> objects;
        private readonly Func<T> objectGenerator;
        private readonly Action<T> resetAction;
        private readonly int maxSize;

        public ObjectPool(int maxSize = 100, Func<T> objectGenerator = null, Action<T> resetAction = null)
        {
            this.maxSize = maxSize;
            this.objects = new Stack<T>(maxSize);
            this.objectGenerator = objectGenerator ?? (() => new T());
            this.resetAction = resetAction;
        }

        /// <summary>
        /// Gets an object from the pool or creates a new one
        /// </summary>
        public T Get()
        {
            if (objects.Count > 0)
            {
                return objects.Pop();
            }
            return objectGenerator();
        }

        /// <summary>
        /// Returns an object to the pool
        /// </summary>
        public void Return(T item)
        {
            if (item == null) return;

            // Reset the object if reset action is provided
            resetAction?.Invoke(item);

            // Only add to pool if under capacity
            if (objects.Count < maxSize)
            {
                objects.Push(item);
            }
        }

        /// <summary>
        /// Gets the current number of objects in the pool
        /// </summary>
        public int Count => objects.Count;

        /// <summary>
        /// Clears all objects from the pool
        /// </summary>
        public void Clear()
        {
            objects.Clear();
        }
    }

    /// <summary>
    /// Specialized pools for commonly used objects in VarietyMatters
    /// </summary>
    public static class VarietyObjectPools
    {
        // Pool for List<string> objects used in food description generation
        public static readonly ObjectPool<List<string>> StringListPool = 
            new ObjectPool<List<string>>(
                maxSize: 50,
                resetAction: list => list.Clear()
            );

        // Pool for HashSet<string> objects used in variety calculations
        public static readonly ObjectPool<HashSet<string>> StringHashSetPool = 
            new ObjectPool<HashSet<string>>(
                maxSize: 30,
                resetAction: set => set.Clear()
            );

        // Pool for Dictionary<string, object> for temporary data storage
        public static readonly ObjectPool<Dictionary<string, object>> StringObjectDictPool = 
            new ObjectPool<Dictionary<string, object>>(
                maxSize: 20,
                resetAction: dict => dict.Clear()
            );

        // Pool for List<EatenFoodSource> used in variety tracking
        public static readonly ObjectPool<List<EatenFoodSource>> EatenFoodSourceListPool =
            new ObjectPool<List<EatenFoodSource>>(
                maxSize: 25,
                resetAction: list => list.Clear()
            );
    }

    /// <summary>
    /// Disposable wrapper for automatic object return to pool
    /// </summary>
    public struct PooledObject<T> : IDisposable where T : class, new()
    {
        private readonly ObjectPool<T> pool;
        private readonly T obj;
        private bool disposed;

        public PooledObject(ObjectPool<T> pool, T obj)
        {
            this.pool = pool;
            this.obj = obj;
            this.disposed = false;
        }

        public T Value => obj;

        public void Dispose()
        {
            if (!disposed && obj != null)
            {
                pool.Return(obj);
                disposed = true;
            }
        }
    }

    /// <summary>
    /// Extension methods for easier pool usage
    /// </summary>
    public static class ObjectPoolExtensions
    {
        /// <summary>
        /// Gets a pooled object with automatic disposal
        /// </summary>
        public static PooledObject<T> GetPooled<T>(this ObjectPool<T> pool) where T : class, new()
        {
            return new PooledObject<T>(pool, pool.Get());
        }
    }
}