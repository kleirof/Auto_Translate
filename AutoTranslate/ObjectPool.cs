using System;
using System.Collections.Generic;

namespace AutoTranslate
{
    public class ObjectPool<T> where T : class, new()
    {
        private readonly Queue<T> _pool;
        private readonly int _maxSize;
        private readonly Action<T> _resetAction;

        private int NewCount = 0;
        private int BorrowCount = 0;
        private int ReturnCount = 0;

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
                BorrowCount++;
                return _pool.Dequeue();
            }
            else
            {
                NewCount++;
                return new T();
            }
        }

        public void Return(T obj)
        {
            if (_resetAction != null)
            {
                ReturnCount++;
                _resetAction(obj);
            }

            if (_pool.Count < _maxSize)
            {
                _pool.Enqueue(obj);
            }
        }
    }
}
