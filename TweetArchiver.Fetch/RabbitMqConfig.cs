using System;
using System.Collections.Generic;
using System.Text;

namespace TweetArchiver.Fetch
{
    internal class RabbitMqConfig
    {
        public Uri Url { get; set; }

        public string TargetExchange { get; set; }
    }
}
