using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using AStar.FilesApi.Client.SDK.FilesApi;
using AStar.Infrastructure.Data;
using AStar.Update.Database.WorkerService.Models;
using AStar.Update.Database.WorkerService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace AStar.Update.Database.WorkerService;

[ExcludeFromCodeCoverage]
internal class Program
{
    protected Program()
    { }

    private static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
                                    .AddJsonFile("appsettings.json")
                                    .AddUserSecrets<Program>()
                                    .Build();

        var logger = new LoggerConfiguration()
                                .ReadFrom.Configuration(configuration)
                                .CreateLogger();

        Log.Logger = logger;
        logger.Information("AStar.Update.Database.WorkerService Starting up");

        var builder = Host.CreateApplicationBuilder(args);

        _ = builder.Services.AddOptions<ApiConfiguration>()
                            .Bind(configuration.GetSection(ApiConfiguration.SectionLocation))
                            .ValidateOnStart();
        _ = builder.Services.AddOptions<DirectoryChanges>()
                            .Bind(configuration.GetSection(DirectoryChanges.SectionLocation))
                            .ValidateOnStart();
        _ = builder.Services.AddOptions<FilesApiConfiguration>()
                            .Bind(configuration.GetSection(FilesApiConfiguration.SectionLocation))
                            .ValidateDataAnnotations()
                            .ValidateOnStart();

        _ = builder.Services.AddHttpClient<FilesApiClient>().ConfigureHttpClient((serviceProvider, client) =>
                            {
                                client.BaseAddress = serviceProvider.GetRequiredService<IOptions<FilesApiConfiguration>>().Value.BaseUrl;
                                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                            });

        _ = builder.Services.AddSerilog(config => config.ReadFrom.Configuration(builder.Configuration));
        _ = builder.Services.AddSingleton(new AStarDbContextOptions() { EnableLogging = true, IncludeSensitiveData  = true });
        _ = builder.Services.AddHostedService<UpdateDatabaseForAllFiles>()
                            .AddHostedService<DeleteMarkedFiles>()
                            .AddHostedService<MoveFiles>();
        _ = builder.Services.AddSingleton<IFileSystem, FileSystem>();
        _ = builder.Services.AddSingleton<FilesService>();
        _ = builder.Services.AddSingleton(_ => TimeProvider.System);
        using var context = new FilesContext(new DbContextOptionsBuilder<FilesContext>().UseSqlite(ServiceConstants.SqliteConnectionString).Options, new AStarDbContextOptions());
        _ = builder.Services.AddSingleton(_ => context);

        var host = builder.Build();

        _ = context.Database.EnsureCreated();

        host.Run();
    }
}