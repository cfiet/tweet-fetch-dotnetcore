using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace TweetArchiver.Fetch
{
    public static class Extensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
        {
            int currentBatchNumber = 0;

            IEnumerable<T> nextBatch;
            do
            {
                nextBatch = source.Skip(currentBatchNumber * batchSize).Take(batchSize);
                yield return nextBatch;
                currentBatchNumber += 1;
            } while (nextBatch.Any());
        }
    }
}
