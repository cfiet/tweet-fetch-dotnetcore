using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;

namespace TweetArchiver.Fetch
{
    internal class TweetFetcher : ITweetFetcher
    {
        private readonly ILogger _logger;

        public TweetFetcher(ILogger<TweetFetcher> logger)
        {
            _logger = logger;
        }

        public IEnumerable<ITweet> GetTweets(string screenName, int maxBatchSize = 200)
        {
            var currentBatchNo = 0;
            var lastId = 0L;
            var currentBatchSize = 0;
            var retrievedTotal = 0;
            do
            {
                var timelineParams = new UserTimelineParameters()
                {
                    MaximumNumberOfTweetsToRetrieve = maxBatchSize
                };

                if (lastId != 0)
                {
                    timelineParams.MaxId = lastId-1;
                }

                _logger.LogInformation($"Requesting batch {++currentBatchNo} for screen name = {screenName}");

                var fetchedTweets = Timeline.GetUserTimeline(screenName, timelineParams);
                var currentBatch = fetchedTweets.ToList();
                currentBatchSize = currentBatch.Count;
                retrievedTotal += currentBatchSize;
                lastId = currentBatch.LastOrDefault()?.Id ?? 0;
                _logger.LogInformation($"Recieved {currentBatchSize} tweets, last tweet id = {lastId}");

                foreach(var tweet in currentBatch)
                {
                    yield return tweet;
                }
            } while (currentBatchSize != 0);
            _logger.LogInformation($"Fetched {retrievedTotal} tweets of {screenName} in {currentBatchNo} batches");
        }
    }
}
