using AgenticAI.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticAI.Services
{
    public class QueryAnalyzerService
    {
        private readonly ILogger<QueryAnalyzerService> _logger;

        public QueryAnalyzerService(ILogger<QueryAnalyzerService> logger)
        {
            _logger = logger;
        }

        public QueryIntent ParseQueryIntent(string openAiResponse)
        {
            try
            {
                // Clean the response to extract JSON
                var cleanedText = openAiResponse
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                var jsonStart = cleanedText.IndexOf('{');
                var jsonEnd = cleanedText.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd >= 0 && jsonEnd > jsonStart)
                {
                    var jsonContent = cleanedText.Substring(jsonStart, jsonEnd - jsonStart + 1);

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var intent = JsonSerializer.Deserialize<QueryIntent>(jsonContent, options);
                    return intent ?? CreateDefaultIntent();
                }

                return CreateDefaultIntent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing query intent");
                return CreateDefaultIntent();
            }
        }

        private QueryIntent CreateDefaultIntent()
        {
            return new QueryIntent
            {
                QueryType = "GENERAL_CHAT",
                Intent = "general conversation",
                Parameters = new Dictionary<string, object>()
            };
        }

        public (DateTime startDate, DateTime endDate) ParseDateRange(string dateRange)
        {
            var now = DateTime.Now;

            return dateRange.ToLower() switch
            {
                "today" => (now.Date, now.Date.AddDays(1).AddTicks(-1)),
                "this_week" => (now.AddDays(-(int)now.DayOfWeek), now),
                "this_month" => (new DateTime(now.Year, now.Month, 1), now),
                "this_year" => (new DateTime(now.Year, 1, 1), now),
                "last_month" => (new DateTime(now.Year, now.Month - 1, 1), new DateTime(now.Year, now.Month, 1).AddDays(-1)),
                "last_year" => (new DateTime(now.Year - 1, 1, 1), new DateTime(now.Year - 1, 12, 31)),
                _ => (DateTime.MinValue, DateTime.MaxValue) // all_time
            };
        }
    }

    public class QueryIntent
    {
        [JsonPropertyName("query_type")]
        public string QueryType { get; set; } = string.Empty;

        [JsonPropertyName("intent")]
        public string Intent { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new();

        [JsonPropertyName("natural_response_style")]
        public string NaturalResponseStyle { get; set; } = "conversational";
    }
}