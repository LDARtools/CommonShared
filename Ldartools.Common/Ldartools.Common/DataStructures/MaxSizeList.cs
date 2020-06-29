using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ldartools.Common.DataStructures
{
    public class MaxSizeList <T> : BlockingCollection<T>
    {
        public int MaxSize { get; protected set; }

        public MaxSizeList(int maxSize)
        {
            MaxSize = maxSize;
        }


        public new void Add(T item)
        {
            base.Add(item);

            while (base.Count > MaxSize)
            {
                base.Take();
            }
        }

        //public new void Enqueue(T item)
        //{
        //    base.Enqueue(item);

        //    T old;
        //    while (base.Count > MaxSize && base.TryDequeue(out old))
        //    {
        //        ;
        //    }
        //}
    }
}
