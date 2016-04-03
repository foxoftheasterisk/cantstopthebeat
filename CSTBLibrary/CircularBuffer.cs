using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSTBLibrary
{
    /// <summary>
    /// A simple circular buffer that always attempts to be full.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CircularBuffer<T>
    {
        private T[] buffer;
        private int size;
        private int position;
        private bool hasLooped;
        public bool IsFull
        {
            get
            {
                return hasLooped || position + 1 == size;
            }
        }

        public CircularBuffer(int _size)
        {
            size = _size;
            buffer = new T[size];
            position = -1;
        }

        public void Add(T item)
        {
            position++;
            if (position >= buffer.Length)
            {
                position = 0;
                hasLooped = true;
            }

            buffer[position] = item;
            
        }

        public T[] getArray()
        {
            T[] arr;
            int onePast = position + 1;
            if(hasLooped)
            {
                arr = new T[size];

                Array.Copy(buffer, onePast, arr, 0, size - onePast);
                Array.Copy(buffer, 0, arr, size - onePast, onePast);
                //pretty sure that is correct
                //and if not, it will probably throw an outofbounds error
                //...probably
            }
            else
            {
                arr = new T[onePast];
                Array.Copy(buffer, arr, onePast);
            }
            return arr;
        }

    }
}
