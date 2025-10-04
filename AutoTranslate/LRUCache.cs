using System;
using System.Collections.Generic;

namespace AutoTranslate
{
    public class LRUCache<TKey, TValue>
    {
        private class LRUNode
        {
            public TKey Key;
            public TValue Value;
            public LRUNode Previous;
            public LRUNode Next;

            public LRUNode(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }

            public void Update(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }

        private readonly int _capacity;
        private readonly Dictionary<TKey, LRUNode> _cache;
        private readonly ObjectPool<LRUNode> _nodePool;
        private LRUNode _head;
        private LRUNode _tail;

        public LRUCache(int capacity, int preallocateSize = 0)
        {
            if (capacity <= 0)
                throw new ArgumentException("容量必须大于0。Capacity must be greater than zero.");

            _capacity = capacity;
            _cache = new Dictionary<TKey, LRUNode>(capacity);
            _nodePool = new ObjectPool<LRUNode>(
                () => new LRUNode(default, default),
                capacity,
                node => {
                    node.Previous = null;
                    node.Next = null;
                    node.Key = default;
                    node.Value = default;
                },
                Math.Min(Math.Max(preallocateSize, 0), capacity));
        }

        public TValue Get(TKey key)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                MoveToHead(node);
                return node.Value;
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
                MoveToHead(node);
                value = node.Value;
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
                if (!EqualityComparer<TValue>.Default.Equals(node.Value, value))
                    node.Update(key, value);
                MoveToHead(node);
            }
            else
            {
                if (_cache.Count >= _capacity)
                {
                    RemoveTail();
                }

                var newNode = _nodePool.Get();
                newNode.Update(key, value);
                AddToHead(newNode);
                _cache[key] = newNode;
            }
        }

        public bool ContainsKey(TKey key)
        {
            return _cache.ContainsKey(key);
        }

        private void MoveToHead(LRUNode node)
        {
            if (node == _head) return;

            if (node.Previous != null) node.Previous.Next = node.Next;
            if (node.Next != null) node.Next.Previous = node.Previous;
            if (node == _tail) _tail = node.Previous;

            AddToHead(node);
        }

        private void AddToHead(LRUNode node)
        {
            node.Previous = null;
            node.Next = _head;

            if (_head != null) _head.Previous = node;
            _head = node;

            if (_tail == null) _tail = node;
        }

        private void RemoveTail()
        {
            if (_tail == null) return;

            _cache.Remove(_tail.Key);
            var toRemove = _tail;

            _tail = _tail.Previous;
            if (_tail != null) _tail.Next = null;
            if (toRemove == _head) _head = null;

            _nodePool.Return(toRemove);
        }

        public List<KeyValuePair<TKey, TValue>> GetOrderedKeyValuePairs()
        {
            var orderedList = new List<KeyValuePair<TKey, TValue>>(_cache.Count);
            var node = _head;
            while (node != null)
            {
                orderedList.Add(new KeyValuePair<TKey, TValue>(node.Key, node.Value));
                node = node.Next;
            }
            return orderedList;
        }

        public List<KeyValuePair<TKey, TValue>> GetOrderedKeyValuePairsReverse()
        {
            var orderedList = new List<KeyValuePair<TKey, TValue>>(_cache.Count);
            var node = _tail;
            while (node != null)
            {
                orderedList.Add(new KeyValuePair<TKey, TValue>(node.Key, node.Value));
                node = node.Previous;
            }
            return orderedList;
        }
    }
}