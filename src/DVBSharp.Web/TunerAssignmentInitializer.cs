using DVBSharp.Tuner;
using Microsoft.Extensions.Hosting;

namespace DVBSharp.Web;

public sealed class TunerAssignmentInitializer : IHostedService
{
    private readonly TunerAssignmentManager _assignments;
    private readonly TunerManager _tunerManager;
    private readonly ILogger<TunerAssignmentInitializer> _logger;

    public TunerAssignmentInitializer(TunerAssignmentManager assignments, TunerManager tunerManager, ILogger<TunerAssignmentInitializer> logger)
    {
        _assignments = assignments;
        _tunerManager = tunerManager;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var assignment in _assignments.GetAssignments())
        {
            var tuner = _tunerManager.GetTuner(assignment.TunerId);
            if (tuner == null)
            {
                _logger.LogWarning("Assignment found for tuner {TunerId}, but tuner is not registered", assignment.TunerId);
                continue;
            }

            try
            {
                await tuner.TuneAsync(assignment.Frequency);
                _logger.LogInformation("Pinned tuner {TunerId} to {Frequency} Hz on startup", assignment.TunerId, assignment.Frequency);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to tune tuner {TunerId} to {Frequency} Hz on startup", assignment.TunerId, assignment.Frequency);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
