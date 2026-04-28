using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Models;

namespace SemanticDb.EF.SqlServer.Search;

/// <summary>
/// An <see cref="IVectorSearch"/> implementation that uses SQL Server native
/// VECTOR_DISTANCE for efficient similarity search.
/// </summary>
internal sealed class SqlServerVectorSearch : IVectorSearch
{
    private readonly DbContext _dbContext;
    private readonly ILogger<SqlServerVectorSearch> _logger;
    private readonly string _sqlWithoutScope;
    private readonly string _sqlWithScope;

    public SqlServerVectorSearch(
        DbContext dbContext,
        SemanticDbOptions options,
        ILogger<SqlServerVectorSearch> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        var entityType = dbContext.Model.FindEntityType(typeof(RagChunk))
                         ?? throw new InvalidOperationException(
                             $"{nameof(RagChunk)} is not registered in the DbContext model. " +
                             "Call builder.ApplySemanticDbConfiguration() in OnModelCreating.");

        var table = entityType.GetTableName()!;
        var schema = entityType.GetSchema();
        var qualifiedTableName = schema is not null ? $"[{schema}].[{table}]" : $"[{table}]";
        var dimensions = options.VectorDimensions;

        _sqlWithoutScope = $"""
               SELECT TOP (@limit) EntityId, ScopeKey,
                   VECTOR_DISTANCE('cosine', Embedding, CAST(@vector AS VECTOR({dimensions}))) AS Score,
                   PromptContext
               FROM {qualifiedTableName}
               WHERE ChunkName = @chunkName
               ORDER BY Score ASC
               """;

        _sqlWithScope = $"""
               SELECT TOP (@limit) EntityId, ScopeKey,
                   VECTOR_DISTANCE('cosine', Embedding, CAST(@vector AS VECTOR({dimensions}))) AS Score,
                   PromptContext
               FROM {qualifiedTableName}
               WHERE ChunkName = @chunkName
                 AND ScopeKey = @scopeKey
               ORDER BY Score ASC
               """;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SemanticDbResult>> SearchAsync(
        string chunkName,
        float[] queryVector,
        string? scopeKey,
        int limit,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Executing SQL Server vector search: chunkName={ChunkName}, scopeKey={ScopeKey}, limit={Limit}.",
            chunkName, scopeKey, limit);

        var vectorJson = JsonSerializer.Serialize(queryVector);

        var sql = scopeKey is null ? _sqlWithoutScope : _sqlWithScope;

        var results = new List<SemanticDbResult>();

        var connection = _dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqlParameter("@vector", vectorJson));
        command.Parameters.Add(new SqlParameter("@chunkName", chunkName));
        command.Parameters.Add(new SqlParameter("@limit", limit));

        if (scopeKey is not null)
            command.Parameters.Add(new SqlParameter("@scopeKey", scopeKey));

        await _dbContext.Database.OpenConnectionAsync(cancellationToken);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SemanticDbResult
            {
                EntityId = reader.GetString(0),
                ScopeKey = reader.IsDBNull(1) ? null : reader.GetString(1),
                Score = (float)reader.GetDouble(2),
                PromptContext = reader.GetString(3)
            });
        }

        _logger.LogDebug("SQL Server vector search returned {Count} result(s).", results.Count);

        return results.AsReadOnly();
    }
}
