using AStar.Infrastructure.Data;

namespace AStar.Update.Database.WorkerService.Fixtures;

public class FilesContextFixture : IDisposable
{
    private bool disposedValue;

    public FilesContextFixture() => SUT = new MockFilesContext().CreateContext();

    public FilesContext SUT { get; }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                SUT.Dispose();
            }

            disposedValue = true;
        }
    }
}