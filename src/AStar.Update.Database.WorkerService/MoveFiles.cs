using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AStar.FilesApi.Client.SDK.FilesApi;
using AStar.FilesApi.Client.SDK.Models;
using AStar.Infrastructure.Data;
using AStar.Update.Database.WorkerService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AStar.Update.Database.WorkerService;

[ExcludeFromCodeCoverage]
public class MoveFiles(IOptions<DirectoryChanges> directories, FilesApiClient filesApiClient, ILogger<UpdateDatabaseForAllFiles> logger) : BackgroundService
{
    private FilesContext Context
        => new(new DbContextOptionsBuilder<FilesContext>().UseSqlite(ServiceConstants.SqliteConnectionString).Options, new());

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MoveFiles started at: {RunTime}", DateTimeOffset.Now);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("MoveFiles started at: {RunTime}", DateTimeOffset.Now);
                var startTime = DateTime.UtcNow;
                var endTime = "3:00 AM";

                var duration = DateTime.Parse(endTime, CultureInfo.CurrentCulture).Subtract(startTime);
                if (duration < TimeSpan.Zero)
                {
                    duration = duration.Add(TimeSpan.FromHours(24));
                }

                logger.LogInformation("MoveFiles Waiting for: {RunTime} hours before updating the marked to move files .", duration);
                await Task.Delay(duration, stoppingToken);

                await MoveFilesToTheirNewHomeAsync();
                logger.LogInformation("MoveFiles Waiting for: 24 hours before updating the marked to move files again.");
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred in AStar.Update.Database.WorkerService: {ErrorMessage}", ex.Message);
            }
        }
    }

    private async Task MoveFilesToTheirNewHomeAsync()
    {
        foreach (var directory in directories.Value.Directories)
        {
            logger.LogInformation("Getting the files from the database that contain the {DirectoryName}.", directory);
            var filesToMove = Context.FileAccessDetails.Where(file => !file.SoftDeleted);

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