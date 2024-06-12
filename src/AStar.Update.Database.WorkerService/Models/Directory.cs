using System.Text.Json;

namespace AStar.Update.Database.WorkerService.Models;

public record Directory(string Old, string New)
{
    public override string ToString() => JsonSerializer.Serialize(this);
}