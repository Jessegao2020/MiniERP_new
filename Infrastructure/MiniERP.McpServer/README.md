# MiniERP MCP Server (read-only prototype)

A separate .NET 8 console process for MCP clients. It uses the existing
Application services, Infrastructure repositories, EF Core and SQLite models.
No Avalonia reference, API key, HTTP listener, database creation or migration.

## Build and test

From the repository root:

```sh
dotnet build Infrastructure/MiniERP.McpServer/MiniERP.McpServer.csproj -c Release
python3 Infrastructure/MiniERP.McpServer/smoke.py
```

The smoke script performs a real stdio initialization, tool discovery and
read-only calls. It uses the normal MiniERP data directory unless
MINIERP_DATA_DIR points to a directory containing both initialized databases.
Use disposable copies first (make a consistent SQLite backup or copy with
MiniERP closed). Run the current desktop version to initialize/upgrade the
databases before opening them with the server.

## MCP client configuration

For a client that supports local stdio MCP processes, configure the command
and args using absolute paths, e.g. on Linux (replace the example paths):

```json
{
  "mcpServers": {
    "minierp": {
      "command": "dotnet",
      "args": ["/absolute/path/MiniERP_new/Infrastructure/MiniERP.McpServer/bin/Release/net8.0/MiniERP.McpServer.dll"]
    }
  }
}
```

Set MINIERP_DATA_DIR in the client's supported environment configuration if
using backup/test databases. By default the path is the same
LocalApplicationData/MiniERP directory used by Avalonia, normally
~/.local/share/MiniERP on Linux and %LOCALAPPDATA%/MiniERP on Windows.
The client starts the process and communicates via stdin/stdout; diagnostic
logs go to stderr. Running the executable interactively waits for MCP messages.

This JSON is an example for compatible MCP clients, not a guarantee that every
ChatGPT desktop build accepts it. ChatGPT local plugin installation and
authorization must be verified on the actual host. Web/mobile cannot directly
access a local stdio process merely by installing a plugin.

## Tools

- search_customers: company/address/country lookup, returns customer IDs.
- search_articles: model/catalog lookup, stored prices (currency not inferred).
- get_customer_quotations: quotation summaries by customer ID.
- get_quotation: historical quotation details, line items and totals.

All tools are read-only. SQLite connections enforce Mode=ReadOnly; no startup
migration, arbitrary SQL, deletion, update or file export is exposed. Each call
creates its own DI scope. Responses deliberately omit minimum prices, internal
notes, contact names and raw entity graphs. Existing repository searches are
case-sensitive and materialize matching rows before limiting the response.
Limits bound returned records, not database work. Quotation prices come from
historical snapshots, not the current catalog. Query results may be transmitted
to the AI provider; connect only if authorized to disclose the business data.

Future write tools should use shared business services, explicit validation,
confirmation, transaction boundaries, audit identity and concurrency safeguards.
