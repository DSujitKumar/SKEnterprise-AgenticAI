using System.Text.Json.Serialization;


namespace AgenticAI.Models
{
    public class ChatResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;

        [JsonPropertyName("query_type")]
        public string QueryType { get; set; } = string.Empty; // "analytics", "general", "data_query"

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        [JsonPropertyName("session_id")]
        public string? SessionId { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("execution_time_ms")]
        public long ExecutionTimeMs { get; set; }
    }
}