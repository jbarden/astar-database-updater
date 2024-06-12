using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AStar.Update.Database.WorkerService.Models;

public class DirectoryChanges
{
    public const string SectionLocation = "DirectoryChanges";

    [Required]
    public Directory[] Directories { get; set; } = [];

    public override string ToString() => JsonSerializer.Serialize(this);
}