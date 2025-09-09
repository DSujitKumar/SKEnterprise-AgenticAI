using Microsoft.Extensions.Logging;
using AgenticAI.Models;

namespace AgenticAI.Services
{
    public class InvoiceAnalyticsService
    {
        private readonly TableStorageService _tableStorageService;
        private readonly ILogger<InvoiceAnalyticsService> _logger;

        public InvoiceAnalyticsService(TableStorageService tableStorageService, ILogger<InvoiceAnalyticsService> logger)
        {
            _tableStorageService = tableStorageService;
            _logger = logger;
        }

        public async Task<QueryResult> GetSalesAnalytics(Dictionary<string, object> parameters)
        {
            try
            {
                var salesInvoices = await _tableStorageService.GetInvoicesByTransactionTypeAsync("SELL");

                // Apply filters based on parameters
                salesInvoices = ApplyFilters(salesInvoices, parameters);

                var summary = new Dictionary<string, object>
                {
                    ["total_sales_amount"] = salesInvoices.Sum(i => i.GrandTotal),
                    ["total_transactions"] = salesInvoices.Count,
                    ["average_sale_value"] = salesInvoices.Any() ? salesInvoices.Average(i => i.GrandTotal) : 0,
                    ["total_items_sold"] = salesInvoices.Sum(i => i.TotalItems),
                    ["total_tax_collected"] = salesInvoices.Sum(i => i.TaxAmount)
                };

                var charts = new List<ChartData>
                {
                    CreateMonthlyChart(salesInvoices, "Monthly Sales", "SELL"),
                    CreateCustomerChart(salesInvoices, "Top Customers")
                };

                return new QueryResult
                {
                    TotalRecords = salesInvoices.Count,
                    Summary = summary,
                    Data = salesInvoices.Take(10).ToList(), // Return top 10 for performance
                    Charts = charts
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sales analytics");
                return new QueryResult();
            }
        }

        public async Task<QueryResult> GetPurchaseAnalytics(Dictionary<string, object> parameters)
        {
            try
            {
                var purchaseInvoices = await _tableStorageService.GetInvoicesByTransactionTypeAsync("BUY");

                // Apply filters
                purchaseInvoices = ApplyFilters(purchaseInvoices, parameters);

                var summary = new Dictionary<string, object>
                {
                    ["total_purchase_amount"] = purchaseInvoices.Sum(i => i.GrandTotal),
                    ["total_transactions"] = purchaseInvoices.Count,
                    ["average_purchase_value"] = purchaseInvoices.Any() ? purchaseInvoices.Average(i => i.GrandTotal) : 0,
                    ["total_items_purchased"] = purchaseInvoices.Sum(i => i.TotalItems),
                    ["total_tax_paid"] = purchaseInvoices.Sum(i => i.TaxAmount)
                };

                var charts = new List<ChartData>
                {
                    CreateMonthlyChart(purchaseInvoices, "Monthly Purchases", "BUY"),
                    CreateSupplierChart(purchaseInvoices, "Top Suppliers")
                };

                return new QueryResult
                {
                    TotalRecords = purchaseInvoices.Count,
                    Summary = summary,
                    Data = purchaseInvoices.Take(10).ToList(),
                    Charts = charts
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase analytics");
                return new QueryResult();
            }
        }

        public async Task<QueryResult> GetComparisonAnalytics()
        {
            try
            {
                var allInvoices = await _tableStorageService.GetAllInvoicesAsync();
                var salesInvoices = allInvoices.Where(i => i.TransactionType == "SELL").ToList();
                var purchaseInvoices = allInvoices.Where(i => i.TransactionType == "BUY").ToList();

                var totalSales = salesInvoices.Sum(i => i.GrandTotal);
                var totalPurchases = purchaseInvoices.Sum(i => i.GrandTotal);

                var summary = new Dictionary<string, object>
                {
                    ["total_sales"] = totalSales,
                    ["total_purchases"] = totalPurchases,
                    ["net_profit"] = totalSales - totalPurchases,
                    ["profit_margin_percentage"] = totalSales > 0 ? ((totalSales - totalPurchases) / totalSales) * 100 : 0,
                    ["sales_transactions"] = salesInvoices.Count,
                    ["purchase_transactions"] = purchaseInvoices.Count
                };

                var charts = new List<ChartData>
                {
                    new ChartData
                    {
                        Type = "pie",
                        Title = "Sales vs Purchases",
                        Labels = new List<string> { "Sales", "Purchases" },
                        Values = new List<double> { totalSales, totalPurchases }
                    }
                };

                return new QueryResult
                {
                    TotalRecords = allInvoices.Count,
                    Summary = summary,
                    Charts = charts
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comparison analytics");
                return new QueryResult();
            }
        }

        public async Task<QueryResult> GetCustomerAnalytics(string customerName)
        {
            try
            {
                var customerInvoices = await _tableStorageService.GetInvoicesByCustomerAsync(customerName);

                var summary = new Dictionary<string, object>
                {
                    ["customer_name"] = customerName,
                    ["total_amount"] = customerInvoices.Sum(i => i.GrandTotal),
                    ["total_transactions"] = customerInvoices.Count,
                    ["average_transaction_value"] = customerInvoices.Any() ? customerInvoices.Average(i => i.GrandTotal) : 0,
                    ["transaction_types"] = customerInvoices.GroupBy(i => i.TransactionType).ToDictionary(g => g.Key, g => g.Count())
                };

                return new QueryResult
                {
                    TotalRecords = customerInvoices.Count,
                    Summary = summary,
                    Data = customerInvoices
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer analytics");
                return new QueryResult();
            }
        }

        private List<InvoiceData> ApplyFilters(List<InvoiceData> invoices, Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("date_range", out var dateRange) && dateRange?.ToString() != "all_time")
            {
                // Apply date filtering logic here
                // Implementation depends on your date parsing logic
            }

            if (parameters.TryGetValue("customer_name", out var customerName) && !string.IsNullOrEmpty(customerName?.ToString()))
            {
                invoices = invoices.Where(i => i.CustomerName.Contains(customerName.ToString()!, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return invoices;
        }

        private ChartData CreateMonthlyChart(List<InvoiceData> invoices, string title, string transactionType)
        {
            var monthlyData = invoices
                .Where(i => DateTime.TryParse(i.InvoiceDate, out _))
                .GroupBy(i => DateTime.Parse(i.InvoiceDate).ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.GrandTotal));

            return new ChartData
            {
                Type = "bar",
                Title = title,
                Labels = monthlyData.Keys.ToList(),
                Values = monthlyData.Values.ToList()
            };
        }

        private ChartData CreateCustomerChart(List<InvoiceData> invoices, string title)
        {
            var customerData = invoices
                .GroupBy(i => i.CustomerName)
                .OrderByDescending(g => g.Sum(i => i.GrandTotal))
                .Take(5)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.GrandTotal));

            return new ChartData
            {
                Type = "bar",
                Title = title,
                Labels = customerData.Keys.ToList(),
                Values = customerData.Values.ToList()
            };
        }

        private ChartData CreateSupplierChart(List<InvoiceData> invoices, string title)
        {
            var supplierData = invoices
                .GroupBy(i => i.SellerName)
                .OrderByDescending(g => g.Sum(i => i.GrandTotal))
                .Take(5)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.GrandTotal));

            return new ChartData
            {
                Type = "bar",
                Title = title,
                Labels = supplierData.Keys.ToList(),
                Values = supplierData.Values.ToList()
            };
        }
    }
}