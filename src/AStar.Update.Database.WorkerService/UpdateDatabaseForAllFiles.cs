using System.Diagnostics.CodeAnalysis;
using AStar.Update.Database.WorkerService.Models;
using AStar.Update.Database.WorkerService.Services;
using Microsoft.Extensions.Options;

namespace AStar.Update.Database.WorkerService;

[ExcludeFromCodeCoverage]
public class UpdateDatabaseForAllFiles(FilesService filesService, IOptions<ApiConfiguration> directories, ILogger<UpdateDatabaseForAllFiles> logger) : BackgroundService
{
    public static bool GlobalUpdateIsRunning { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        const string endTime = "5:00 AM";
        logger.LogInformation("UpdateDatabaseForAllFiles started at: {RunTime}", DateTimeOffset.Now);
        var intialRunDelay = TimeDelay.CalculateDelayToNextRun(endTime);

        logger.LogInformation("Waiting for: {DelayToNextRun} hours before updating the full database.", intialRunDelay);
        // await Task.Delay(intialRunDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                    GlobalUpdateIsRunning = true;
                    var files = GetFiles(directories);

                    await filesService.ProcessNewFiles(files, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred in AStar.Update.Database.WorkerService: {ErrorMessage}", ex.Message);
            }

            var delayToNextRun = TimeDelay.CalculateDelayToNextRun(endTime);
            logger.LogInformation("Waiting for: {DelayToNextRun} hours before updating the full database again.", delayToNextRun);
            GlobalUpdateIsRunning = false;
            await Task.Delay(delayToNextRun, stoppingToken);
        }
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