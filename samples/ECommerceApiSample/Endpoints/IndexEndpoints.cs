using ECommerceApiSample.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SemanticDb.Core.Abstractions;

namespace ECommerceApiSample.Endpoints;

/// <summary>
/// Admin endpoints that demonstrate <see cref="ISemanticDbIndexer"/> for explicit reindexing.
/// Useful when entities are updated via raw SQL, bulk imports, or other mechanisms that
/// bypass EF Core's change-tracking interceptor.
/// </summary>
public static class IndexEndpoints
{
    /// <summary>Registers all admin reindex endpoints on the given route builder.</summary>
    public static IEndpointRouteBuilder MapIndexEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/reindex/products", async (
            ISemanticDbIndexer indexer,
            CancellationToken ct) =>
        {
            await indexer.RequestReindexAsync<Product>(ct);
            return Results.Ok(new { queued = "all products" });
        });

        app.MapPost("/admin/reindex/products/{id:int}", async (
            int id,
            ISemanticDbIndexer indexer,
            CancellationToken ct) =>
        {
            await indexer.RequestReindexAsync<Product>(id, ct);
            return Results.Ok(new { queued = id });
        });

        app.MapPost("/admin/reindex/reviews", async (
            ISemanticDbIndexer indexer,
            CancellationToken ct) =>
        {
            await indexer.RequestReindexAsync<ProductReview>(ct);
            return Results.Ok(new { queued = "all reviews" });
        });

        app.MapPost("/admin/reindex/reviews/{id:int}", async (
            int id,
            ISemanticDbIndexer indexer,
            CancellationToken ct) =>
        {
            await indexer.RequestReindexAsync<ProductReview>(id, ct);
            return Results.Ok(new { queued = id });
        });

        // Restarts the entire search index: enqueues all entities of every registered type.
        app.MapPost("/admin/reindex", async (
            ISemanticDbIndexer indexer,
            CancellationToken ct) =>
        {
            await indexer.RequestReindexAllAsync(ct);
            return Results.Ok(new { queued = "all entity types" });
        });

        return app;
    }
}
