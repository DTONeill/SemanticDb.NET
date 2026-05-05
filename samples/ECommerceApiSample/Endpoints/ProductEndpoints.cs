using ECommerceApiSample.Data;
using ECommerceApiSample.Models;
using ECommerceApiSample.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using SemanticDb.Core.Abstractions;

namespace ECommerceApiSample.Endpoints;

/// <summary>Request body for creating a new product.</summary>
public record CreateProductRequest(
    string Name,
    string Description,
    decimal Price,
    int CategoryId);

/// <summary>Request body for updating an existing product.</summary>
public record UpdateProductRequest(
    string? Name,
    string? Description,
    decimal? Price,
    int? CategoryId);

/// <summary>Minimal API endpoints for product CRUD and semantic search.</summary>
public static class ProductEndpoints
{
    /// <summary>Registers all product endpoints on the given route builder.</summary>
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products", async (AppDbContext db) =>
        {
            var products = await db.Products
                .Include(p => p.Category)
                .OrderBy(p => p.CategoryId)
                .ThenBy(p => p.Name)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.IsArchived,
                    Category = p.Category.Name
                })
                .ToListAsync();

            return Results.Ok(products);
        });

        app.MapGet("/products/{id:int}", async (int id, AppDbContext db) =>
        {
            var product = await db.Products
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.IsArchived,
                    Category = p.Category.Name,
                    p.CategoryId
                })
                .FirstOrDefaultAsync();

            return product is null ? Results.NotFound() : Results.Ok(product);
        });

        app.MapPost("/products", async (CreateProductRequest body, AppDbContext db) =>
        {
            var category = await db.Categories.FindAsync(body.CategoryId);
            if (category is null)
            {
                return Results.BadRequest(new { error = $"Category {body.CategoryId} not found." });
            }

            var product = new Product
            {
                Name = body.Name,
                Description = body.Description,
                Price = body.Price,
                CategoryId = body.CategoryId
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            return Results.Created($"/products/{product.Id}", product);
        });

        app.MapPut("/products/{id:int}", async (int id, UpdateProductRequest body, AppDbContext db) =>
        {
            var product = await db.Products.FindAsync(id);
            if (product is null)
            {
                return Results.NotFound();
            }

            if (body.Name is not null) { product.Name = body.Name; }
            if (body.Description is not null) { product.Description = body.Description; }
            if (body.Price is not null) { product.Price = body.Price.Value; }
            if (body.CategoryId is not null)
            {
                var category = await db.Categories.FindAsync(body.CategoryId.Value);
                if (category is null)
                {
                    return Results.BadRequest(new { error = $"Category {body.CategoryId} not found." });
                }

                product.CategoryId = body.CategoryId.Value;
            }

            await db.SaveChangesAsync();
            return Results.Ok(product);
        });

        // Soft delete: sets IsArchived = true, which causes ProductsByCategoryChunk and
        // ProductDetailChunk to return IsDeleted = true, removing the chunk from the index.
        app.MapDelete("/products/{id:int}", async (int id, AppDbContext db) =>
        {
            var product = await db.Products.FindAsync(id);
            if (product is null)
            {
                return Results.NotFound();
            }

            product.IsArchived = true;
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        app.MapGet("/products/search", async (
            string q,
            int? categoryId,
            ISemanticDbService semanticSearch,
            AppDbContext db) =>
        {
            var results = categoryId is null
                ? await semanticSearch.SearchAsync<ProductsByCategoryChunk>(query: q)
                : await semanticSearch.SearchAsync<ProductsByCategoryChunk, string>(
                    query: q, categoryId.Value.ToString());

            if (results.Count == 0)
            {
                return Results.NotFound();
            }

            var entityIds = results.Select(r => int.Parse(r.EntityId)).ToList();

            var products = await db.Products
                .Where(p => entityIds.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.IsArchived,
                    Category = p.Category.Name,
                    p.CategoryId
                })
                .ToListAsync();

            var ordered = entityIds
                .Select(id => products.FirstOrDefault(p => p.Id == id))
                .Where(p => p is not null)
                .ToList();

            return Results.Ok(ordered);
        });

        // Uses ProductDetailChunk so ToPromptContext returns rich markdown context for the LLM.
        app.MapGet("/products/ask", async (
            string q,
            ISemanticDbService semanticSearch,
            IChatClient chatClient) =>
        {
            var results = await semanticSearch.SearchAsync<ProductDetailChunk>(query: q);

            if (results.Count == 0)
            {
                return Results.NotFound();
            }

            var context = string.Join("\n\n", results.Select(r => r.PromptContext));

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System,
                    "You are an e-commerce assistant. Answer the user's question based solely on " +
                    "the product catalog entries below. If the answer cannot be found in the " +
                    "provided products, say so.\n\n" +
                    $"Products:\n{context}"),
                new(ChatRole.User, q)
            };

            var response = await chatClient.GetResponseAsync(messages);

            return Results.Ok(new
            {
                answer = response.Text,
                sources = results.Select(r => r.EntityId)
            });
        });

        return app;
    }
}
