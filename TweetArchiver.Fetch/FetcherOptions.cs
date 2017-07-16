﻿using System;
using System.Collections.Generic;
using System.Text;

namespace TweetArchiver.Fetch
{
    internal class FetchConfig
    {
        public IEnumerable<string> ScreenNames { get; set; }

        public int MaxBatchRetrieved { get; set; } = 200;

        public TimeSpan BatchDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    }
}
