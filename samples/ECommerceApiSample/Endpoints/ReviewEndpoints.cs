using ECommerceApiSample.Data;
using ECommerceApiSample.Models;
using ECommerceApiSample.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SemanticDb.Core.Abstractions;
using SemanticDb.Core.Search;

namespace ECommerceApiSample.Endpoints;

/// <summary>Request body for creating a new product review.</summary>
public record CreateReviewRequest(
    string AuthorName,
    int Rating,
    string Content);

/// <summary>Minimal API endpoints for product review CRUD and semantic search.</summary>
public static class ReviewEndpoints
{
    /// <summary>Registers all review endpoints on the given route builder.</summary>
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products/{productId:int}/reviews", async (int productId, AppDbContext db) =>
        {
            var product = await db.Products.FindAsync(productId);
            if (product is null)
            {
                return Results.NotFound();
            }

            var reviews = await db.ProductReviews
                .Where(r => r.ProductId == productId && r.IsApproved)
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            return Results.Ok(reviews);
        });

        // Reviews start as unapproved. Saving triggers the interceptor, which calls
        // IsDeleted() = !IsApproved = true, so no chunk is written until approved.
        app.MapPost("/products/{productId:int}/reviews", async (
            int productId,
            CreateReviewRequest body,
            AppDbContext db) =>
        {
            var product = await db.Products.FindAsync(productId);
            if (product is null)
            {
                return Results.NotFound();
            }

            if (body.Rating is < 1 or > 5)
            {
                return Results.BadRequest(new { error = "Rating must be between 1 and 5." });
            }

            var review = new ProductReview
            {
                ProductId = productId,
                AuthorName = body.AuthorName,
                Rating = body.Rating,
                Content = body.Content,
                IsApproved = false
            };

            db.ProductReviews.Add(review);
            await db.SaveChangesAsync();

            return Results.Created($"/products/{productId}/reviews/{review.Id}", review);
        });

        // Approving transitions IsDeleted from true → false, writing the chunk into the index.
        app.MapPut("/products/{productId:int}/reviews/{id:int}/approve", async (
            int productId,
            int id,
            AppDbContext db) =>
        {
            var review = await db.ProductReviews
                .FirstOrDefaultAsync(r => r.Id == id && r.ProductId == productId);

            if (review is null)
            {
                return Results.NotFound();
            }

            review.IsApproved = true;
            await db.SaveChangesAsync();

            return Results.Ok(review);
        });

        // Unapproving transitions IsDeleted from false → true, removing the chunk from the index.
        app.MapPut("/products/{productId:int}/reviews/{id:int}/unapprove", async (
            int productId,
            int id,
            AppDbContext db) =>
        {
            var review = await db.ProductReviews
                .FirstOrDefaultAsync(r => r.Id == id && r.ProductId == productId);

            if (review is null)
            {
                return Results.NotFound();
            }

            review.IsApproved = false;
            await db.SaveChangesAsync();

            return Results.Ok(review);
        });

        app.MapGet("/products/{productId:int}/reviews/search", async (
            int productId,
            string q,
            ISemanticSearcher<ProductReviewsByProductChunk> searcher,
            AppDbContext db) =>
        {
            var product = await db.Products.FindAsync(productId);
            if (product is null)
            {
                return Results.NotFound();
            }

            var results = await searcher.Query(q).WithScope(productId).ToListAsync();

            if (results.Count == 0)
            {
                return Results.NotFound();
            }

            var entityIds = results.Select(r => int.Parse(r.EntityId)).ToList();

            var reviews = await db.ProductReviews
                .Where(r => entityIds.Contains(r.Id))
                .ToListAsync();

            var ordered = entityIds
                .Select(id => reviews.FirstOrDefault(r => r.Id == id))
                .Where(r => r is not null)
                .ToList();

            return Results.Ok(ordered);
        });

        return app;
    }
}
