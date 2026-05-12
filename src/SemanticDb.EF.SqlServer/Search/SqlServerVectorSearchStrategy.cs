using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SemanticDb.Core.Chunk;
using SemanticDb.Core.Configuration;
using SemanticDb.Core.Models;
using SemanticDb.Core.Search;

namespace SemanticDb.EF.SqlServer.Search;

/// <summary>
/// <see cref="ISearchStrategy"/> implementation that generates an embedding for the query text
/// and executes a native SQL Server <c>VECTOR_DISTANCE</c> query for efficient similarity search.
/// Registered by <c>UseSqlServer()</c>.
/// </summary>
internal sealed class SqlServerVectorSearchStrategy : ISearchStrategy
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly DbContext _dbContext;
    private readonly ILogger<SqlServerVectorSearchStrategy> _logger;
    private readonly string _sqlWithoutScope;
    private readonly string _sqlWithScope;

    public SqlServerVectorSearchStrategy(
        [FromKeyedServices(SemanticDbBuilder.EmbeddingGeneratorKey)]
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        DbContext dbContext,
        SemanticDbOptions options,
        ILogger<SqlServerVectorSearchStrategy> logger)
    {
        _embeddingGenerator = embeddingGenerator;
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
                1.0 - VECTOR_DISTANCE('cosine', Embedding, CAST(@vector AS VECTOR({dimensions}))) AS Score,
                PromptContext
            FROM {qualifiedTableName}
            WHERE ChunkName = @chunkName
            ORDER BY Score DESC
            """;

        _sqlWithScope = $"""
            SELECT TOP (@limit) EntityId, ScopeKey,
                1.0 - VECTOR_DISTANCE('cosine', Embedding, CAST(@vector AS VECTOR({dimensions}))) AS Score,
                PromptContext
            FROM {qualifiedTableName}
            WHERE ChunkName = @chunkName
              AND ScopeKey = @scopeKey
            ORDER BY Score DESC
            """;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SemanticDbResult>> ExecuteAsync(SearchExecutionContext ctx, CancellationToken ct)
    {
        _logger.LogDebug(
            "Executing SQL Server vector search: chunkName={ChunkName}, scopeKey={ScopeKey}, limit={Limit}.",
            ctx.ChunkName, ctx.ScopeKey, ctx.TopK);

        var embedding = await _embeddingGenerator.GenerateVectorAsync(ctx.QueryText, cancellationToken: ct);
        var vectorJson = JsonSerializer.Serialize(embedding.ToArray());
        var sql = ctx.ScopeKey is null ? _sqlWithoutScope : _sqlWithScope;

        var results = new List<SemanticDbResult>();

        await using var connection = new SqlConnection(_dbContext.Database.GetConnectionString());
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqlParameter("@vector", vectorJson));
        command.Parameters.Add(new SqlParameter("@chunkName", ctx.ChunkName));
        command.Parameters.Add(new SqlParameter("@limit", ctx.TopK));

        if (ctx.ScopeKey is not null)
            command.Parameters.Add(new SqlParameter("@scopeKey", ctx.ScopeKey));

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
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
