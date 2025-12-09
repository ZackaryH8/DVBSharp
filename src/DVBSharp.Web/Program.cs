using System.Linq;
using System.Text.Json;
using DVBSharp.Core;
using DVBSharp.Geo;
using DVBSharp.Tuner;
using DVBSharp.Tuner.Emulation;
using DVBSharp.Tuner.Linux;
using DVBSharp.Tuner.Models;
using DVBSharp.Tuner.Transmitters;
using DVBSharp.Web;
using DVBSharp.Web.HdHomeRun;
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

builder.Services.AddSingleton<ITunerProvider, DvbTunerProvider>();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ITunerProvider, FakeCambridgeTunerProvider>();
}
builder.Services.AddSingleton<TunerManager>();
var dataRoot = Path.Combine(builder.Environment.ContentRootPath, "data");

builder.Services.AddSingleton<MuxManager>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var storagePath = Path.Combine(env.ContentRootPath, "data", "muxes.json");
    return new MuxManager(storagePath);
});

builder.Services.AddSingleton<FakeMuxScanner>();
builder.Services.AddSingleton(new TransmitterDatabase(
    Path.Combine(dataRoot, "uk-ofcom-tv-transmitting-stations.csv"),
    Path.Combine(dataRoot, "uk_transmitters.json")));
builder.Services.AddSingleton<PostcodeLookup>();
builder.Services.Configure<HdHomeRunOptions>(builder.Configuration.GetSection("HdHomeRun"));
builder.Services.AddSingleton<HdHomeRunLineupService>();
builder.Services.AddSingleton<HdHomeRunXmlTemplateProvider>();
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

app.MapGet("/api/stream/{id}", async (string id, HttpContext ctx, TunerManager manager, IOptions<StreamingOptions> streamingOptions, ILoggerFactory loggerFactory, TunerAssignmentManager assignmentManager, int? frequency) =>
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

    return await StreamFromTunerAsync(tuner, ctx, frequency, streamingOptions.Value, logger);
});

app.MapGet("/api/stream/any", async (HttpContext ctx, TunerManager manager, IOptions<StreamingOptions> streamingOptions, ILoggerFactory loggerFactory, TunerAssignmentManager assignmentManager, int frequency) =>
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

    return await StreamFromTunerAsync(tuner, ctx, frequency, streamingOptions.Value, logger);
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

app.MapGet("/discover.json", (HttpContext ctx, IOptions<HdHomeRunOptions> options, TunerManager manager) =>
{
    var opts = options.Value;
    var tuners = manager.GetTuners().ToList();
    var baseUrl = ResolveBaseUrl(ctx.Request);

    var payload = new
    {
        FriendlyName = opts.FriendlyName,
        Manufacturer = opts.Manufacturer,
        ManufacturerURL = opts.Manufacturer,
        ModelNumber = opts.ModelNumber,
        FirmwareName = opts.FirmwareName,
        FirmwareVersion = opts.FirmwareVersion,
        TunerCount = tuners.Count,
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

app.MapGet("/api/integrations/hdhomerun", (HttpContext ctx, IOptions<HdHomeRunOptions> options, TunerManager manager, HdHomeRunLineupService lineupService) =>
{
    var baseUrl = ResolveBaseUrl(ctx.Request);
    var tuners = manager.GetTuners().ToList();
    var tunerId = tuners.FirstOrDefault()?.Id;
    var lineup = lineupService.BuildLineup(baseUrl, tunerId);
    var opts = options.Value;

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
        tunerCount = tuners.Count,
        channelCount = lineup.Count,
        baseUrl,
        endpoints = new
        {
            discover = $"{baseUrl}/discover.json",
            status = $"{baseUrl}/lineup_status.json",
            lineup = $"{baseUrl}/lineup.json",
            lineupPost = $"{baseUrl}/lineup.post",
            streamAny = $"{baseUrl}/api/stream/any"
        },
        lineupPreview = lineup
            .Take(5)
            .Select(item => new
            {
                guideNumber = item.GuideNumber,
                guideName = item.GuideName,
                url = item.Url,
                callSign = item.CallSign,
                category = item.Category
            })
            .ToList()
    };

    return ApiResponse.Ok(payload).ToHttpResult();
});

app.MapGet("/api/transmitters", (TransmitterDatabase db, int skip = 0, int take = 25) =>
{
    skip = Math.Max(0, skip);
    take = Math.Clamp(take, 1, 100);
    var slice = db.Transmitters
        .Skip(skip)
        .Take(take)
        .ToList();
    return ApiResponse.Ok(new
    {
        Items = slice,
        Total = db.Transmitters.Count,
        Skip = skip,
        Take = slice.Count
    }).ToHttpResult();
});

app.MapPost("/api/transmitters/nearest", (NearestTransmitterRequest request, TransmitterDatabase db) =>
{
    if (request == null)
    {
        return ApiResponse<string>.Fail("Latitude and longitude are required").ToHttpResult();
    }

    var nearest = db.Nearest(request.Lat, request.Lon).ToList();
    var payload = nearest.Select(item => new
    {
        item.Tx.SiteName,
        item.Tx.Region,
        item.Tx.Postcode,
        item.Tx.IsRelay,
        item.Tx.Latitude,
        item.Tx.Longitude,
        item.Tx.Muxes,
        DistanceKm = Math.Round(item.DistanceKm, 2)
    });

    return ApiResponse.Ok(payload).ToHttpResult();
});

app.MapPost("/api/transmitters/from-postcode", async (PostcodeLookupRequest request, TransmitterDatabase db, PostcodeLookup lookup) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.Postcode))
    {
        return ApiResponse<string>.Fail("Postcode is required").ToHttpResult();
    }

    var coords = await lookup.LookupAsync(request.Postcode);
    if (coords == null)
    {
        return ApiResponse<string>.Fail("Postcode not found").ToHttpResult();
    }

    var (lat, lon) = coords.Value;
    var nearest = db.Nearest(lat, lon, 1).FirstOrDefault();
    if (nearest.Tx is null)
    {
        return ApiResponse<string>.Fail("No transmitters available").ToHttpResult();
    }

    var response = new
    {
        postcode = request.Postcode.Trim(),
        lat,
        lon,
        transmitter = nearest.Tx.SiteName,
        distanceKm = Math.Round(nearest.DistanceKm, 2)
    };

    return ApiResponse.Ok(response).ToHttpResult();
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

static string ResolveBaseUrl(HttpRequest request)
{
    var host = request.Host.HasValue ? request.Host.Value : "localhost";
    return $"{request.Scheme}://{host}";
}

static async Task<IResult> StreamFromTunerAsync(ITuner tuner, HttpContext ctx, int? frequency, StreamingOptions streamingOptions, ILogger logger)
{
    if (!string.IsNullOrWhiteSpace(streamingOptions.TestTransportPath))
    {
        var overridePath = ResolveStreamingPath(streamingOptions.TestTransportPath);
        if (File.Exists(overridePath))
        {
            logger.LogInformation("Streaming override active for tuner {TunerId} using {Path}", tuner.Id, overridePath);
            await StreamFromFileAsync(overridePath, ctx.RequestAborted, ctx.Response, logger);
            return Results.Empty;
        }

        logger.LogWarning("Streaming override path {Path} not found; falling back to tuner {TunerId}", overridePath, tuner.Id);
    }

    if (frequency.HasValue)
    {
        logger.LogInformation("Tuning tuner {TunerId} to {Frequency} Hz", tuner.Id, frequency);
        await tuner.TuneAsync(frequency.Value);
    }

    ctx.Response.StatusCode = 200;
    ctx.Response.ContentType = "video/mp2t";
    logger.LogInformation("Starting live stream from tuner {TunerId}", tuner.Id);
    await foreach (var packet in tuner.ReadStreamAsync(ctx.RequestAborted))
    {
        await ctx.Response.Body.WriteAsync(packet, ctx.RequestAborted);
        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
    }

    logger.LogInformation("Stream ended for tuner {TunerId}", tuner.Id);
    return Results.Empty;
}

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

static string ResolveStreamingPath(string path)
{
    if (Path.IsPathRooted(path))
    {
        return path;
    }

    return Path.GetFullPath(path, AppContext.BaseDirectory);
}

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

app.Run();
