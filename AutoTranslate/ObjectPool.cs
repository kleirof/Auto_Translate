using System;
using System.Collections.Generic;

namespace AutoTranslate
{
    public class ObjectPool<T> where T : class, new()
    {
        private readonly Queue<T> _pool;
        private readonly int _maxSize;
        private readonly Action<T> _resetAction;

        public ObjectPool(int maxSize = 16, Action<T> resetAction = null)
        {
            _maxSize = maxSize;
            _pool = new Queue<T>(maxSize);
            _resetAction = resetAction;
        }

        public T Get()
        {
            if (_pool.Count > 0)
            {
                return _pool.Dequeue();
            }
            else
            {
                return new T();
            }
        }

        public void Return(T obj)
        {
            if (_resetAction != null)
            {
                _resetAction(obj);
            }

            if (_pool.Count < _maxSize)
            {
                _pool.Enqueue(obj);
            }
        }
    }
}
