namespace AStar.Update.Database.WorkerService.Models;

public class ApiConfigurationShould
{
    [Fact]
    public void ReturnTheExpectedToString()
        => new ApiConfiguration().ToString().Should().Be(@"{""Directories"":[],""FilesApiConfiguration"":{""BaseUrl"":""http://not.set.com""}}");
}