using System;
using System.Collections.Generic;

namespace AutoTranslate
{
    public class ObjectPool<T> where T : class
    {
        private readonly Queue<T> _pool;
        private readonly int _maxSize;
        private readonly Func<T> _factory;
        private readonly Action<T> _resetAction;

        public ObjectPool(Func<T> factory, int maxSize = 16, Action<T> resetAction = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
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
                return _factory();
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