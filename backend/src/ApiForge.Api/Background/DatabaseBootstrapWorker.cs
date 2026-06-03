using Microsoft.Data.SqlClient;

namespace ApiForge.Api.Background;

public sealed class DatabaseBootstrapWorker(IConfiguration configuration, ILogger<DatabaseBootstrapWorker> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Database:RunMigrationsOnStartup"))
        {
            return;
        }

        var connectionString = configuration.GetConnectionString("ApiForge");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("Database bootstrap skipped because the ApiForge connection string is missing.");
            return;
        }

        var scriptsPath = Path.Combine(AppContext.BaseDirectory, "database", "scripts");
        if (!Directory.Exists(scriptsPath))
        {
            logger.LogWarning("Database bootstrap skipped because script folder was not found at {ScriptsPath}.", scriptsPath);
            return;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var scriptFile in Directory.GetFiles(scriptsPath, "*.sql").OrderBy(Path.GetFileName))
        {
            var scriptName = Path.GetFileName(scriptFile);
            var script = await File.ReadAllTextAsync(scriptFile, cancellationToken);
            logger.LogInformation("Running database bootstrap script {ScriptName}.", scriptName);

            foreach (var batch in SplitBatches(script))
            {
                var commandText = RemoveDatabaseSwitching(batch);
                if (string.IsNullOrWhiteSpace(commandText))
                {
                    continue;
                }

                await using var command = new SqlCommand(commandText, connection)
                {
                    CommandTimeout = 120
                };
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IEnumerable<string> SplitBatches(string script)
    {
        return script
            .Replace("\r\n", "\n")
            .Split('\n')
            .Aggregate(new List<string> { string.Empty }, (batches, line) =>
            {
                if (line.Trim().Equals("go", StringComparison.OrdinalIgnoreCase))
                {
                    batches.Add(string.Empty);
                }
                else
                {
                    batches[^1] += line + Environment.NewLine;
                }

                return batches;
            })
            .Where(batch => !string.IsNullOrWhiteSpace(batch));
    }

    private static string RemoveDatabaseSwitching(string batch)
    {
        if (batch.Contains("create database", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var lines = batch
            .Split(Environment.NewLine)
            .Where(line =>
            {
                var trimmed = line.Trim();
                return !trimmed.StartsWith("use ", StringComparison.OrdinalIgnoreCase)
                    && !trimmed.StartsWith("if db_id", StringComparison.OrdinalIgnoreCase);
            });

        return string.Join(Environment.NewLine, lines);
    }
}
