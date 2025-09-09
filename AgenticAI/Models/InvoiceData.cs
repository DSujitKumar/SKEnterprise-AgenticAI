using Azure;
using Azure.Data.Tables;
using System.Text.Json.Serialization;

namespace AgenticAI.Models
{
    public class InvoiceData : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        [JsonPropertyName("customer_id")]
        public string CustomerId { get; set; } = string.Empty;

        [JsonPropertyName("customer_name")]
        public string CustomerName { get; set; } = string.Empty;

        [JsonPropertyName("billing_address")]
        public string BillingAddress { get; set; } = string.Empty;

        [JsonPropertyName("invoice_number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [JsonPropertyName("invoice_date")]
        public string InvoiceDate { get; set; } = string.Empty;

        [JsonPropertyName("seller_id")]
        public string SellerId { get; set; } = string.Empty;

        [JsonPropertyName("seller_name")]
        public string SellerName { get; set; } = string.Empty;

        [JsonPropertyName("total_items")]
        public int TotalItems { get; set; }

        [JsonPropertyName("gross_amount")]
        public double GrossAmount { get; set; }

        [JsonPropertyName("discount_amount")]
        public double DiscountAmount { get; set; }

        [JsonPropertyName("taxable_value")]
        public double TaxableValue { get; set; }

        [JsonPropertyName("tax_amount")]
        public double TaxAmount { get; set; }

        [JsonPropertyName("grand_total")]
        public double GrandTotal { get; set; }

        [JsonPropertyName("tax_rate")]
        public double TaxRate { get; set; }

        public string TransactionType { get; set; } = string.Empty; // "BUY" or "SELL"
        public DateTime ProcessedDate { get; set; }
        public string BlobName { get; set; } = string.Empty;
    }
}