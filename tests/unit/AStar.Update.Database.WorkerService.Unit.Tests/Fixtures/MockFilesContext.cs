using System.Text.Json;
using AStar.Infrastructure.Data;
using AStar.Infrastructure.Models;

namespace AStar.Update.Database.WorkerService.Fixtures;

public class MockFilesContext : IDisposable
{
    private bool disposedValue;
    private readonly ConnectionString connectionString = new() { Value = "Filename=:memory:" };
    private readonly FilesContext context;

    public MockFilesContext()
    {
        context = new FilesContext(connectionString, new() { InMemory = true });

        _ = context.Database.EnsureCreated();

        AddMockFiles(context);
        _ = context.SaveChanges();
    }

    public FilesContext Context() => context;

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                context.Dispose();
            }

            disposedValue = true;
        }
    }

    private static void AddMockFiles(FilesContext mockFilesContext)
    {
        var filesAsJson = File.ReadAllText(@"TestFiles\files.json");

        var listFromJson = JsonSerializer.Deserialize<IEnumerable<FileDetail>>(filesAsJson)!;

        mockFilesContext.AddRange(listFromJson);
    }
}