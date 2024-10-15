using System.Diagnostics.CodeAnalysis;
using AStar.FilesApi.Client.SDK.FilesApi;
using AStar.FilesApi.Client.SDK.Models;
using AStar.Infrastructure.Data;
using AStar.Update.Database.WorkerService.Models;
using AStar.Update.Database.WorkerService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AStar.Update.Database.WorkerService;

[ExcludeFromCodeCoverage]
public class MoveFiles(FilesContext context, IOptions<DirectoryChanges> directories, FilesApiClient filesApiClient, ILogger<UpdateDatabaseForAllFiles> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MoveFiles started at: {RunTime}", DateTimeOffset.Now);
        while (!stoppingToken.IsCancellationRequested)
        {
            const string endTime = "3:00 AM";
            try
            {
                logger.LogInformation("MoveFiles started at: {RunTime}", DateTimeOffset.Now);
                var intialRunDelay = TimeDelay.CalculateDelayToNextRun(endTime);

                logger.LogInformation("MoveFiles Waiting for: {DelayToNextRun} hours before updating the marked to move files .", intialRunDelay);
                await Task.Delay(intialRunDelay, stoppingToken);

                await MoveFilesToTheirNewHomeAsync();
                var delayToNextRun = TimeDelay.CalculateDelayToNextRun(endTime);
                logger.LogInformation("MoveFiles Waiting for: {DelayToNextRun} hours before updating the marked to move files again.", delayToNextRun);
                await Task.Delay(delayToNextRun, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred in AStar.Update.Database.WorkerService: {ErrorMessage}", ex.Message);
            }
        }
    }

    private async Task MoveFilesToTheirNewHomeAsync()
    {
        if (UpdateDatabaseForAllFiles.GlobalUpdateIsRunning)
        {
            return;
        }

        foreach (var directory in directories.Value.Directories)
        {
            logger.LogInformation("Getting the files from the database that contain the {DirectoryName}.", directory);
            var filesToMove = context.Files.Include(x => x.FileAccessDetail).Where(file => !file.FileAccessDetail.SoftDeleted && file.DirectoryName.Contains(directory.Old));

            foreach (var fileToMove in filesToMove)
            {
                var file = await filesApiClient.GetFileDetail(fileToMove.Id);
                var fullNameWithPath = file.FullName;
                var newLocation = file.DirectoryName.Replace(directory.Old, directory.New);

                try
                {
                    _ = await filesApiClient.UpdateFileAsync(new DirectoryChangeRequest() { OldDirectoryName = file.DirectoryName, NewDirectoryName = newLocation, FileName = file.Name });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error: {Error} occurred whilst updating the directory for {FileName}", fullNameWithPath, ex.Message);
                }
            }
        }
    }
}