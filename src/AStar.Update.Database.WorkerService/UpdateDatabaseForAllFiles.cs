using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AStar.FilesApi.Client.SDK.FilesApi;
using AStar.Update.Database.WorkerService.Models;
using AStar.Update.Database.WorkerService.Services;
using Microsoft.Extensions.Options;

namespace AStar.Update.Database.WorkerService;

[ExcludeFromCodeCoverage]
public class UpdateDatabaseForAllFiles(FilesService filesService, IOptions<ApiConfiguration> directories, ILogger<UpdateDatabaseForAllFiles> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("UpdateDatabaseForAllFiles started at: {RunTime}", DateTimeOffset.Now);
            var duration = CalculateDelayToNextRun();

            logger.LogInformation("Waiting for: {DelayHours} hours before updating the full database.", duration);
            await Task.Delay(duration, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var files = GetFiles(directories);

                await UpdateDirectoryFiles(files, stoppingToken);
                var durationToNextRun = CalculateDelayToNextRun();
                logger.LogInformation("Waiting for: {DelayHours} hours before updating the full database again.", durationToNextRun);
                await Task.Delay(durationToNextRun, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred in AStar.Update.Database.WorkerService: {ErrorMessage}", ex.Message);
        }
    }

    private static TimeSpan CalculateDelayToNextRun()
    {
        var startTime = DateTime.UtcNow;
        var endTime = "5:00 AM";

        var duration = DateTime.Parse(endTime, CultureInfo.CurrentCulture).Subtract(startTime);
        if (duration < TimeSpan.Zero)
        {
            duration = duration.Add(TimeSpan.FromHours(24));
        }

        return duration;
    }

    private async Task UpdateDirectoryFiles(IEnumerable<string> files, CancellationToken stoppingToken)
    {
        await filesService.ProcessNewFiles(files, stoppingToken);
        await filesService.ProcessMovedFiles(files, directories.Value.Directories, stoppingToken);

        await filesService.RemoveFilesFromDbThatDoNotExistAnyMore(files, stoppingToken);
    }

    private List<string> GetFiles(IOptions<ApiConfiguration> directories)
    {
        var files = new List<string>();

        foreach (var dir in directories.Value.Directories)
        {
            files.AddRange(filesService.GetFilesFromDirectory(dir));
        }

        return files;
    }
}