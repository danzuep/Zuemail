using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace Zuemail.Core.Extensions
{
    public static class SystemExtensions
    {
        public static string ToEnumeratedString<T>(this IEnumerable<T> data, string div = ", ") =>
            data is null ? "" : string.Join(div, data.Select(o => o?.ToString() ?? ""));

        public static void ActionEach<T>(this IEnumerable<T> items, Action<T> action, CancellationToken cancellationToken = default)
        {
            if (items != null && action != null)
            {
                foreach (T item in items)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    else if (item != null)
                        action(item);
                }
            }
        }
    }
}
