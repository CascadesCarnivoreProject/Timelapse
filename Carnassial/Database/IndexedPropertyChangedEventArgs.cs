﻿using System.ComponentModel;
using System.Windows.Data;

namespace Carnassial.Database
{
    internal class IndexedPropertyChangedEventArgs<TIndex> : PropertyChangedEventArgs
    {
        public TIndex Index { get; private set; }

        public IndexedPropertyChangedEventArgs(TIndex index)
            : base(Binding.IndexerName)
        {
            this.Index = index;
        }
    }
}
