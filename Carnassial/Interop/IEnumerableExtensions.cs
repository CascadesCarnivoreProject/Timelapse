using System;
using System.Collections.Generic;

namespace Carnassial.Interop
{
    internal static class IEnumerableExtensions
    {
        public static void Dispose<T>(this IEnumerable<T?> enumerable) where T : IDisposable
        {
            if (enumerable != null)
            {
                foreach (T item in enumerable)
                {
                    if (item != null)
                    {
                        item.Dispose();
                    }
                }
            }
        }
    }
}
