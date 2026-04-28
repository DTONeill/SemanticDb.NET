using SemanticDb.Core.Configuration;
using Microsoft.Extensions.Hosting;

namespace SemanticDb.Core.Hosting;

internal sealed class SemanticDbValidationService : IHostedService
{
    private readonly SemanticDbBuilder _builder;
    private readonly SemanticDbOptions _options;

    public SemanticDbValidationService(SemanticDbBuilder builder, SemanticDbOptions options)
    {
        _builder = builder;
        _options = options;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_builder.ProviderKey is null)
            throw new InvalidOperationException(
                "No SemanticDb provider registered. Call .UseSqlServer() or another provider after AddSemanticDb().");

        if (_options.VectorDimensions <= 0)
            throw new InvalidOperationException(
                $"SemanticDb: {nameof(SemanticDbOptions.VectorDimensions)} must be a positive integer. " +
                $"Common values: 1536 (text-embedding-3-small), 3072 (text-embedding-3-large).");

        if (_options.MaxRetries < 0)
            throw new InvalidOperationException(
                $"SemanticDb: {nameof(SemanticDbOptions.MaxRetries)} must be >= 0.");

        if (_options.DefaultSearchLimit <= 0)
            throw new InvalidOperationException(
                $"SemanticDb: {nameof(SemanticDbOptions.DefaultSearchLimit)} must be a positive integer.");

        if (_options.RetryBaseDelay <= TimeSpan.Zero)
            throw new InvalidOperationException(
                $"SemanticDb: {nameof(SemanticDbOptions.RetryBaseDelay)} must be a positive duration.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
