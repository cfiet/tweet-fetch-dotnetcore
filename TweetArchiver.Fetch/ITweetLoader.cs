using System.Collections.Generic;
using Tweetinvi.Models;

namespace TweetArchiver.Fetch
{
    public interface ITweetFetcher
    {
        IEnumerable<ITweet> GetTweets(string screenName, int maxBatchSize = 200);
    }
}