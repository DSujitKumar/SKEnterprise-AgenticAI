using System.Text.Json.Serialization;

namespace AgenticAI.Models
{
    public class QueryResult
    {
        [JsonPropertyName("total_records")]
        public int TotalRecords { get; set; }

        [JsonPropertyName("summary")]
        public Dictionary<string, object> Summary { get; set; } = new();

        [JsonPropertyName("data")]
        public List<InvoiceData>? Data { get; set; }

        [JsonPropertyName("charts")]
        public List<ChartData>? Charts { get; set; }
    }

    public class ChartData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty; // "bar", "pie", "line"

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("labels")]
        public List<string> Labels { get; set; } = new();

        [JsonPropertyName("values")]
        public List<double> Values { get; set; } = new();
    }

    public class AnalyticsSummary
    {
        public decimal TotalSales { get; set; }
        public decimal TotalPurchases { get; set; }
        public int TotalSellTransactions { get; set; }
        public int TotalBuyTransactions { get; set; }
        public decimal NetProfit { get; set; }
        public Dictionary<string, decimal> TopCustomers { get; set; } = new();
        public Dictionary<string, decimal> TopSuppliers { get; set; } = new();
        public Dictionary<string, int> MonthlySales { get; set; } = new();
        public Dictionary<string, int> MonthlyPurchases { get; set; } = new();
    }
}