using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;
using System.Diagnostics;
using RabbitMQ.Client;
using System.Text;

namespace TweetArchiver.Fetch
{    
    class Program
    {
        static void Main(string[] args)
        {
            var configRoot = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddInMemoryCollection()
                .AddJsonFile("config.json")
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            var serviceProvider = new ServiceCollection()
                .AddSingleton(configRoot)
                .AddOptions()
                .AddLogging()
                .Configure<TwitterCredentailsConfig>(configRoot.GetSection("Twitter:Credentials"))
                .Configure<FetchConfig>(configRoot.GetSection("Twitter:Fetch"))
                .Configure<RabbitMqConfig>(configRoot.GetSection("RabbitMq"))
                .AddTransient<ITweetFetcher, TweetFetcher>()
                .AddSingleton((sp) =>
                {
                    var config = sp.GetService<IOptions<TwitterCredentailsConfig>>();

                    if (config.Value == null) throw new ArgumentNullException("TwitterCredentialsOptions");
                    if (config.Value.ConsumerKey == null) throw new ArgumentNullException("TwitterCredentailsOptions.ConsumerKey");
                    if (config.Value.ConsumerSecret == null) throw new ArgumentNullException("TwitterCredentailsOptions.ConsumerSecret");
                    if (config.Value.AccessToken == null) throw new ArgumentNullException("TwitterCredentailsOptions.AccessToken");
                    if (config.Value.AccessSecret == null) throw new ArgumentNullException("TwitterCredentailsOptions.AccessSecret");

                    var credentials = Auth.CreateCredentials(config.Value.ConsumerKey, config.Value.ConsumerSecret, config.Value.AccessToken, config.Value.AccessSecret);
                    Auth.SetCredentials(credentials);
                    return credentials;
                })
                .AddSingleton((sp) =>
                {
                    var rabbitConfig = sp.GetRequiredService<IOptions<RabbitMqConfig>>();

                    if (rabbitConfig.Value == null) throw new ArgumentNullException("RabbitMqConfig");

                    var connectionFactory = new ConnectionFactory();
                    connectionFactory.Uri = rabbitConfig.Value.Url.ToString();

                    return connectionFactory.CreateConnection();
                })
                .AddScoped((sp) =>
                {
                    var connection = sp.GetRequiredService<IConnection>();
                    return connection.CreateModel();
                })
                .BuildServiceProvider();

            serviceProvider.GetRequiredService<ILoggerFactory>().AddConsole();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var creds = serviceProvider.GetRequiredService<ITwitterCredentials>();
            logger.LogInformation($"Using consumer key = {creds.ConsumerKey}");

            var fetcher = serviceProvider.GetRequiredService<ITweetFetcher>();
            var fetchOptions = serviceProvider.GetRequiredService<IOptions<FetchConfig>>();

            if (fetchOptions.Value == null) throw new ArgumentNullException("FetcherOptions");

            logger.LogInformation($"Found {fetchOptions.Value.ScreenNames.Count()} screen names to fetch. Starting fetching in batches of {fetchOptions.Value.ParallelFetches}");

            var watch = new Stopwatch();
            watch.Start();
            foreach(var batch in fetchOptions.Value.ScreenNames.Batch(fetchOptions.Value.ParallelFetches))
            {
                var fetching = batch.Select((screenName) =>
                    Task.Run(() => FetchTweetsToRabbitMq(fetcher, serviceProvider, screenName, fetchOptions.Value.MaxBatchRetrieved)));

                Task.WhenAll(fetching).Wait();
            }
            watch.Stop();
            logger.LogInformation($"Fetching has completed, total time taken: {watch.Elapsed}");
        }

        public static void FetchTweetsToRabbitMq(ITweetFetcher fetcher, IServiceProvider serviceProvider, string screenName, int batchSize)
        {
            using (serviceProvider.CreateScope())
            {
                var model = serviceProvider.GetRequiredService<IModel>();
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                var rabbitConfig = serviceProvider.GetRequiredService<IOptions<RabbitMqConfig>>();
                logger.LogInformation($"Starting fetching process for {screenName}");
                foreach (var tweet in fetcher.GetTweets(screenName, batchSize))
                {
                    var properties = model.CreateBasicProperties();
                    properties.ContentType = "application/json";
                    properties.ContentEncoding = "utf8";
                    properties.MessageId = tweet.IdStr;
                    properties.AppId = "tweet-fetch-dotnetcore";
                    properties.Type = "tweet";
                    properties.Headers = new Dictionary<string, object>
                    {
                        {"screenName", screenName }
                    };

                    var data = tweet.ToJson();
                    var messageContent = Encoding.UTF8.GetBytes(data);

                    model.BasicPublish(rabbitConfig.Value.TargetExchange, $"tweet.{screenName}", properties, messageContent);
                    logger.LogInformation($"Published tweet @{screenName}:{tweet.Id}");
                }

                logger.LogInformation($"Fetching process for {screenName} has completed successfully");
            }
        }
    }
}