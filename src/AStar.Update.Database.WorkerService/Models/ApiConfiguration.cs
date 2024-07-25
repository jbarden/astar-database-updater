using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AStar.FilesApi.Client.SDK.FilesApi;

namespace AStar.Update.Database.WorkerService.Models;

public class ApiConfiguration
{
    public const string SectionLocation = "ApiConfiguration";

    [Required]
    public string[] Directories { get; set; } = [];

    [Required]
    public FilesApiConfiguration FilesApiConfiguration { get; set; } = new();

    public override string ToString() => JsonSerializer.Serialize(this);
}