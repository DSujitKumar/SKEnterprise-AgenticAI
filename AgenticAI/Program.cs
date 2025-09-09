using AgenticAI.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .AddSingleton<OpenAIService>()
    .AddSingleton<TableStorageService>()
    .AddSingleton<QueryAnalyzerService>()
    .AddSingleton<InvoiceAnalyticsService>()
    .AddLogging()
    .AddHttpClient();

builder.Build().Run();
