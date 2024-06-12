namespace AStar.Update.Database.WorkerService.Models;

public class DirectoryChangesShould
{
    [Fact]
    public void ReturnTheExpectedToString()
        => new DirectoryChanges().ToString().Should().Be(@"{""Directories"":[]}");
}