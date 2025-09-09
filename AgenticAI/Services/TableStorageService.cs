using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AgenticAI.Models;

namespace AgenticAI.Services
{
    public class TableStorageService
    {
        private readonly TableClient _tableClient;
        private readonly ILogger<TableStorageService> _logger;

        public TableStorageService(IConfiguration configuration, ILogger<TableStorageService> logger)
        {
            _logger = logger;
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ??
                throw new ArgumentNullException("AzureWebJobsStorage configuration is missing");
            var tableName = Environment.GetEnvironmentVariable("TableName") ?? "InvoiceData";

            var serviceClient = new TableServiceClient(connectionString);
            _tableClient = serviceClient.GetTableClient(tableName);
        }

        public async Task<List<InvoiceData>> GetAllInvoicesAsync()
        {
            try
            {
                var invoices = new List<InvoiceData>();

                await foreach (var entity in _tableClient.QueryAsync<InvoiceData>())
                {
                    invoices.Add(entity);
                }

                return invoices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all invoices");
                return new List<InvoiceData>();
            }
        }

        public async Task<List<InvoiceData>> GetInvoicesByTransactionTypeAsync(string transactionType)
        {
            try
            {
                var invoices = new List<InvoiceData>();
                var filter = $"PartitionKey eq '{transactionType}'";

                await foreach (var entity in _tableClient.QueryAsync<InvoiceData>(filter))
                {
                    invoices.Add(entity);
                }

                return invoices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving invoices for transaction type: {transactionType}");
                return new List<InvoiceData>();
            }
        }

        public async Task<List<InvoiceData>> GetInvoicesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var invoices = new List<InvoiceData>();

                await foreach (var entity in _tableClient.QueryAsync<InvoiceData>())
                {
                    if (DateTime.TryParse(entity.InvoiceDate, out var invoiceDate) &&
                        invoiceDate >= startDate && invoiceDate <= endDate)
                    {
                        invoices.Add(entity);
                    }
                }

                return invoices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices by date range");
                return new List<InvoiceData>();
            }
        }

        public async Task<List<InvoiceData>> GetInvoicesByCustomerAsync(string customerName)
        {
            try
            {
                var invoices = new List<InvoiceData>();

                await foreach (var entity in _tableClient.QueryAsync<InvoiceData>())
                {
                    if (entity.CustomerName.Contains(customerName, StringComparison.OrdinalIgnoreCase))
                    {
                        invoices.Add(entity);
                    }
                }

                return invoices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving invoices for customer: {customerName}");
                return new List<InvoiceData>();
            }
        }
    }
}