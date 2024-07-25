using System.IO.Abstractions.TestingHelpers;
using AStar.Infrastructure.Data;
using AStar.Update.Database.WorkerService.Fixtures;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace AStar.Update.Database.WorkerService.Services;

public class FilesServiceShould
{
    private readonly FilesContext context;
    private readonly FilesService sut;

    public FilesServiceShould()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @"c:\temp\.editorconfig2", new MockFileData("Testing is meh.") },
            { @"c:\temp\files.list.txt", new MockFileData("Testing is meh.") },
            { @"c:\demo\jQuery.js", new MockFileData("some js") },
            { @"c:\demo\9.JPG", new MockFileData([0x12, 0x34, 0x56, 0xd2]) },
            { @"c:\demo\A-0005.JPG", new MockFileData([0x12, 0x34, 0x56, 0xd2]) }
        });

        context = new FilesContextFixture().SUT;
        sut = new FilesService(context, fileSystem, NullLogger<FilesService>.Instance);
    }

    [Fact]
    public async Task MarkAllSoftDeletePendingAsDeletedOnceComplete()
    {
        var f = context.FileAccessDetails.Where(fileAccessDetail => fileAccessDetail.SoftDeletePending);
        context.FileAccessDetails.Count(fileAccessDetail => fileAccessDetail.SoftDeletePending).Should().BeGreaterThan(0);

        await sut.DeleteFilesMarkedForSoftDeletionAsync(CancellationToken.None);

        context.FileAccessDetails.Count(fileAccessDetail => fileAccessDetail.SoftDeletePending).Should().Be(0);
        f.Count().Should().Be(0);
    }

    [Fact]
    public async Task MarkAllHardDeletePendingAsDeletedOnceComplete()
    {
        context.FileAccessDetails.Count(fileAccessDetail => fileAccessDetail.HardDeletePending).Should().BeGreaterThan(0);

        await sut.DeleteFilesMarkedForHardDeletionAsync(CancellationToken.None);

        context.FileAccessDetails.Count(fileAccessDetail => fileAccessDetail.HardDeletePending).Should().Be(0);
    }

    [Theory]
    [InlineData("*.*", true, 5)]
    [InlineData("*.*", false, 0)]
    [InlineData("*.js", true, 1)]
    [InlineData("*.js", false, 0)]
    public void ReturnTheExpectedFiles(string searchPattern, bool recursive, int expectedCount)
    {
        var files = sut.GetFilesFromDirectory(@"c:\", searchPattern, recursive);

        files.Should().HaveCount(expectedCount);
    }

    [Fact]
    public async Task ProcessNewFilesCorrectly()
    {
        var originalFileCount = context.Files.Count();
        var originalFileAccessDetailsCount = context.FileAccessDetails.Count();

        await sut.ProcessNewFiles([@"c:\myfile.txt", @"c:\demo\jQuery.js", @"c:\demo\image.gif"], CancellationToken.None);

        context.Files.Count().Should().BeGreaterThan(originalFileCount, "the SkiaSharp code fails... will extract shortly");
        context.FileAccessDetails.Count().Should().BeGreaterThan(originalFileAccessDetailsCount, "the SkiaSharp code fails... will extract shortly");
    }

    [Fact]
    public async Task ProcessMovedFilesCorrectly()
    {
        var originalMaxId = context.Files.Max(file => file.Id);
        await sut.ProcessMovedFiles([@"c:\.editorconfig2", @"c:\demo\9.JPG", @"c:\demo\A-0005.JPG"], [@"c:\"], CancellationToken.None);

        using var scope = new AssertionScope();
        var file1 = context.Files.OrderBy(file => file.Id).Last(file => file.FileName == ".editorconfig2" && file.DirectoryName == @"c:");
        file1.Id.Should().NotBe(originalMaxId);
        context.FileAccessDetails.Count(fileAccessDetail => fileAccessDetail.Id == file1.Id).Should().Be(1);
        var file2 = context.Files.OrderBy(file => file.Id).Last(file => file.FileName == "9.JPG" && file.DirectoryName == @"c:\demo");
        file2.Id.Should().NotBe(originalMaxId);
        context.FileAccessDetails.Count(fileAccessDetail => fileAccessDetail.Id == file2.Id).Should().Be(1);
        var file3 = context.Files.OrderBy(file => file.Id).Last(file => file.FileName == "A-0005.JPG" && file.DirectoryName == @"c:\demo");
        file3.Id.Should().NotBe(originalMaxId);
        context.FileAccessDetails.Count(fileAccessDetail => fileAccessDetail.Id == file3.Id).Should().Be(1);
    }

    [Fact]
    public async Task RemoveFilesFromDbThatDoNotExistAnyMoreCorrectly()
    {
        var originalFileCount = context.Files.Count();
        var originalFileAccessDetailsCount = context.FileAccessDetails.Count();

        await sut.RemoveFilesFromDbThatDoNotExistAnyMore([@"c:\not.important.txt"], CancellationToken.None);

        using var scope = new AssertionScope();
        context.Files.Count().Should().BeLessThan(originalFileCount, "the SkiaSharp code fails... will extract shortly");
        context.FileAccessDetails.Count().Should().BeLessThan(originalFileAccessDetailsCount, "the SkiaSharp code fails... will extract shortly");
    }
}