using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniERP.Infrastructure;
using ModelContextProtocol.Server;

var dataDirectory = Environment.GetEnvironmentVariable("MINIERP_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (string.IsNullOrWhiteSpace(localAppData))
        throw new InvalidOperationException("Set MINIERP_DATA_DIR to the directory containing erp.db and catalog.db.");

    dataDirectory = Path.Combine(localAppData, "MiniERP");
}

var erp = ReadOnlyConnectionString(dataDirectory, "erp.db");
var catalog = ReadOnlyConnectionString(dataDirectory, "catalog.db");

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddInfrastructure(erp, catalog);
builder.Services.AddApplication();
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

using var host = builder.Build();
await host.RunAsync();

static string ReadOnlyConnectionString(string directory, string name)
{
    var path = Path.GetFullPath(Path.Combine(directory, name));
    if (!File.Exists(path))
        throw new FileNotFoundException($"MiniERP database not found: {path}. Start MiniERP to initialize its databases, or set MINIERP_DATA_DIR.", path);

    // Fail closed: never create an empty database or apply migrations from MCP.
    return new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = SqliteOpenMode.ReadOnly
    }.ToString();
}
