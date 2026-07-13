using System.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace HrastERP.Infrastructure.Persistence;

internal sealed class DatabaseSeeder
{
    private readonly HrastDbContext _context;
    private readonly IWebHostEnvironment _env;

    public DatabaseSeeder(HrastDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await EnsureHistoryTableAsync(connection, cancellationToken);

            var baseDir = AppContext.BaseDirectory;
            await ApplyScriptsAsync(connection, Path.Combine(baseDir, "Seeds", "Reference"), cancellationToken);

            if (_env.IsDevelopment())
                await ApplyScriptsAsync(connection, Path.Combine(baseDir, "Seeds", "Fixtures"), cancellationToken);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task EnsureHistoryTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS seed_history (
                script_name TEXT PRIMARY KEY,
                applied_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ApplyScriptsAsync(NpgsqlConnection connection, string folder, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder))
            return;

        var scripts = Directory
            .GetFiles(folder, "*.sql")
            .OrderBy(f =>
            {
                var prefix = Path.GetFileNameWithoutExtension(f).Split('_')[0];
                return int.TryParse(prefix, out var n) ? n : int.MaxValue;
            })
            .ThenBy(Path.GetFileName);

        foreach (var scriptPath in scripts)
        {
            var scriptName = Path.GetFileName(scriptPath);

            if (await IsAppliedAsync(connection, scriptName, cancellationToken))
                continue;

            var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken);

            await using var execCmd = connection.CreateCommand();
            execCmd.CommandText = sql;

            try
            {
                await execCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Seed script '{scriptName}' failed: {ex.Message}", ex);
            }

            await RecordHistoryAsync(connection, scriptName, cancellationToken);
        }
    }

    private static async Task<bool> IsAppliedAsync(NpgsqlConnection connection, string scriptName, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM seed_history WHERE script_name = $1";
        cmd.Parameters.AddWithValue(scriptName);
        var count = (long)(await cmd.ExecuteScalarAsync(cancellationToken))!;
        return count > 0;
    }

    private static async Task RecordHistoryAsync(NpgsqlConnection connection, string scriptName, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO seed_history (script_name) VALUES ($1)";
        cmd.Parameters.AddWithValue(scriptName);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
