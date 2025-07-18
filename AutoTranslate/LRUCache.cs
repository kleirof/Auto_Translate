using System;
using System.Collections.Generic;

namespace AutoTranslate
{
    public class LRUCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
        private readonly LinkedList<CacheItem> _order;

        private readonly ObjectPool<LinkedListNode<CacheItem>> _nodePool;
        private readonly ObjectPool<CacheItem> _cacheItemPool;

        public class CacheItem
        {
            public TKey Key;
            public TValue Value;

            public CacheItem() { }

            public void Reset(TKey key, TValue value)
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

            _nodePool = new ObjectPool<LinkedListNode<CacheItem>>(() => new LinkedListNode<CacheItem>(null), capacity, node => node.Value = null);

            _cacheItemPool = new ObjectPool<CacheItem>(() => new CacheItem(), capacity, item => item.Reset(default, default));
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
                if (!EqualityComparer<TValue>.Default.Equals(node.Value.Value, value))
                    node.Value.Reset(key, value);
            }
            else
            {
                if (_cache.Count >= _capacity)
                {
                    var leastUsedNode = _order.Last;
                    _cache.Remove(leastUsedNode.Value.Key);
                    _order.RemoveLast();

                    _cacheItemPool.Return(leastUsedNode.Value);
                    _nodePool.Return(leastUsedNode);
                }

                var cacheItem = _cacheItemPool.Get();
                cacheItem.Reset(key, value);

                node = _nodePool.Get();
                node.Value = cacheItem;

                _order.AddFirst(node);
                _cache[key] = node;
            }
        }

        public bool ContainsKey(TKey key)
        {
            return _cache.ContainsKey(key);
        }

        public List<KeyValuePair<TKey, TValue>> GetOrderedKeyValuePairs()
        {
            var orderedList = new List<KeyValuePair<TKey, TValue>>(_cache.Count);
            var node = _order.First;
            while (node != null)
            {
                if (node.Value != null)
                {
                    orderedList.Add(new KeyValuePair<TKey, TValue>(node.Value.Key, node.Value.Value));
                }
                node = node.Next;
            }
            return orderedList;
        }

        public int GetCapacity()
        {
            return _capacity;
        }

        public Dictionary<TKey, LinkedListNode<CacheItem>> GetCache() 
        {
            return _cache;
        }

        public LinkedList<CacheItem> GetOrder() 
        {
            return _order;
        }
    }
}