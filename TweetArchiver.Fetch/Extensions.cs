using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TweetArchiver.Fetch
{
    public static class Extensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
        {
            var reminder = source;
            while(reminder.Any())
            {
                yield return reminder.Take(batchSize);
                reminder = reminder.Skip(batchSize);
            }
        }
    }
}
