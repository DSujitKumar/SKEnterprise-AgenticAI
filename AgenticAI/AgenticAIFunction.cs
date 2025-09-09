using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using AgenticAI.Services;
using AgenticAI.Models;
using System.Net;
using System.Text.Json;
using System.Diagnostics;

namespace AgenticAI.Functions
{
    public class AgenticAIFunction
    {
        private readonly OpenAIService _openAIService;
        private readonly TableStorageService _tableStorageService;
        private readonly QueryAnalyzerService _queryAnalyzerService;
        private readonly InvoiceAnalyticsService _analyticsService;
        private readonly ILogger<AgenticAIFunction> _logger;

        public AgenticAIFunction(
            OpenAIService openAIService,
            TableStorageService tableStorageService,
            QueryAnalyzerService queryAnalyzerService,
            InvoiceAnalyticsService analyticsService,
            ILogger<AgenticAIFunction> logger)
        {
            _openAIService = openAIService;
            _tableStorageService = tableStorageService;
            _queryAnalyzerService = queryAnalyzerService;
            _analyticsService = analyticsService;
            _logger = logger;
        }

        [Function("httpTriggered-Chat")]
        public async Task<HttpResponseData> Chat(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "chat")] HttpRequestData req)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("🤖 Agentic AI Chat request received");

            try
            {
                // Parse request
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var chatRequest = JsonSerializer.Deserialize<ChatRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (chatRequest == null || string.IsNullOrEmpty(chatRequest.Message))
                {
                    return await CreateErrorResponse(req, "Invalid request. Message is required.", HttpStatusCode.BadRequest);
                }

                _logger.LogInformation($"💬 Processing message: {chatRequest.Message}");

                // Step 1: Analyze query intent with OpenAI
                var intentResponse = await _openAIService.AnalyzeQueryIntent(chatRequest.Message);
                var queryIntent = _queryAnalyzerService.ParseQueryIntent(intentResponse);

                _logger.LogInformation($"🎯 Detected query type: {queryIntent.QueryType}");

                // Step 2: Process based on query type
                QueryResult? queryResult = null;
                string responseText = "";

                switch (queryIntent.QueryType)
                {
                    case "SALES_ANALYTICS":
                        queryResult = await _analyticsService.GetSalesAnalytics(queryIntent.Parameters);
                        responseText = await _openAIService.GenerateNaturalResponse(queryIntent.QueryType, queryResult, chatRequest.Message);
                        break;

                    case "PURCHASE_ANALYTICS":
                        queryResult = await _analyticsService.GetPurchaseAnalytics(queryIntent.Parameters);
                        responseText = await _openAIService.GenerateNaturalResponse(queryIntent.QueryType, queryResult, chatRequest.Message);
                        break;

                    case "COMPARISON":
                        queryResult = await _analyticsService.GetComparisonAnalytics();
                        responseText = await _openAIService.GenerateNaturalResponse(queryIntent.QueryType, queryResult, chatRequest.Message);
                        break;

                    case "CUSTOMER_ANALYSIS":
                        if (queryIntent.Parameters.TryGetValue("customer_name", out var customerName))
                        {
                            queryResult = await _analyticsService.GetCustomerAnalytics(customerName?.ToString() ?? "");
                            responseText = await _openAIService.GenerateNaturalResponse(queryIntent.QueryType, queryResult, chatRequest.Message);
                        }
                        else
                        {
                            responseText = "I'd be happy to help analyze customer data! Could you please specify which customer you'd like me to analyze?";
                        }
                        break;

                    case "SUPPLIER_ANALYSIS":
                        // Similar to customer analysis but for suppliers
                        var purchaseData = await _analyticsService.GetPurchaseAnalytics(queryIntent.Parameters);
                        responseText = await _openAIService.GenerateNaturalResponse(queryIntent.QueryType, purchaseData, chatRequest.Message);
                        queryResult = purchaseData;
                        break;

                    case "TIME_ANALYSIS":
                        // Implement time-based analysis
                        var timeBasedData = await GetTimeBasedAnalysis(queryIntent.Parameters);
                        responseText = await _openAIService.GenerateNaturalResponse(queryIntent.QueryType, timeBasedData, chatRequest.Message);
                        queryResult = timeBasedData;
                        break;

                    case "GENERAL_CHAT":
                        responseText = await HandleGeneralChat(chatRequest.Message);
                        break;

                    case "DATA_QUERY":
                        queryResult = await HandleDataQuery(queryIntent.Parameters);
                        responseText = await _openAIService.GenerateNaturalResponse(queryIntent.QueryType, queryResult, chatRequest.Message);
                        break;

                    default:
                        responseText = "I'm here to help with your business analytics and general questions. You can ask me about sales, purchases, customers, or just chat!";
                        break;
                }

                stopwatch.Stop();

                // Create response
                var chatResponse = new ChatResponse
                {
                    Response = responseText,
                    QueryType = queryIntent.QueryType,
                    Data = queryResult,
                    SessionId = chatRequest.SessionId ?? Guid.NewGuid().ToString(),
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };

                _logger.LogInformation($"✅ Chat response generated in {stopwatch.ElapsedMilliseconds}ms");

                // Return response
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(chatResponse, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing chat request");
                return await CreateErrorResponse(req, "An error occurred while processing your request.", HttpStatusCode.InternalServerError);
            }
        }

        [Function("GetSummary")]
        public async Task<HttpResponseData> GetSummary(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "summary")] HttpRequestData req)
        {
            _logger.LogInformation("📊 Business summary request received");

            try
            {
                var allInvoices = await _tableStorageService.GetAllInvoicesAsync();
                var salesInvoices = allInvoices.Where(i => i.TransactionType == "SELL").ToList();
                var purchaseInvoices = allInvoices.Where(i => i.TransactionType == "BUY").ToList();

                var summary = new
                {
                    overview = new
                    {
                        total_invoices = allInvoices.Count,
                        sales_invoices = salesInvoices.Count,
                        purchase_invoices = purchaseInvoices.Count,
                        total_sales_value = salesInvoices.Sum(i => i.GrandTotal),
                        total_purchase_value = purchaseInvoices.Sum(i => i.GrandTotal),
                        net_profit = salesInvoices.Sum(i => i.GrandTotal) - purchaseInvoices.Sum(i => i.GrandTotal)
                    },
                    top_customers = salesInvoices
                        .GroupBy(i => i.CustomerName)
                        .OrderByDescending(g => g.Sum(i => i.GrandTotal))
                        .Take(5)
                        .Select(g => new { name = g.Key, total_value = g.Sum(i => i.GrandTotal), transactions = g.Count() })
                        .ToList(),
                    top_suppliers = purchaseInvoices
                        .GroupBy(i => i.SellerName)
                        .OrderByDescending(g => g.Sum(i => i.GrandTotal))
                        .Take(5)
                        .Select(g => new { name = g.Key, total_value = g.Sum(i => i.GrandTotal), transactions = g.Count() })
                        .ToList(),
                    monthly_trends = allInvoices
                        .Where(i => DateTime.TryParse(i.InvoiceDate, out _))
                        .GroupBy(i => new { Month = DateTime.Parse(i.InvoiceDate).ToString("yyyy-MM"), Type = i.TransactionType })
                        .Select(g => new {
                            month = g.Key.Month,
                            type = g.Key.Type,
                            total_amount = g.Sum(i => i.GrandTotal),
                            transaction_count = g.Count()
                        })
                        .OrderBy(x => x.month)
                        .ToList()
                };

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(summary, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating business summary");
                return await CreateErrorResponse(req, "Error generating summary", HttpStatusCode.InternalServerError);
            }
        }

        [Function("HealthCheck")]
        public async Task<HttpResponseData> HealthCheck(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
        {
            var health = new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                version = "1.0.0",
                services = new
                {
                    openai = "connected",
                    table_storage = "connected",
                    analytics_engine = "ready"
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(health));
            return response;
        }

        private async Task<string> HandleGeneralChat(string message)
        {
            var lowerMessage = message.ToLower();

            if (lowerMessage.Contains("hello") || lowerMessage.Contains("hi") || lowerMessage.Contains("hey"))
            {
                return "Hello! 👋 I'm your AI assistant for SKEnterprises. I can help you analyze your sales, purchases, customer data, and more. What would you like to know about your business today?";
            }

            if (lowerMessage.Contains("help") || lowerMessage.Contains("what can you do"))
            {
                return @"I can help you with:

📊 **Business Analytics:**
- ""How much did I sell this month?""
- ""What are my total purchases?""
- ""Show me my profit margins""

👥 **Customer Analysis:**
- ""Who are my top customers?""
- ""How much did customer XYZ spend?""

🏢 **Supplier Analysis:**
- ""Which suppliers do I buy from most?""
- ""Show me my biggest expenses""

📈 **Trends & Comparisons:**
- ""Compare my sales vs purchases""
- ""Show monthly trends""

💬 **General Chat:**
- I'm also here for friendly conversation!

Just ask me anything about your business or say hello! 😊";
            }

            if (lowerMessage.Contains("thank") || lowerMessage.Contains("thanks"))
            {
                return "You're very welcome! Happy to help with your business analytics anytime. Is there anything else you'd like to know? 😊";
            }

            // For other general messages, use OpenAI to generate a response
            try
            {
                var response = await _openAIService.GenerateNaturalResponse("GENERAL_CHAT", new { message = "general conversation" }, message);
                return response;
            }
            catch
            {
                return "I'm here to help! You can ask me about your business analytics or we can just chat. What's on your mind?";
            }
        }

        private async Task<QueryResult> GetTimeBasedAnalysis(Dictionary<string, object> parameters)
        {
            try
            {
                var allInvoices = await _tableStorageService.GetAllInvoicesAsync();

                // Apply date range if specified
                if (parameters.TryGetValue("date_range", out var dateRange))
                {
                    var (startDate, endDate) = _queryAnalyzerService.ParseDateRange(dateRange.ToString() ?? "all_time");

                    if (startDate != DateTime.MinValue && endDate != DateTime.MaxValue)
                    {
                        allInvoices = allInvoices.Where(i =>
                            DateTime.TryParse(i.InvoiceDate, out var invoiceDate) &&
                            invoiceDate >= startDate && invoiceDate <= endDate).ToList();
                    }
                }

                var summary = new Dictionary<string, object>
                {
                    ["total_transactions"] = allInvoices.Count,
                    ["total_amount"] = allInvoices.Sum(i => i.GrandTotal),
                    ["sales_amount"] = allInvoices.Where(i => i.TransactionType == "SELL").Sum(i => i.GrandTotal),
                    ["purchase_amount"] = allInvoices.Where(i => i.TransactionType == "BUY").Sum(i => i.GrandTotal),
                    ["date_range"] = parameters.GetValueOrDefault("date_range", "all_time")
                };

                return new QueryResult
                {
                    TotalRecords = allInvoices.Count,
                    Summary = summary,
                    Data = allInvoices.Take(20).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in time-based analysis");
                return new QueryResult();
            }
        }

        private async Task<QueryResult> HandleDataQuery(Dictionary<string, object> parameters)
        {
            try
            {
                // Handle specific data lookup requests
                if (parameters.TryGetValue("transaction_type", out var transactionType))
                {
                    var invoices = await _tableStorageService.GetInvoicesByTransactionTypeAsync(transactionType.ToString()!);

                    return new QueryResult
                    {
                        TotalRecords = invoices.Count,
                        Data = invoices.Take(50).ToList(),
                        Summary = new Dictionary<string, object>
                        {
                            ["transaction_type"] = transactionType,
                            ["total_amount"] = invoices.Sum(i => i.GrandTotal),
                            ["count"] = invoices.Count
                        }
                    };
                }

                // Default: return recent transactions
                var allInvoices = await _tableStorageService.GetAllInvoicesAsync();
                var recentInvoices = allInvoices
                    .OrderByDescending(i => i.ProcessedDate)
                    .Take(20)
                    .ToList();

                return new QueryResult
                {
                    TotalRecords = recentInvoices.Count,
                    Data = recentInvoices,
                    Summary = new Dictionary<string, object>
                    {
                        ["query_type"] = "recent_transactions",
                        ["total_amount"] = recentInvoices.Sum(i => i.GrandTotal)
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling data query");
                return new QueryResult();
            }
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, string message, HttpStatusCode statusCode)
        {
            var errorResponse = new
            {
                error = message,
                timestamp = DateTime.UtcNow,
                status_code = (int)statusCode
            };

            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(errorResponse));
            return response;
        }
    }
}