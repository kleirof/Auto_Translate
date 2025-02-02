using System;
using System.Collections.Generic;

namespace AutoTranslate
{
    public class LRUCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
        private readonly LinkedList<CacheItem> _order;

        private class CacheItem
        {
            public TKey Key;
            public TValue Value;

            public CacheItem(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }

        public LRUCache(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("容量必须大于0。Capacity must be greater than zero.");

            _capacity = capacity;
            _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
            _order = new LinkedList<CacheItem>();
        }

        public TValue Get(TKey key)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                return node.Value.Value;
            }
            else
            {
                return default(TValue);
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);

                value = node.Value.Value;
                return true;
            }
            else
            {
                value = default(TValue);
                return false;
            }
        }

        public void Set(TKey key, TValue value)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _order.Remove(node);
            }
            else if (_cache.Count >= _capacity)
            {
                var leastUsedNode = _order.Last;
                _cache.Remove(leastUsedNode.Value.Key);
                _order.RemoveLast();
            }

            var newNode = new LinkedListNode<CacheItem>(new CacheItem(key, value));
            _order.AddFirst(newNode);
            _cache[key] = newNode;
        }

        public bool ContainsKey(TKey key)
        {
            return _cache.ContainsKey(key);
        }
    }
}
