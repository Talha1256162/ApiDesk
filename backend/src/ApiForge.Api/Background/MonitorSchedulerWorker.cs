using System.Diagnostics;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Requests;

namespace ApiForge.Api.Background;

public sealed class MonitorSchedulerWorker(IServiceScopeFactory scopeFactory, ILogger<MonitorSchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunDueMonitorsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Monitor scheduler failed.");
            }
        }
    }

    private async Task RunDueMonitorsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var productOpsRepository = scope.ServiceProvider.GetRequiredService<IProductOpsRepository>();
        var collectionRepository = scope.ServiceProvider.GetRequiredService<ICollectionRepository>();
        var environmentRepository = scope.ServiceProvider.GetRequiredService<IEnvironmentRepository>();
        var requestRunRepository = scope.ServiceProvider.GetRequiredService<IRequestRunRepository>();
        var httpRequestExecutor = scope.ServiceProvider.GetRequiredService<IHttpRequestExecutor>();

        var monitors = await productOpsRepository.GetEnabledMonitorsAsync(cancellationToken);
        foreach (var monitor in monitors)
        {
            var runs = await productOpsRepository.GetMonitorRunsAsync(monitor.Id, 1, cancellationToken);
            if (runs.Count > 0 && DateTime.UtcNow - runs[0].CreatedOn < ScheduleInterval(monitor.ScheduleExpression))
            {
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            var passed = 0;
            var failed = 0;
            var requests = await collectionRepository.GetCollectionRequestsAsync(monitor.CollectionId, cancellationToken);
            foreach (var item in requests)
            {
                var request = await collectionRepository.GetRequestAsync(item.Id, cancellationToken);
                if (request is null)
                {
                    failed++;
                    continue;
                }

                var runId = await requestRunRepository.CreateRunAsync(request.Id, request.WorkspaceId, monitor.EnvironmentId, monitor.CreatedBy, DateTime.UtcNow, cancellationToken);
                try
                {
                    var variables = await environmentRepository.ResolveVariablesAsync(request.WorkspaceId, request.CollectionId, monitor.EnvironmentId, monitor.CreatedBy, cancellationToken);
                    var response = await httpRequestExecutor.ExecuteAsync(runId, request, variables, cancellationToken);
                    await requestRunRepository.CompleteRunAsync(runId, response, cancellationToken);
                    if (response.Succeeded)
                    {
                        passed++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    await requestRunRepository.FailRunAsync(runId, ex.Message, stopwatch.ElapsedMilliseconds, DateTime.UtcNow, cancellationToken);
                }
            }

            stopwatch.Stop();
            await productOpsRepository.AddMonitorRunAsync(monitor.Id, failed == 0 ? "Passed" : "Failed", passed, failed, stopwatch.ElapsedMilliseconds, monitor.CreatedBy, cancellationToken);
        }
    }

    private static TimeSpan ScheduleInterval(string expression)
    {
        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var number = parts.FirstOrDefault(part => int.TryParse(part, out _));
        var value = number is null ? 15 : Math.Clamp(int.Parse(number), 1, 10080);
        if (expression.Contains("day", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromDays(value);
        }

        if (expression.Contains("hour", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromHours(value);
        }

        return TimeSpan.FromMinutes(value);
    }
}
