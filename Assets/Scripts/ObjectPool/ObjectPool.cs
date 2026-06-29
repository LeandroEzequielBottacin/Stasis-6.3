using System;
using System.Collections.Generic;

namespace ObjectPool
{
    public class ObjectPool<T>
    {
        private readonly Func<T> _factoryMethod;
        private readonly Action<T, bool> _turnOnOffCallback;
        private readonly bool _dynamic;

        private readonly List<T> _currentStock = new List<T>();

        public ObjectPool(Func<T> factoryMethod, Action<T, bool> callback, int initialStonks = 1, bool dynamic = true)
        {
            _factoryMethod = factoryMethod;
            _turnOnOffCallback = callback;
            _dynamic = dynamic;

            for (int i = 0; i < initialStonks; i++)
            {
                T obj = _factoryMethod();
                _turnOnOffCallback(obj, false);
                _currentStock.Add(obj);
            }
        }

        public T GetObject()
        {
            var result = default(T);
            if(_currentStock.Count > 0)
            {
                result = _currentStock[0];
                _currentStock.RemoveAt(0);
            }
            else if(_dynamic)
            {
                result = _factoryMethod();
            }

            if(result != null) _turnOnOffCallback(result, true);

            return result;
        }

        public void ReturnObject(T obj)
        {
            _turnOnOffCallback(obj, false);
            _currentStock.Add(obj);
        }
    }
}
