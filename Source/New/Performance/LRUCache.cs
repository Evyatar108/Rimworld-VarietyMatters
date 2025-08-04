using System;
using System.Collections.Generic;

namespace VarietyMatters.New.Performance
{
    /// <summary>
    /// Single-threaded LRU (Least Recently Used) cache implementation
    /// optimized for RimWorld's single-threaded environment
    /// </summary>
    public class LRUCache<TKey, TValue>
    {
        private readonly int maxSize;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> dict;
        private readonly LinkedList<CacheItem> list;

        public LRUCache(int maxSize)
        {
            if (maxSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Cache size must be positive");
                
            this.maxSize = maxSize;
            this.dict = new Dictionary<TKey, LinkedListNode<CacheItem>>(maxSize);
            this.list = new LinkedList<CacheItem>();
        }

        /// <summary>
        /// Attempts to get a value from the cache, moving it to front if found
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            if (dict.TryGetValue(key, out var node))
            {
                // Move to front (most recently used)
                list.Remove(node);
                list.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
            value = default(TValue);
            return false;
        }

        /// <summary>
        /// Adds or updates a value in the cache
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            // Update existing item
            if (dict.TryGetValue(key, out var existingNode))
            {
                existingNode.Value.Value = value;
                list.Remove(existingNode);
                list.AddFirst(existingNode);
                return;
            }

            // Remove least recently used item if at capacity
            if (dict.Count >= maxSize && list.Last != null)
            {
                var last = list.Last;
                list.RemoveLast();
                dict.Remove(last.Value.Key);
            }

            // Add new item at front
            var newNode = new LinkedListNode<CacheItem>(new CacheItem(key, value));
            list.AddFirst(newNode);
            dict[key] = newNode;
        }

        /// <summary>
        /// Removes an item from the cache
        /// </summary>
        public bool Remove(TKey key)
        {
            if (dict.TryGetValue(key, out var node))
            {
                list.Remove(node);
                dict.Remove(key);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        public void Clear()
        {
            list.Clear();
            dict.Clear();
        }

        /// <summary>
        /// Gets the current number of items in the cache
        /// </summary>
        public int Count => dict.Count;

        /// <summary>
        /// Internal cache item structure
        /// </summary>
        private class CacheItem
        {
            public TKey Key { get; }
            public TValue Value { get; set; }

            public CacheItem(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }
    }
}