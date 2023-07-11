using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnifiedStockExchange.Domain.Utility
{
    public class LazyList<T> : IList<T>
    {
        //TODO: Create tests for this class.

        private readonly List<T> _internalList;
        private IEnumerator<T>? _enumerator;

        /// <summary>
        /// Retrieves elements from IEnumerable<typeparamref name="T"/> as they are needed.
        /// Use yield return to generate lazy IEnumerable<typeparamref name="T"/>.
        /// </summary>
        public LazyList(IEnumerable<T> fromEnumerable)
        {
            _internalList = new List<T>();

            _enumerator = fromEnumerable.GetEnumerator();
        }

        private bool GenerateItems(int index = -1)
        {
            if (_enumerator != null)
            {
                if (index < 0)
                {
                    while (_enumerator.MoveNext())
                        _internalList.Add(_enumerator.Current);
                }
                else
                {
                    while (index >= _internalList.Count && _enumerator.MoveNext())
                        _internalList.Add(_enumerator.Current);
                }
            }

            return index < _internalList.Count;
        }

        public T this[int index]
        {
            get
            {
                GenerateItems(index);
                return _internalList[index];
            }

            set
            {
                GenerateItems(index);
                _internalList[index] = value;
            }
        }

        public int Count
        {
            get
            {
                GenerateItems();
                return _internalList.Count;
            }
        }

        public bool IsReadOnly => true;

        public void Add(T item)
        {
            GenerateItems();
            _internalList.Add(item);
        }

        public void Clear()
        {
            _enumerator = null;
            _internalList.Clear();
        }

        public bool Contains(T item)
        {
            bool result = _internalList.Contains(item);
            while (!result && GenerateItems(_internalList.Count))
            {
                //EMPTY: Generate items until found or end of enumerator.
            }

            return result;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _internalList.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new LazyListEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new LazyListEnumerator(this);
        }

        public int IndexOf(T item)
        {
            int result = _internalList.IndexOf(item);
            if (_enumerator != null)
            {
                int index = _internalList.Count - 1;
                if (item == null)
                {
                    while (result < 0 && _enumerator.MoveNext())
                    {
                        index++;
                        result = (_internalList[index] == null) ? index : -1;
                    }
                }
                else
                {
                    while (result < 0 && _enumerator.MoveNext())
                    {
                        index++;
                        result = item.Equals(_internalList[index]) ? index : -1;
                    }
                }
            }

            return result;
        }

        public void Insert(int index, T item)
        {
            GenerateItems(index);
            _internalList.Insert(index, item);
        }

        public bool Remove(T item)
        {
            bool result = _internalList.Remove(item);
            if (!result && _enumerator != null)
            {
                int index = _internalList.Count - 1;
                if (item == null)
                {
                    while (!result && _enumerator.MoveNext())
                        result = _internalList[index] == null;
                }
                else
                {
                    while (!result && _enumerator.MoveNext())
                    {
                        index++;
                        result = item.Equals(_internalList[index]);
                    }
                }

                if(result)
                {
                    _internalList.RemoveAt(index);
                }
            }

            return result;
        }

        public void RemoveAt(int index)
        {
            GenerateItems(index);
            _internalList.RemoveAt(index);
        }

        class LazyListEnumerator : IEnumerator<T>
        {
            private readonly LazyList<T> _lazyList;

            int _index;
            public LazyListEnumerator(LazyList<T> lazyList)
            {
                _lazyList = lazyList;
                _index = -1;
            }

            public void Reset()
            {
                _index = -1;
            }

            public bool MoveNext()
            {
                try
                {
                    _ = _lazyList[_index + 1];
                    _index++;
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }
            }

            public T Current => _lazyList[_index];
            object? IEnumerator.Current => Current;

            bool _isDisposed;
            public void Dispose()
            {
                if (_isDisposed)
                    return;
                _isDisposed = true;

                //EMPTY: Nothing to dispose.
            }
        }
    }

    public class LazyList : IList
    {
        LazyList<object?> _lazyList;

        public LazyList(IEnumerable enumerable)
        {
            // https://stackoverflow.com/a/812699/2109230

            _lazyList = new LazyList<object?>(enumerable.Cast<object?>());
        }

        public object? this[int index] { get => _lazyList[index]; set => _lazyList[index] = value; }

        public bool IsFixedSize => true;

        public bool IsReadOnly => true;

        public int Count => _lazyList.Count;

        public bool IsSynchronized => false;

        public object SyncRoot { get; } = new object();

        public int Add(object? value)
        {
            _lazyList.Add(value);
            return _lazyList.Count;
        }

        public void Clear()
        {
            _lazyList.Clear();
        }

        public bool Contains(object? value)
        {
            return _lazyList.Contains(value);
        }

        public void CopyTo(Array array, int index)
        {
            for (int i = 0; i < index && i < _lazyList.Count; i++)
                array.SetValue(_lazyList[i], i);
        }

        public IEnumerator GetEnumerator()
        {
            return _lazyList.GetEnumerator();
        }

        public int IndexOf(object? value)
        {
            return _lazyList.IndexOf(value);
        }

        public void Insert(int index, object? value)
        {
            _lazyList.Insert(index, value);
        }

        public void Remove(object? value)
        {
            _lazyList.Remove(value);
        }

        public void RemoveAt(int index)
        {
            _lazyList.RemoveAt(index);
        }
    }
}
