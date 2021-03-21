using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;

namespace Carnassial.Dialog
{
    internal class ObservableArray<T> : IList<T>, IList, INotifyCollectionChanged
    {
        private readonly T[] array;
        private int previousCreateIndex;

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public ObservableArray(int length, T defaultValue)
        {
            this.array = new T[length];
            this.previousCreateIndex = 0;

            for (int index = 0; index < this.array.Length; ++index)
            {
                this.array[index] = defaultValue;
            }
        }

        object? IList.this[int index]
        {
            get { return this.array[index]; }
            set 
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                this.array[index] = (T)value; 
            }
        }

        public T this[int index]
        {
            get { return this.array[index]; }
            set { this.array[index] = value; }
        }

        public int Count
        {
            get { return this.array.Length; }
        }

        bool IList.IsFixedSize
        {
            get { return this.array.IsFixedSize; }
        }

        bool ICollection<T>.IsReadOnly
        {
            get { return this.array.IsReadOnly; }
        }

        bool IList.IsReadOnly
        {
            get { return this.array.IsReadOnly; }
        }

        bool ICollection.IsSynchronized
        {
            get { return this.array.IsSynchronized; }
        }

        object ICollection.SyncRoot
        {
            get { return this.array.SyncRoot; }
        }

        void ICollection<T>.Add(T item)
        {
            ((IList<T>)this.array).Add(item);
        }

        int IList.Add(object? value)
        {
            throw new NotSupportedException();
        }

        void ICollection<T>.Clear()
        {
            ((IList<T>)this.array).Clear();
        }

        void IList.Clear()
        {
            ((IList<T>)this.array).Clear();
        }

        bool IList.Contains(object? value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            return ((IList<T>)this.array).Contains((T)value);
        }

        bool ICollection<T>.Contains(T item)
        {
            return ((IList<T>)this.array).Contains(item);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            this.array.CopyTo(array, index);
        }

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            ((IList<T>)this.array).CopyTo(array, arrayIndex);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.array.GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return ((IList<T>)this.array).GetEnumerator();
        }

        int IList.IndexOf(object? value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            return ((IList<T>)this.array).IndexOf((T)value);
        }

        int IList<T>.IndexOf(T item)
        {
            return ((IList<T>)this.array).IndexOf(item);
        }

        void IList.Insert(int index, object? value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            ((IList<T>)this.array).Insert(index, (T)value);
        }

        void IList<T>.Insert(int index, T item)
        {
            ((IList<T>)this.array).Insert(index, item);
        }

        bool ICollection<T>.Remove(T item)
        {
            return ((IList<T>)this.array).Remove(item);
        }

        void IList.Remove(object? value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            ((IList<T>)this.array).Remove((T)value);
        }

        void IList.RemoveAt(int index)
        {
            ((IList<T>)this.array).RemoveAt(index);
        }

        void IList<T>.RemoveAt(int index)
        {
            ((IList<T>)this.array).RemoveAt(index);
        }

        public void SendElementsCreatedEvents(int stopIndex)
        {
            Debug.Assert(stopIndex >= this.previousCreateIndex, "Passed index unexpectedly below previous adds.  Did a concurrency overrun occur?");
            if (stopIndex == this.previousCreateIndex)
            {
                // nothing to do
                return;
            }

            // workaround for https://github.com/dotnet/corefx/issues/10752
            for (int index = this.previousCreateIndex; index < stopIndex; ++index)
            {
                NotifyCollectionChangedEventArgs eventArgs = new(NotifyCollectionChangedAction.Add, this.array[index], index);
                this.CollectionChanged?.Invoke(this, eventArgs);
            }

            this.previousCreateIndex = stopIndex;
        }
    }
}
