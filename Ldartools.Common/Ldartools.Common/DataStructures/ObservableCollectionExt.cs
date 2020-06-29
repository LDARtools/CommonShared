using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Ldartools.Common.DataStructures
{
    [Obsolete("Please DO NOT use this collection as certain framework controls do not subscribe to update events do to their implementation failing to handle binding to inheritted types.", true)]
    public class ObservableCollectionExt<T> : ObservableCollection<T>
    {
        public ObservableCollectionExt()
        {
            
        }

        public ObservableCollectionExt(IEnumerable<T> collection) : base(collection)
        {
            
        }

        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));

            foreach (var i in collection) Items.Add(i);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
