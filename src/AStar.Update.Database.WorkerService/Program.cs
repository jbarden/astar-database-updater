using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Net.Mime;
using AStar.FilesApi.Client.SDK.FilesApi;
using AStar.Infrastructure.Data;
using AStar.Update.Database.WorkerService.Models;
using AStar.Update.Database.WorkerService.Services;
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

        Serilog.Core.Logger logger = new LoggerConfiguration()
                                    .ReadFrom.Configuration(configuration)
                                    .CreateLogger();
        try
        {

            Log.Logger = logger;
            logger.Information("AStar.Update.Database.WorkerService is starting up");

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
                client.DefaultRequestHeaders.Accept.Add(new(MediaTypeNames.Application.Json));
            });

            _ = builder.Services.AddSerilog(config => config.ReadFrom.Configuration(builder.Configuration))
                                .AddHostedService<DeleteMarkedFiles>()
                                .AddHostedService<UpdateDatabaseForAllFiles>()
                                .AddHostedService<MoveFiles>();
            using var context = new FilesContext(new() { Value = configuration.GetConnectionString("sqlServer")! },
                                                 new() { EnableLogging = false, IncludeSensitiveData = false, InMemory = false });
            _ = builder.Services.AddSingleton<IFileSystem, FileSystem>()
                                .AddSingleton<FilesService>()
                                .AddSingleton(_ => TimeProvider.System)
                                .AddSingleton(_ => context);

            var host = builder.Build();

            _ = context.Database.EnsureCreated();

            host.Run();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fatal error occurred in {AppName}", typeof(Program).AssemblyQualifiedName);
        }
    }
}