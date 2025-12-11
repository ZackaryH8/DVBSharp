using System.Linq;
using System.Text.Json;
using DVBSharp.Core;
using DVBSharp.Tuner;
using Microsoft.AspNetCore.Mvc;
using DVBSharp.Tuner.Emulation;
using DVBSharp.Tuner.Linux;
using DVBSharp.Tuner.Models;
using DVBSharp.Web;
using DVBSharp.Web.HdHomeRun;
using DVBSharp.Web.PredefinedMuxes;
using DVBSharp.Web.Requests;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var useFakeTuners = builder.Configuration.GetSection("Tuners").GetValue<bool?>("UseFakeProvider");
if (useFakeTuners == true)
{
    builder.Services.AddSingleton<ITunerProvider, FakeCambridgeTunerProvider>();
}
else if (useFakeTuners == false)
{
    builder.Services.AddSingleton<ITunerProvider, DvbTunerProvider>();
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ITunerProvider, FakeCambridgeTunerProvider>();
}
else
{
    builder.Services.AddSingleton<ITunerProvider, DvbTunerProvider>();
}
builder.Services.AddSingleton<TunerManager>();
builder.Services.AddSingleton<ActiveStreamManager>();

builder.Services.AddSingleton<MuxManager>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var storagePath = Path.Combine(env.ContentRootPath, "data", "muxes.json");
    return new MuxManager(storagePath);
});

builder.Services.AddSingleton<FakeMuxScanner>();
builder.Services.Configure<HdHomeRunOptions>(builder.Configuration.GetSection("HdHomeRun"));
builder.Services.AddSingleton<HdHomeRunLineupService>();
builder.Services.AddSingleton<HdHomeRunXmlTemplateProvider>();
builder.Services.AddSingleton<HdHomeRunSettingsStore>();
builder.Services.AddSingleton<PredefinedMuxRepository>();
builder.Services.AddSingleton<TunerAssignmentManager>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var storagePath = Path.Combine(env.ContentRootPath, "data", "tuner_assignments.json");
    return new TunerAssignmentManager(storagePath);
});
builder.Services.AddHostedService<TunerAssignmentInitializer>();
builder.Services.Configure<StreamingOptions>(builder.Configuration.GetSection("Streaming"));
builder.Services.PostConfigure<StreamingOptions>(options =>
{
    if (!string.IsNullOrWhiteSpace(options.TestTransportPath)) return;
    var defaultPath = Path.Combine(builder.Environment.ContentRootPath, "test.ts");
    if (File.Exists(defaultPath))
    {
        options.TestTransportPath = defaultPath;
    }
});
var hdHomeRunJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNamingPolicy = null
};

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

app.MapGet("/api/dvb/adapters", () =>
{
    return ApiResponse.Ok(DvbDeviceLocator.GetAdapters()).ToHttpResult();
});

app.MapGet("/api/tuners", async (TunerManager manager) =>
{
    var snapshots = await manager.GetTunersWithStatusAsync();
    var result = snapshots.Select(snapshot => new
    {
        snapshot.Info.Id,
        snapshot.Info.Name,
        snapshot.Info.Type,
        snapshot.Info.Capabilities,
        snapshot.Info.Description,
        Status = snapshot.Status
    });

    Console.WriteLine($"[DEBUG] Returning {snapshots.Count} tuners");

    return ApiResponse.Ok(result).ToHttpResult();
});

app.MapGet("/api/tuners/{id}", async (string id, TunerManager manager) =>
{
    var tuner = manager.GetTuner(id);
    if (tuner is null)
    {
        return ApiResponse<string>.Fail("Tuner not found").ToHttpResult();
    }

    var status = await tuner.GetStatusAsync();
    return ApiResponse.Ok(new { tuner.Info, Status = status }).ToHttpResult();
});

app.MapGet("/api/stream/{id}", async (string id, HttpContext ctx, TunerManager manager, IOptions<StreamingOptions> streamingOptions, ILoggerFactory loggerFactory, TunerAssignmentManager assignmentManager, [FromServices] ActiveStreamManager streamManager, int? frequency) =>
{
    var tuner = manager.GetTuner(id);
    if (tuner is null)
    {
        return Results.NotFound(ApiResponse<string>.Fail("Tuner not found"));
    }

    var logger = loggerFactory.CreateLogger("Streaming");
    logger.LogDebug("Streaming request for tuner {TunerId} at frequency {Frequency} Hz", tuner.Id, frequency);

    var assignment = assignmentManager.GetAssignment(tuner.Id);
    if (assignment != null)
    {
        if (frequency.HasValue && frequency.Value != assignment.Frequency)
        {
            return ApiResponse<string>.Fail($"Tuner {tuner.Id} is pinned to {assignment.Frequency} Hz").ToHttpResult();
        }

        frequency = assignment.Frequency;
    }

    return await StreamFromTunerAsync(tuner, ctx, frequency, streamingOptions.Value, logger, streamManager, assignment?.Label);
});

app.MapGet("/api/stream/any", async (HttpContext ctx, TunerManager manager, IOptions<StreamingOptions> streamingOptions, ILoggerFactory loggerFactory, TunerAssignmentManager assignmentManager, [FromServices] ActiveStreamManager streamManager, int frequency) =>
{
    if (frequency <= 0)
    {
        return ApiResponse<string>.Fail("Frequency must be provided").ToHttpResult();
    }

    var tuner = SelectTunerForFrequency(manager, assignmentManager, frequency);
    if (tuner is null)
    {
        return ApiResponse<string>.Fail("No tuners available for requested frequency").ToHttpResult();
    }

    var logger = loggerFactory.CreateLogger("Streaming");
    logger.LogDebug("Streaming request via /any using tuner {TunerId} at frequency {Frequency} Hz", tuner.Id, frequency);

    return await StreamFromTunerAsync(tuner, ctx, frequency, streamingOptions.Value, logger, streamManager);
});

app.MapGet("/api/muxes", (MuxManager muxManager) =>
{
    return ApiResponse.Ok(muxManager.GetMuxes()).ToHttpResult();
});

app.MapGet("/api/muxes/{id}", (string id, MuxManager muxManager) =>
{
    var mux = muxManager.GetMux(id);
    return mux is null
        ? ApiResponse<Mux>.Fail("Mux not found").ToHttpResult()
        : ApiResponse.Ok(mux).ToHttpResult();
});

app.MapGet("/api/muxes/predefined", (PredefinedMuxRepository repository) =>
{
    return ApiResponse.Ok(repository.GetLocations()).ToHttpResult();
});

app.MapGet("/api/muxes/predefined/{id}", (string id, PredefinedMuxRepository repository) =>
{
    var location = repository.GetLocation(id);
    return location is null
        ? ApiResponse<PredefinedMuxLocation>.Fail("Predefined mux location not found").ToHttpResult()
        : ApiResponse.Ok(location).ToHttpResult();
});

app.MapPost("/api/muxes/scan", async (MuxScanRequest request, FakeMuxScanner scanner) =>
{
    if (request == null || request.Frequency <= 0)
    {
        return ApiResponse<string>.Fail("Frequency must be provided").ToHttpResult();
    }

    try
    {
        var mux = await scanner.ScanAsync(request.Frequency);
        return ApiResponse.Ok(mux).ToHttpResult();
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return ApiResponse<string>.Fail(ex.Message).ToHttpResult();
    }
});

app.MapGet("/api/channels", (MuxManager muxManager) =>
{
    var channels = muxManager.GetChannels().Select(channel => new
    {
        MuxId = channel.mux.Id,
        channel.mux.Frequency,
        channel.service.ServiceId,
        channel.service.Name,
        channel.service.PmtPid,
        channel.service.AudioPids,
        channel.service.VideoPids,
        channel.service.Streams,
        channel.service.LogicalChannelNumber,
        channel.service.CallSign,
        channel.service.Category
    });

    return ApiResponse.Ok(channels).ToHttpResult();
});

app.MapGet("/api/channels/summary", (MuxManager muxManager) =>
{
    var muxes = muxManager.GetMuxes().ToList();
    var channels = muxManager.GetChannels().ToList();
    var totalChannels = channels.Count;
    var channelsWithLcn = channels.Count(item => item.service.LogicalChannelNumber.HasValue);
    var lastUpdated = muxes.Count > 0
        ? muxes.Max(m => m.LastUpdated).ToString("O")
        : null;

    var categories = channels
        .GroupBy(item => string.IsNullOrWhiteSpace(item.service.Category) ? "Uncategorised" : item.service.Category!.Trim())
        .Select(group => new
        {
            category = group.Key,
            count = group.Count()
        })
        .OrderByDescending(group => group.count)
        .ToList();

    var payload = new
    {
        totalChannels,
        muxCount = muxes.Count,
        channelsWithLcn,
        logicalChannelCoverage = totalChannels == 0 ? 0 : (double)channelsWithLcn / totalChannels,
        lastUpdated,
        categories
    };

    return ApiResponse.Ok(payload).ToHttpResult();
});

app.MapGet("/device.xml", (HttpContext ctx, IOptions<HdHomeRunOptions> options, HdHomeRunXmlTemplateProvider templates) =>
{
    var baseUrl = ResolveBaseUrl(ctx.Request);
    var xml = templates.GetDeviceXml(options.Value, baseUrl);
    return Results.Content(xml, "application/xml");
});

app.MapGet("/ConnectionManager.xml", (HdHomeRunXmlTemplateProvider templates) =>
    Results.Content(templates.GetConnectionManagerXml(), "application/xml"));

app.MapGet("/ContentDirectory.xml", (HdHomeRunXmlTemplateProvider templates) =>
    Results.Content(templates.GetContentDirectoryXml(), "application/xml"));

app.MapGet("/discover.json", (HttpContext ctx, IOptions<HdHomeRunOptions> options, TunerManager manager, HdHomeRunSettingsStore settings) =>
{
    var opts = options.Value;
    var tuners = manager.GetTuners().ToList();
    var baseUrl = ResolveBaseUrl(ctx.Request);
    var advertisedTuners = settings.ApplyLimit(tuners.Count);

    var payload = new
    {
        FriendlyName = opts.FriendlyName,
        Manufacturer = opts.Manufacturer,
        ManufacturerURL = opts.Manufacturer,
        ModelNumber = opts.ModelNumber,
        FirmwareName = opts.FirmwareName,
        FirmwareVersion = opts.FirmwareVersion,
        TunerCount = advertisedTuners,
        DeviceID = opts.DeviceId,
        DeviceAuth = opts.DeviceAuth,
        BaseURL = baseUrl,
        LineupURL = $"{baseUrl}/lineup.json"
    };

    return Results.Json(payload, hdHomeRunJsonOptions);
});

app.MapGet("/lineup_status.json", (MuxManager muxManager, IOptions<HdHomeRunOptions> options) =>
{
    var muxes = muxManager.GetMuxes().ToList();
    var channelCount = muxes.Sum(m => m.Services.Count);
    DateTimeOffset? lastUpdated = muxes.Count > 0
        ? muxes.Max(m => m.LastUpdated)
        : null;
    var opts = options.Value;

    var payload = new
    {
        ScanInProgress = 0,
        ScanPossible = muxes.Count > 0 ? 1 : 0,
        Source = opts.SourceType,
        SourceList = new[] { opts.SourceType },
        MuxCount = muxes.Count,
        ChannelCount = channelCount,
        LastScanTime = lastUpdated?.ToUnixTimeSeconds(),
        LastUpdated = lastUpdated?.ToString("O")
    };

    return Results.Json(payload, hdHomeRunJsonOptions);
});

app.MapGet("/lineup.json", (HttpContext ctx, HdHomeRunLineupService lineupService, TunerManager manager) =>
{
    var baseUrl = ResolveBaseUrl(ctx.Request);
    var tunerId = manager.GetTuners().FirstOrDefault()?.Id;
    var lineup = lineupService.BuildLineup(baseUrl, tunerId);
    return Results.Json(lineup, hdHomeRunJsonOptions);
});

app.MapPost("/lineup.post", (HttpContext ctx, ILogger<HdHomeRunLineupService> logger) =>
{
    if (ctx.Request.Query.TryGetValue("channel", out var values))
    {
        foreach (var value in values)
        {
            logger.LogInformation("Received HDHomeRun lineup request for {Channel}", value);
        }
    }

    return Results.Json(new { Result = "success" }, hdHomeRunJsonOptions);
});

app.MapGet("/api/integrations/hdhomerun", (HttpContext ctx, IOptions<HdHomeRunOptions> options, TunerManager manager, HdHomeRunLineupService lineupService, HdHomeRunSettingsStore settingsStore) =>
{
    var baseUrl = ResolveBaseUrl(ctx.Request);
    var tuners = manager.GetTuners().ToList();
    var tunerId = tuners.FirstOrDefault()?.Id;
    var lineup = lineupService.BuildLineup(baseUrl, tunerId);
    var opts = options.Value;
    var settings = settingsStore.GetSettings();
    var advertisedTuners = settingsStore.ApplyLimit(tuners.Count);

    var payload = new
    {
        friendlyName = opts.FriendlyName,
        deviceId = opts.DeviceId,
        deviceAuth = opts.DeviceAuth,
        manufacturer = opts.Manufacturer,
        modelNumber = opts.ModelNumber,
        firmwareName = opts.FirmwareName,
        firmwareVersion = opts.FirmwareVersion,
        sourceType = opts.SourceType,
        tunerCount = advertisedTuners,
        physicalTuners = tuners.Count,
        tunerLimit = settings.TunerLimit,
        channelCount = lineup.Count,
        baseUrl,
        endpoints = new
        {
            discover = $"{baseUrl}/discover.json",
            status = $"{baseUrl}/lineup_status.json",
            lineup = $"{baseUrl}/lineup.json",
            lineupPost = $"{baseUrl}/lineup.post",
            streamAny = $"{baseUrl}/api/stream/any"
        }
    };

    return ApiResponse.Ok(payload).ToHttpResult();
});

app.MapGet("/api/integrations/hdhomerun/settings", (HdHomeRunSettingsStore settings) =>
{
    return ApiResponse.Ok(settings.GetSettings()).ToHttpResult();
});

app.MapPost("/api/integrations/hdhomerun/settings", async (UpdateHdHomeRunSettingsRequest request, HdHomeRunSettingsStore settings) =>
{
    if (request is null)
    {
        return ApiResponse<string>.Fail("Payload is required").ToHttpResult();
    }

    var updated = await settings.UpdateAsync(request.TunerLimit);
    return ApiResponse.Ok(updated).ToHttpResult();
});

app.MapGet("/api/tuner-assignments", (TunerAssignmentManager assignments) =>
{
    return ApiResponse.Ok(assignments.GetAssignments()).ToHttpResult();
});

app.MapGet("/api/tuner-assignments/{tunerId}", (string tunerId, TunerAssignmentManager assignments) =>
{
    var assignment = assignments.GetAssignment(tunerId);
    return assignment == null
        ? ApiResponse<TunerAssignment>.Fail("Assignment not found").ToHttpResult()
        : ApiResponse.Ok(assignment).ToHttpResult();
});

app.MapPost("/api/tuner-assignments", async (PinTunerRequest request, TunerAssignmentManager assignments, TunerManager tunerManager, ILogger<TunerAssignmentManager> logger) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.TunerId) || request.Frequency <= 0)
    {
        return ApiResponse<string>.Fail("TunerId and frequency are required").ToHttpResult();
    }

    var tuner = tunerManager.GetTuner(request.TunerId);
    if (tuner == null)
    {
        return ApiResponse<string>.Fail("Tuner not found").ToHttpResult();
    }

    var assignment = await assignments.SetAssignmentAsync(request.TunerId, request.Frequency, request.Label);
    try
    {
        await tuner.TuneAsync(assignment.Frequency);
        logger.LogInformation("Pinned tuner {TunerId} to {Frequency} Hz", assignment.TunerId, assignment.Frequency);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to tune tuner {TunerId} after assignment", assignment.TunerId);
    }

    return ApiResponse.Ok(assignment).ToHttpResult();
});

app.MapDelete("/api/tuner-assignments/{tunerId}", async (string tunerId, TunerAssignmentManager assignments) =>
{
    var removed = await assignments.RemoveAssignmentAsync(tunerId);
    return removed
        ? ApiResponse.Ok(new { tunerId }).ToHttpResult()
        : ApiResponse<string>.Fail("Assignment not found").ToHttpResult();
});

app.MapGet("/api/streams/active", ([FromServices] ActiveStreamManager streams) =>
{
    return ApiResponse.Ok(streams.GetActive()).ToHttpResult();
});

/// <summary>
/// Builds a base URL for HDHomeRun integrations even when proxied.
/// </summary>
static string ResolveBaseUrl(HttpRequest request)
{
    var host = request.Host.HasValue ? request.Host.Value : "localhost";
    return $"{request.Scheme}://{host}";
}

/// <summary>
/// Streams transport data for a tuner or from the configured override file while tracking active streams.
/// </summary>
static async Task<IResult> StreamFromTunerAsync(ITuner tuner, HttpContext ctx, int? frequency, StreamingOptions streamingOptions, ILogger logger, ActiveStreamManager streamManager, string? muxLabel = null)
{
    if (await TryStreamOverrideAsync(ctx, streamingOptions, logger, streamManager, tuner, frequency, muxLabel))
    {
        return Results.Empty;
    }

    if (frequency.HasValue)
    {
        logger.LogInformation("Tuning tuner {TunerId} to {Frequency} Hz", tuner.Id, frequency);
        await tuner.TuneAsync(frequency.Value);
    }

    ctx.Response.StatusCode = 200;
    ctx.Response.ContentType = "video/mp2t";
    var clientAddress = ctx.Connection.RemoteIpAddress?.ToString();
    logger.LogInformation("Starting live stream from tuner {TunerId} for {Client}", tuner.Id, clientAddress ?? "unknown client");
    var stream = streamManager.Start(tuner.Id, frequency, muxLabel, clientAddress);
    try
    {
        await foreach (var packet in tuner.ReadStreamAsync(ctx.RequestAborted))
        {
            await ctx.Response.Body.WriteAsync(packet, ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        }
    }
    finally
    {
        streamManager.End(stream.Id);
        logger.LogInformation("Stream ended for tuner {TunerId}", tuner.Id);
    }

    return Results.Empty;
}

/// <summary>
/// Streams the specified transport file on loop until the caller cancels the request.
/// </summary>
static async Task StreamFromFileAsync(string path, CancellationToken token, HttpResponse response, ILogger logger)
{
    response.StatusCode = 200;
    response.ContentType = "video/mp2t";
    var buffer = new byte[188 * 256];
    logger.LogInformation("Streaming file {Path}", path);
    while (!token.IsCancellationRequested)
    {
        await using var file = File.OpenRead(path);
        int read;
        while ((read = await file.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
        {
            await response.Body.WriteAsync(buffer.AsMemory(0, read), token);
        }
    }

    logger.LogInformation("File stream for {Path} cancelled", path);
}

/// Resolves relative paths for streaming assets into absolute paths rooted in the app directory.
/// </summary>
static string ResolveStreamingPath(string path)
{
    if (Path.IsPathRooted(path))
    {
        return path;
    }

    return Path.GetFullPath(path, AppContext.BaseDirectory);
}

/// <summary>
/// Picks a tuner capable of handling the requested frequency while respecting pinned assignments.
/// </summary>
static ITuner? SelectTunerForFrequency(TunerManager manager, TunerAssignmentManager assignments, int frequency)
{
    var pinned = assignments.GetAssignments().FirstOrDefault(a => a.Frequency == frequency);
    if (pinned != null)
    {
        var tuner = manager.GetTuner(pinned.TunerId);
        if (tuner != null)
        {
            return tuner;
        }
    }

    foreach (var tunerInfo in manager.GetTuners())
    {
        var tuner = manager.GetTuner(tunerInfo.Id);
        if (tuner == null) continue;
        var assignment = assignments.GetAssignment(tunerInfo.Id);
        if (assignment == null || assignment.Frequency == frequency)
        {
            return tuner;
        }
    }

    return null;
}

/// <summary>
/// Attempts to serve the request using a configured transport stream file instead of a live tuner.
/// </summary>
static async Task<bool> TryStreamOverrideAsync(HttpContext ctx, StreamingOptions streamingOptions, ILogger logger, ActiveStreamManager streamManager, ITuner tuner, int? frequency, string? muxLabel)
{
    if (string.IsNullOrWhiteSpace(streamingOptions.TestTransportPath))
    {
        return false;
    }

    var overridePath = ResolveStreamingPath(streamingOptions.TestTransportPath);
    if (!File.Exists(overridePath))
    {
        logger.LogWarning("Streaming override path {Path} not found; falling back to tuner {TunerId}", overridePath, tuner.Id);
        return false;
    }

    logger.LogInformation("Streaming override active for tuner {TunerId} using {Path}", tuner.Id, overridePath);
    var record = streamManager.Start(tuner.Id, frequency, muxLabel, ctx.Connection.RemoteIpAddress?.ToString());
    try
    {
        await StreamFromFileAsync(overridePath, ctx.RequestAborted, ctx.Response, logger);
    }
    finally
    {
        streamManager.End(record.Id);
    }

    return true;
}

app.Run();
