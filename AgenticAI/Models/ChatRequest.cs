using System.Text.Json.Serialization;

namespace AgenticAI.Models
{
    public class ChatRequest
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("session_id")]
        public string? SessionId { get; set; }

        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }
    }
}