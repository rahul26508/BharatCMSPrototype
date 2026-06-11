namespace BharatCMS.Infrastructure.Services;

using BharatCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using BharatCMS.Core.Context;
using System.Net.Http.Headers;
using System.Text.Json;

/// <summary>
/// Hosted service for cron-based API integrations.
/// Handles DigiLocker, GIS, etc. synchronization.
/// </summary>
public class ApiSyncScheduler : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApiSyncScheduler> _logger;
    private readonly IConfiguration _config;
    private static readonly HttpClient _httpClient = new();

    public ApiSyncScheduler(IServiceProvider serviceProvider, ILogger<ApiSyncScheduler> logger, IConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("API Sync Scheduler started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingSyncsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync scheduler error");
            }

            // Check every 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessPendingSyncsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BharatDbContext>();

        var integrations = await db.ApiIntegrations
            .Where(i => i.IsActive)
            .Where(i => i.CronSchedule != null)
            .ToListAsync(ct);

        foreach (var integration in integrations)
        {
            if (ShouldRunNow(integration.CronSchedule!))
            {
                await SyncIntegrationAsync(integration, db, ct);
            }
        }
    }

    private bool ShouldRunNow(string cronExpression)
    {
        // Simplified cron check - in production use NCrontab library
        return true; // Run on every check for demo
    }

    private async Task SyncIntegrationAsync(ApiIntegration integration, BharatDbContext db, CancellationToken ct)
    {
        _logger.LogInformation("Syncing {Provider} for tenant {TenantId}", integration.Provider, integration.TenantId);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, integration.Endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Add custom headers
            if (!string.IsNullOrEmpty(integration.CustomHeaders))
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(integration.CustomHeaders);
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            integration.LastSyncAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Sync completed for {Provider}", integration.Provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for {Provider}", integration.Provider);
        }
    }
}

/// <summary>
/// Background OCR processor for PDF documents.
/// </summary>
public class OcrProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OcrProcessor> _logger;

    public OcrProcessor(IServiceProvider serviceProvider, ILogger<OcrProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OCR Processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOcrAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR processor error");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessPendingOcrAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BharatDbContext>();

        var pendingDocs = await db.Documents
            .Where(d => d.ContentType == "application/pdf")
            .Where(d => string.IsNullOrEmpty(d.ExtractedText))
            .Take(10)
            .ToListAsync(ct);

        foreach (var doc in pendingDocs)
        {
            try
            {
                var filePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "storage",
                    doc.TenantId.ToString(),
                    doc.FileName);

                if (File.Exists(filePath))
                {
                    // Use PdfPig for text extraction
                    using var document = UglyToad.PdfPig.PdfDocument.Open(filePath);
                    var text = string.Join("\n", document.GetPages().Select(p => p.Text));

                    doc.ExtractedText = text.Length > 100000 ? text[..100000] : text;
                    await db.SaveChangesAsync(ct);

                    _logger.LogInformation("OCR completed for document {Id}", doc.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR failed for document {Id}", doc.Id);
            }
        }
    }
}
