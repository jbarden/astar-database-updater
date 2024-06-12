using System.Diagnostics.CodeAnalysis;
using AStar.Update.Database.WorkerService.Services;

namespace AStar.Update.Database.WorkerService;

[ExcludeFromCodeCoverage]
public class DeleteMarkedFiles(FilesService filesService, TimeProvider timeProvider, ILogger<UpdateDatabaseForAllFiles> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DeleteMarkedFiles started at: {RunTime} (Local Time)", timeProvider.GetLocalNow());
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await filesService.DeleteFilesMarkedForSoftDeletionAsync(stoppingToken);
                await filesService.DeleteFilesMarkedForHardDeletionAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred in AStar.Update.Database.WorkerService: {ErrorMessage}", ex.Message);
            }

            logger.LogInformation("Waiting for an hour before restarting at: {RunTime} (Local Time)", timeProvider.GetLocalNow().AddHours(1));
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}