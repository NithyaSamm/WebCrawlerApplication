using System.Collections.Concurrent;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WebCrawlerTask
{
    public class WebCrawler
    {
        private readonly HttpClient _httpClient;
        private readonly ConcurrentBag<string> _foundUrls = new ConcurrentBag<string>();
        private readonly string _errorLogFilePath;
        private readonly string _urlLogFilePath;
        private static readonly object _logLock = new object();

        // Constructor that initializes the HttpClient and log file paths
        public WebCrawler(HttpClient httpClient, string errorLogFilePath, string urlLogFilePath)
        {
            _httpClient = httpClient;
            _errorLogFilePath = errorLogFilePath;
            _urlLogFilePath = urlLogFilePath;

            // Ensure log files exist
            EnsureLogFileExists(_urlLogFilePath);
            EnsureLogFileExists(_errorLogFilePath);

            // Clear existing logs for fresh start
            ClearLogFile(_errorLogFilePath);
            ClearLogFile(_urlLogFilePath);

            // Set User-Agent for the HttpClient
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
        }

        // Ensure the log file exists
        public static void EnsureLogFileExists(string path)
        {
            if (!File.Exists(path))
            {
                File.Create(path).Dispose();
            }
        }

        // Clear the contents of the log file
        private static void ClearLogFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        // // Main method to crawl a list of URLs asynchronously
        public async Task CrawlAsync(IEnumerable<string> urls)
        {
            var tasks = urls.Select(url => Task.Run(() => ProcessUrlAsync(url)));
            await Task.WhenAll(tasks);
        }

        // Processes each URL: fetches content and extracts URLs
        public async Task ProcessUrlAsync(string url)
        {
            try
            {
                string? content = await FetchContentAsync(url);
                if (!string.IsNullOrEmpty(content))
                {
                    DisplayWebsiteDetails(content, url);
                    var extractedUrls = ExtractUrls(content);
                    foreach (var extractedUrl in extractedUrls)
                    {
                        _foundUrls.Add(extractedUrl);
                        LogExtractedUrl(extractedUrl);
                    }
                }
                else
                {
                    LogWarning($"No content found for URL {url}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error processing URL {url}: {ex.Message}");
            }
        }

        // Fetches the content of a URL asynchronously
        public async Task<string?> FetchContentAsync(string url)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                LogError($"Failed to fetch content from {url}: {ex.Message}");
                return null;
            }
        }

        // Extracts URLs from HTML content
        public IEnumerable<string> ExtractUrls(string htmlContent)
        {
            try
            {

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlContent); // Load the HTML content into an HtmlDocument

                // Extract URLs from anchor and image tags
                var anchorTags = htmlDoc.DocumentNode.Descendants("a")
                    .Select(a => a.GetAttributeValue("href", null))
                    .Where(href => !string.IsNullOrEmpty(href));

                var imgTags = htmlDoc.DocumentNode.Descendants("img")
                    .Select(img => img.GetAttributeValue("src", null))
                    .Where(src => !string.IsNullOrEmpty(src));

                return anchorTags.Concat(imgTags);
            }
            catch (Exception ex)
            {
                LogError($"Failed to extract URLs: {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }

        // Displays website details such as title, meta description, and headings
        public void DisplayWebsiteDetails(string? htmlContent, string url)
        {
            try
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlContent);

                var titleNode = htmlDoc.DocumentNode.SelectSingleNode("//title");
                string title = titleNode != null ? titleNode.InnerText : "No Title";

                var metaDescriptionNode = htmlDoc.DocumentNode.SelectSingleNode("//meta[@name='description']");
                string metaDescription = metaDescriptionNode != null
                    ? metaDescriptionNode.GetAttributeValue("content", "No Description")
                    : "No Description";

                var headings = htmlDoc.DocumentNode.Descendants()
                    .Where(node => node.Name.StartsWith('h') && node.Name.Length == 2)
                    .Select(node => $"{node.Name}: {node.InnerText.Trim()}");

                StringBuilder logContent = new StringBuilder();
                logContent.AppendLine($"Website URL: {url}");
                logContent.AppendLine($"Title: {title}");
                logContent.AppendLine($"Meta Description: {metaDescription}");
                logContent.AppendLine("Headings:");
                foreach (var heading in headings)
                {
                    logContent.AppendLine($"  - {heading}");
                }

                // Log content to file
                LogMessage(_urlLogFilePath, "INFO", logContent.ToString());

                // Console output
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"Website URL: {url}");
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"Title: {title}");
                Console.WriteLine($"Meta Description: {metaDescription}");
                Console.WriteLine("Headings:");
                Console.ResetColor();
                foreach (var heading in headings)
                {
                    Console.WriteLine($"  - {heading}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to display website details for URL {url}: {ex.Message}");
            }
        }


        // Logs an error message to the error log file
        public void LogError(string message)
        {
            LogMessage(_errorLogFilePath, "ERROR", message);
        }

        // Logs a warning message to the error log file
        public void LogWarning(string message)
        {
            LogMessage(_errorLogFilePath, "WARNING", message);
        }
        public void LogExtractedUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                _foundUrls.Add(url); // Store the URL in the collection
                LogMessage(_urlLogFilePath, "URL", url); 
            }
        }

        // Writes a log message to a specified log file
        public static void LogMessage(string logFilePath, string level, string message)
        {
            try
            {
                lock (_logLock)
                {
                    File.AppendAllText(logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }

    }
}
