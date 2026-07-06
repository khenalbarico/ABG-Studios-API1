using Abg.Data.Paymongo;
using Abg.Data.Tables;
using Azure.Data.Tables;
using Azure.Monitor.OpenTelemetry.Exporter;
using FunctionApp1.RateLimiting;
using FunctionApp1.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

var configuration = builder.Configuration;

builder.Services.AddSingleton(new TableServiceClient(
    configuration["TablesConnectionString"] ?? "UseDevelopmentStorage=true"));

builder.Services.AddSingleton(new PaymongoOptions
{
    SecretKey        = configuration["Paymongo:SecretKey"] ?? "",
    BaseUrl          = configuration["Paymongo:BaseUrl"] ?? "https://api.paymongo.com/v1/",
    WebhookSecretKey = configuration["Paymongo:WebhookSecretKey"] ?? ""
});

builder.Services.AddSingleton<IServiceStore, ServiceTableStore>();
builder.Services.AddSingleton<IScheduleConfigStore, ScheduleConfigTableStore>();
builder.Services.AddSingleton<IBookingStore, BookingTableStore>();
builder.Services.AddSingleton<IAppointmentStore, AppointmentTableStore>();
builder.Services.AddSingleton<IPurchaseStore, PurchaseTableStore>();
builder.Services.AddSingleton<IUserStore, UserTableStore>();
builder.Services.AddSingleton<IBookingHoldStore, BookingHoldTableStore>();

builder.Services.AddHttpClient<IPaymongoClient, PaymongoQrphClient>((provider, client) =>
    PaymongoQrphClient.ConfigureHttpClient(client, provider.GetRequiredService<PaymongoOptions>()));

builder.Services.AddSingleton<IRateLimiter, FixedWindowRateLimiter>();
builder.Services.AddSingleton<BookingCoordinator>();
builder.Services.AddSingleton<PaymentCoordinator>();
builder.Services.AddSingleton<PaymongoWebhookProcessor>();

builder.Build().Run();
