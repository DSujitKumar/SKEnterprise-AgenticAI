using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AgenticAI.Services
{
    public class OpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<OpenAIService> _logger;

        public OpenAIService(IConfiguration configuration, ILogger<OpenAIService> logger, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _apiKey = Environment.GetEnvironmentVariable("OpenAPIKey") ?? throw new ArgumentNullException("OpenAI:ApiKey configuration is missing");
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.Timeout = TimeSpan.FromMinutes(2);
        }

        public async Task<string> AnalyzeQueryIntent(string userMessage)
        {
            try
            {
                var prompt = $@"Analyze this user message and determine what type of query it is and extract key parameters.

User Message: ""{userMessage}""

Classify the query into one of these categories:
1. SALES_ANALYTICS - Questions about sales data, revenue, selling performance
2. PURCHASE_ANALYTICS - Questions about purchase data, expenses, buying patterns  
3. COMPARISON - Comparing sales vs purchases, profit analysis
4. CUSTOMER_ANALYSIS - Questions about specific customers, top customers
5. SUPPLIER_ANALYSIS - Questions about suppliers, vendors
6. TIME_ANALYSIS - Monthly, yearly trends, date-based queries
7. GENERAL_CHAT - General conversation, greetings, non-business queries
8. DATA_QUERY - Specific data lookup requests

Return a JSON response with this format:
{{
    ""query_type"": ""SALES_ANALYTICS"",
    ""intent"": ""user wants to know total sales"",
    ""parameters"": {{
        ""transaction_type"": ""SELL"",
        ""date_range"": ""all_time"",
        ""aggregation"": ""sum"",
        ""group_by"": null
    }},
    ""natural_response_style"": ""business_analytical""
}}

Parameters to extract when relevant:
- transaction_type: ""SELL"", ""BUY"", or ""BOTH""
- date_range: ""today"", ""this_month"", ""this_year"", ""all_time"", or specific dates
- customer_name: specific customer if mentioned
- seller_name: specific seller/supplier if mentioned
- aggregation: ""sum"", ""count"", ""average"", ""max"", ""min""
- group_by: ""customer"", ""seller"", ""month"", ""year"" etc.";

                var requestBody = new
                {
                    model = "gpt-4o-mini",
                    messages = new[]
                    {
                        new { role = "system", content = "You are an AI assistant specialized in analyzing business queries about invoice data." },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 500,
                    temperature = 0.1
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"OpenAI API error: {response.StatusCode}");
                    return CreateFallbackResponse(userMessage);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);

                return openAiResponse?.choices?[0]?.message?.content?.Trim() ?? CreateFallbackResponse(userMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing query intent with OpenAI");
                return CreateFallbackResponse(userMessage);
            }
        }

        public async Task<string> GenerateNaturalResponse(string queryType, object data, string originalQuestion)
        {
            try
            {
                var dataJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

                var prompt = $@"Generate a natural, conversational response based on this data analysis.

Original Question: ""{originalQuestion}""
Query Type: {queryType}
Analysis Results: {dataJson}

Guidelines:
1. Be conversational and helpful
2. Use business language appropriate for SKEnterprises
3. Include specific numbers and insights
4. Suggest follow-up questions or actions when relevant
5. If it's general chat, respond naturally without business jargon
6. Keep responses concise but informative
7. Use emojis sparingly and professionally

Generate a natural response:";

                var requestBody = new
                {
                    model = "gpt-4o-mini",
                    messages = new[]
                    {
                        new { role = "system", content = "You are a helpful AI assistant for SKEnterprises, skilled at explaining business data in a conversational way." },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 800,
                    temperature = 0.7
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

                if (!response.IsSuccessStatusCode)
                {
                    return "I found some data for your query, but I'm having trouble explaining it right now. Please check the data section of the response.";
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);

                return openAiResponse?.choices?[0]?.message?.content?.Trim() ?? "I found the information you requested. Please check the data section for details.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating natural response");
                return "I found the information you requested, but I'm having trouble explaining it right now.";
            }
        }

        private string CreateFallbackResponse(string userMessage)
        {
            var lowerMessage = userMessage.ToLower();

            if (lowerMessage.Contains("sell") || lowerMessage.Contains("sale") || lowerMessage.Contains("revenue"))
                return """{"query_type": "SALES_ANALYTICS", "intent": "sales query", "parameters": {"transaction_type": "SELL"}}""";

            if (lowerMessage.Contains("buy") || lowerMessage.Contains("purchase") || lowerMessage.Contains("expense"))
                return """{"query_type": "PURCHASE_ANALYTICS", "intent": "purchase query", "parameters": {"transaction_type": "BUY"}}""";

            return """{"query_type": "GENERAL_CHAT", "intent": "general conversation", "parameters": {}}""";
        }

        private class OpenAIResponse
        {
            public Choice[]? choices { get; set; }
        }

        private class Choice
        {
            public Message? message { get; set; }
        }

        private class Message
        {
            public string? content { get; set; }
        }
    }
}