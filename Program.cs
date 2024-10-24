using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;

namespace WebCrawlerTask
{
    public static class Program
    {
        static async Task Main(string[] args)
        {
            string errorLogFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Error.log");
            string urlLogFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Urls.log");

            using var httpClient = new HttpClient();
            var webCrawler = new WebCrawler(httpClient, errorLogFilePath, urlLogFilePath);

            // Check if appsettings.json exists before trying to load it
            if (!File.Exists("appsettings.json"))
            {
                webCrawler.LogError("Configuration File Not Found.");
                return;
            }

            try
            {
                // Build the configuration
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                IConfiguration config = builder.Build();

                // Retrieve the URLs from the configuration
                var urls = config.GetSection("WebCrawlerSettings:Urls").Get<List<string>>();

                // If no URLs are found, log the error and exit
                if (urls == null || !urls.Any())
                {
                    webCrawler.LogError("No URLs Found in The Configuration.");
                    return;
                }

                // Start crawling
                await webCrawler.CrawlAsync(urls);
                Console.WriteLine("Crawling Completed.");
            }
            catch (Exception ex)
            {
                // Log configuration loading error
                Console.WriteLine($"Failed to Load Configuration: {ex.Message}");
                webCrawler.LogError($"Failed to Load Configuration: {ex.Message}");                
            }
        }
    }
    }