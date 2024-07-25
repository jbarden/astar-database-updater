using System.IO.Abstractions;
using AStar.Infrastructure.Data;
using AStar.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace AStar.Update.Database.WorkerService.Services;

public class FilesService(FilesContext context, IFileSystem fileSystem, ILogger<FilesService> logger)
{
    public async Task DeleteFilesMarkedForSoftDeletionAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting removal of files marked for soft deletion");
        var fileAccessDetails = await context.FileAccessDetails.Where(fileAccess => fileAccess.SoftDeletePending).ToListAsync(cancellationToken);
        logger.LogInformation("There are {Files} files marked for soft deletion", fileAccessDetails.Count);

        foreach (var fileAccessDetail in fileAccessDetails)
        {
            var fileDetail = await context.Files.SingleAsync(file => file.FileAccessDetail.Id == fileAccessDetail.Id, cancellationToken);
            logger.LogInformation("Soft-deleting file: {FileName} from {DirectoryName}", fileDetail.FileName, fileDetail.DirectoryName);
            DeleteFileIfItExists(fileSystem, fileDetail);

            fileAccessDetail.SoftDeletePending = false;
            fileAccessDetail.SoftDeleted = true;
        }

        await SaveChangesSafely(cancellationToken);
        logger.LogInformation("Completed removal of files marked for soft deletion");
    }

    public async Task DeleteFilesMarkedForHardDeletionAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting removal of files marked for hard deletion");
        var fileAccessDetails = await context.FileAccessDetails.Where(fileAccess => fileAccess.HardDeletePending).ToListAsync(cancellationToken);
        logger.LogInformation("There are {Files} files marked for hard deletion", fileAccessDetails.Count);

        foreach (var fileAccessDetail in fileAccessDetails)
        {
            var fileDetail = await context.Files.SingleAsync(file => file.FileAccessDetail.Id == fileAccessDetail.Id, cancellationToken);
            logger.LogInformation("Hard-deleting file: {FileName} from {DirectoryName}", fileDetail.FileName, fileDetail.DirectoryName);
            DeleteFileIfItExists(fileSystem, fileDetail);

            _ = context.Files.Remove(fileDetail);
            _ = context.FileAccessDetails.Remove(fileAccessDetail);
        }

        await SaveChangesSafely(cancellationToken);
        logger.LogInformation("Completed removal of files marked for hard deletion");
    }

    public IEnumerable<string> GetFilesFromDirectory(string dir, string searchPattern = "*.*", bool recursive = true)
    {
        logger.LogInformation("Getting files in {Directory}", dir);
        var files = fileSystem.Directory.GetFiles(dir, searchPattern,
                            new EnumerationOptions()
                            {
                                RecurseSubdirectories = recursive,
                                IgnoreInaccessible = true
                            }
                        );

        logger.LogInformation("Got files in {Directory}", dir);

        return files;
    }

    public async Task ProcessNewFiles(IEnumerable<string> files, CancellationToken stoppingToken)
    {
        var counter = 0;
        var filesInDb = context.Files.Select(file => Path.Combine(file.DirectoryName, file.FileName));
        var notInTheDatabase = files.Except(filesInDb).ToList();
        foreach (var file in notInTheDatabase)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                await SaveChangesSafely(stoppingToken);
                break;
            }

            var lastIndexOf = file.LastIndexOf('\\');
            var directoryName = file[..lastIndexOf];
            var fileName = file[++lastIndexOf..];
            if (!await context.Files.AnyAsync(file => file.FileName == fileName && file.DirectoryName == directoryName, stoppingToken))
            {
                AddNewFile(file);
                counter++;

                if (counter >= 20)
                {
                    counter = 0;
                    await SaveChangesSafely(stoppingToken);
                    logger.LogInformation("Updating the database.");

                    logger.LogInformation("File {FileName} has been added to the database.", file);
                }
            }
        }

        await SaveChangesSafely(stoppingToken);
    }

    public async Task ProcessMovedFiles(IEnumerable<string> files, string[] directories, CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting update of files that have moved");
        foreach (var directory in directories)
        {
            foreach (var file in files.Where(file => file.StartsWith(directory)))
            {
                var lastIndexOf = file.LastIndexOf('\\');
                var directoryName = file[..lastIndexOf];
                var fileName = file[++lastIndexOf..];

                var movedFile = await context.Files.FirstOrDefaultAsync(f => f.DirectoryName.StartsWith(directory) && f.DirectoryName != directoryName && f.FileName == fileName, stoppingToken);
                if (movedFile != null)
                {
                    await UpdateExistingFile(directoryName, fileName, movedFile, stoppingToken);
                }
            }
        }

        await SaveChangesSafely(stoppingToken);
    }

    public async Task RemoveFilesFromDbThatDoNotExistAnyMore(IEnumerable<string> files, CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting removal of files deleted from disc outside of the UI.");

        var filesInDb = context.Files
                          .Include(x => x.FileAccessDetail)
                          .Where(file => !file.FileAccessDetail.SoftDeleted && !file.FileAccessDetail.SoftDeletePending && !file.FileAccessDetail.HardDeletePending)
                          .Select(file => Path.Combine(file.DirectoryName, file.FileName));

        var notOnDisc = await filesInDb.Except(files).ToListAsync(stoppingToken);
        foreach (var file in notOnDisc)
        {
            var lastIndexOf = file.LastIndexOf('\\');
            var directoryName = file[..lastIndexOf];
            var fileName = file[++lastIndexOf..];

            var fileDetail = await context.Files.SingleAsync(f => f.DirectoryName == directoryName && f.FileName == fileName, stoppingToken);
            var fileCount = await context.Files.CountAsync(stoppingToken);
            var fileAccessDetailCount = await context.FileAccessDetails.CountAsync(stoppingToken);
            _ = context.Files.Remove(fileDetail);
            await SaveChangesSafely(stoppingToken);
            var fileCountAfter = await context.Files.CountAsync(stoppingToken);
            var fileAccessDetailCountAfter = await context.FileAccessDetails.CountAsync(stoppingToken);
            logger.LogInformation("File Count before: {FileCount} File Access Detail Count before: {FileAccessDetailCount}, File Count after: {FileCountAfter} File Access Detail Count after: {FileAccessDetailCountAfter}", fileCount, fileAccessDetailCount, fileCountAfter, fileAccessDetailCountAfter);
        }

        await SaveChangesSafely(stoppingToken);
    }

    public async Task DeleteFilesPreviouslyMarkedDeletedAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting removal of files previously marked as deleted");
        var fileAccessDetails = await context.FileAccessDetails.Where(fileAccess => fileAccess.SoftDeleted).ToListAsync(stoppingToken);
        logger.LogInformation("There are {Files} files previously marked as deleted", fileAccessDetails.Count);

        foreach (var fileAccessDetail in fileAccessDetails)
        {
            var fileDetail = await context.Files.SingleAsync(file => file.FileAccessDetail.Id == fileAccessDetail.Id, stoppingToken);
            logger.LogInformation("Deleting file: {FileName} from {DirectoryName}", fileDetail.FileName, fileDetail.DirectoryName);
            DeleteFileIfItExists(fileSystem, fileDetail);

            fileAccessDetail.SoftDeletePending = false;
            fileAccessDetail.HardDeletePending = false;
            fileAccessDetail.SoftDeleted = true;
        }

        await SaveChangesSafely(stoppingToken);
        logger.LogInformation("Completed removal of files previously marked as deleted");
    }

    private async Task UpdateExistingFile(string directoryName, string fileName, FileDetail fileFromDatabase, CancellationToken stoppingToken)
    {
        foreach (var file in context.Files.Where(file => file.FileName == fileName))
        {
            _ = context.Files.Remove(file);
        }

        await SaveChangesSafely(stoppingToken);

        var updatedFile = new FileDetail
        {
            DirectoryName = directoryName,
            Height = fileFromDatabase.Height,
            Width = fileFromDatabase.Width,
            FileName = fileName,
            FileSize = fileFromDatabase.FileSize,
            FileAccessDetail = new FileAccessDetail
            {
                SoftDeleted = false,
                SoftDeletePending = false,
                DetailsLastUpdated = DateTime.UtcNow
            }
        };
        _ = await context.Files.AddAsync(updatedFile, stoppingToken);
        logger.LogInformation("File: {FileName} ({OriginalLocation}) appears to have moved since being added to the dB - previous location: {DirectoryName}", fileName, directoryName, fileFromDatabase.DirectoryName);
    }

    private void DeleteFileIfItExists(IFileSystem fileSystem, FileDetail fileDetail)
    {
        try
        {
            if (fileSystem.File.Exists(fileDetail.FullNameWithPath))
            {
                fileSystem.File.Delete(fileDetail.FullNameWithPath);
            }
        }
        catch (DirectoryNotFoundException ex)
        {
            logger.LogWarning(ex, "Directory not found: {FullNameWithPath}", fileDetail.FullNameWithPath);
        }
        catch (FileNotFoundException ex)
        {
            logger.LogWarning(ex, "File not found: {FullNameWithPath}", fileDetail.FullNameWithPath);
        }
    }

    private void AddNewFile(string file)
    {
        try
        {
            var fileInfo = fileSystem.FileInfo.New(file);
            var fileDetail = new FileDetail()
            {
                FileName = fileInfo.Name,
                DirectoryName = fileInfo.DirectoryName!,
                FileSize = fileInfo.Length
            };

            if (fileDetail.IsImage)
            {
                var image = SKImage.FromEncodedData(file);
                if (image is null)
                {
                    File.Delete(file);
                }
                else
                {
                    fileDetail.Height = image.Height;
                    fileDetail.Width = image.Width;
                }
            }

            var fileAccessDetail = new FileAccessDetail
            {
                SoftDeleted = false,
                SoftDeletePending = false,
                DetailsLastUpdated = DateTime.UtcNow
            };

            fileDetail.FileAccessDetail = fileAccessDetail;
            _ = context.Files.Add(fileDetail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving file '{File}' details", file);
        }
    }

    private async Task SaveChangesSafely(CancellationToken stoppingToken)
    {
        try
        {
            _ = await context.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            if (!ex.Message.StartsWith("The database operation was expected to affect"))
            {
                logger.LogError(ex, "Error: {Error} occurred whilst saving changes - probably 'no records affected'", ex.Message);
            }
        }
    }
}