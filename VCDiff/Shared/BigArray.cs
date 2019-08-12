using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VCDiff.Shared
{
    public class BigArray<T> : IEnumerable<T>, IDisposable
    {
        private const int ShiftCount = 19;
        public const ulong Granularity = 1 << 19;
        private T[][] _arrays;
        private bool _disposed;
        private ulong _itemSize;
        private ulong _numberOfArrays;
        public string Tag;
        public BigArray() : this(Granularity)
        {
        }
        public BigArray(ulong size)
        {
            if (size < Granularity)
                size = Granularity;
            try
            {
                _numberOfArrays = size / Granularity;
                while (_numberOfArrays * Granularity < size)
                    ++_numberOfArrays;
                Length = _numberOfArrays * Granularity;
                _arrays = new T[_numberOfArrays][];
                for (ulong i = 0; i < _numberOfArrays; ++i)
                    _arrays[i] = new T[Granularity];
            }
            catch (Exception ex)
            {
                throw new Exception($"'Initialize:BigArray' Exception: {ex.Message}");
            }
        }
        public ulong Length { get; private set; }
        public T this[ulong index]
        {
            get
            {
                if (index >= Length)
                    throw new Exception($"Getter: Index out of bounds, Index: '{index}' must be less than the Length: '{Length}'.");
                return _arrays[index >> ShiftCount][index & (Granularity - 1)];
            }
            set
            {
                //lock (this)
                //{
                    if (index >= Length)
                        try
                        {
                            var nah = _numberOfArrays;
                            _numberOfArrays = index / Granularity;
                            while (_numberOfArrays * Granularity < index)
                                ++_numberOfArrays;
                            Length = _numberOfArrays * Granularity;
                            var temp = new BigArray<T>(Length);
                            for (var a = 0ul; a < nah; a++)
                                Array.Copy(_arrays[a], temp._arrays[a], (int)Granularity);
                            _arrays = temp._arrays;
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"'Resize:BigArray' Exception: {ex.Message}");
                        }
                    _arrays[index >> ShiftCount][index & (Granularity - 1)] = value;
                //}
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(this);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }
        public void Clear()
        {
            for (var a = 0ul; a < _numberOfArrays; a++)
                Array.Clear(_arrays[a], 0, (int)Granularity);
            Length = 0;
        }
        public BigArray<T> Copy(ulong newsize)
        {
            var temp = new BigArray<T>(newsize);
            for (var a = 0ul; a < _numberOfArrays; a++)
                Array.Copy(_arrays[a], temp._arrays[a], (int)Granularity);
            return temp;
        }
        private void Dispose(bool disposing)
        {
            if (!_disposed)
                if (disposing)
                    _arrays = null;
            _disposed = true;
        }
        ~BigArray()
        {
            Dispose(true);
        }
        [Serializable]
        public struct Enumerator : IEnumerator<T>
        {
            private readonly BigArray<T> _array;
            private ulong _index;
            public T Current { get; private set; }
            object IEnumerator.Current
            {
                get
                {
                    if (_index == _array.Length + 1)
                        throw new Exception($"Enumerator out of range: {_index}");
                    return Current;
                }
            }
            internal Enumerator(BigArray<T> array)
            {
                _array = array;
                _index = 0;
                Current = default;
            }
            public void Dispose()
            {
            }
            public bool MoveNext()
            {
                if (_index < _array.Length)
                {
                    Current = _array[_index];
                    _index++;
                    return true;
                }
                _index = _array.Length + 1;
                Current = default;
                return false;
            }
            void IEnumerator.Reset()
            {
                _index = 0;
                Current = default;
            }
        }
    }
}
