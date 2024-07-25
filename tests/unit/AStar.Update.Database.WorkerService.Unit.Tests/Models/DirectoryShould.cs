namespace AStar.Update.Database.WorkerService.Models;

public class DirectoryShould
{
    [Fact]
    public void ReturnTheExpectedToString()
        => new Directory("", "").ToString().Should().Be(@"{""Old"":"""",""New"":""""}");
}