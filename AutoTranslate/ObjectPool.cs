using System;
using System.Collections.Generic;

namespace AutoTranslate
{
    public class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _pool;
        private readonly int _maxSize;
        private readonly Func<T> _factory;
        private readonly Action<T> _resetAction;
        private readonly int _preallocateSize;

        public ObjectPool(Func<T> factory, int maxSize = 16, Action<T> resetAction = null, int preallocateSize = 0)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _maxSize = maxSize > 0 ? maxSize : 0;
            _pool = new Stack<T>(maxSize);
            _resetAction = resetAction;
            _preallocateSize = Math.Min(Math.Max(preallocateSize, 0), maxSize);

            PreallocateObjects();
        }

        private void PreallocateObjects()
        {
            for (int i = 0; i < _preallocateSize; i++)
            {
                T obj = _factory();
                _pool.Push(obj);
            }
        }

        public T Get()
        {
            if (_pool.Count > 0)
            {
                return _pool.Pop();
            }
            else
            {
                return _factory();
            }
        }

        public void Return(T obj)
        {
            if (obj == null) return;

            _resetAction?.Invoke(obj);

            if (_pool.Count < _maxSize)
            {
                _pool.Push(obj);
            }
        }
    }
}