using ECommerceApiSample.Data;
using ECommerceApiSample.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerceApiSample.Services;

/// <summary>
/// Hosted service that seeds the catalog with hardcoded categories, products, and reviews
/// on first startup. No external HTTP calls are made.
/// </summary>
public sealed class CatalogSeedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CatalogSeedService> _logger;

    /// <inheritdoc />
    public CatalogSeedService(
        IServiceScopeFactory scopeFactory,
        ILogger<CatalogSeedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.Categories.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Catalog already seeded, skipping.");
            return;
        }

        var categories = BuildCategories();
        db.Categories.AddRange(categories);
        await db.SaveChangesAsync(cancellationToken);

        var products = BuildProducts(categories);
        db.Products.AddRange(products);
        await db.SaveChangesAsync(cancellationToken);

        var reviews = BuildReviews(products);
        db.ProductReviews.AddRange(reviews);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded {Categories} categories, {Products} products, {Reviews} reviews.",
            categories.Count, products.Count, reviews.Count);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static List<Category> BuildCategories() =>
    [
        new Category { Name = "Electronics",           Description = "Cutting-edge gadgets and consumer electronics for work and play." },
        new Category { Name = "Books",                 Description = "Fiction, non-fiction, and technical titles across all genres." },
        new Category { Name = "Kitchen & Home",        Description = "Cookware, appliances, and home essentials for every household." },
        new Category { Name = "Sports & Outdoors",     Description = "Equipment and apparel for fitness enthusiasts and outdoor adventurers." },
        new Category { Name = "Beauty & Personal Care",Description = "Skincare, haircare, and wellness products for daily routines." },
        new Category { Name = "Toys & Games",          Description = "Puzzles, board games, and educational toys for all ages." }
    ];

    private static List<Product> BuildProducts(List<Category> categories)
    {
        var electronics = categories.First(c => c.Name == "Electronics");
        var books       = categories.First(c => c.Name == "Books");
        var kitchen     = categories.First(c => c.Name == "Kitchen & Home");
        var sports      = categories.First(c => c.Name == "Sports & Outdoors");
        var beauty      = categories.First(c => c.Name == "Beauty & Personal Care");
        var toys        = categories.First(c => c.Name == "Toys & Games");

        return
        [
            // Electronics
            new Product
            {
                Name = "Wireless Noise-Cancelling Headphones",
                Description = "Over-ear headphones with 30-hour battery, adaptive noise cancellation, and foldable design for travel.",
                Price = 249.99m,
                Category = electronics
            },
            new Product
            {
                Name = "USB-C Laptop Stand",
                Description = "Adjustable aluminium stand with a built-in 7-port USB hub supporting 4K display output.",
                Price = 79.99m,
                Category = electronics
            },
            new Product
            {
                Name = "Smart LED Desk Lamp",
                Description = "Touch-dimming lamp with 5 colour temperatures, USB-A charging port, and memory function.",
                Price = 44.99m,
                Category = electronics
            },
            new Product
            {
                Name = "Mechanical Keyboard (TKL)",
                Description = "Tenkeyless layout with tactile brown switches, PBT doubleshot keycaps, and USB-C detachable cable.",
                Price = 129.99m,
                Category = electronics
            },
            // Archived product — demonstrates IsDeleted = true from the very first startup.
            new Product
            {
                Name = "Portable Power Bank 20000mAh",
                Description = "Dual USB-A and USB-C PD 65W fast charge with LED indicator. Discontinued.",
                Price = 59.99m,
                Category = electronics,
                IsArchived = true
            },
            new Product
            {
                Name = "Bluetooth Mechanical Keyboard",
                Description = "Wireless tri-mode keyboard (Bluetooth 5.0, 2.4 GHz, USB-C) with hot-swap sockets and RGB backlight.",
                Price = 159.99m,
                Category = electronics
            },

            // Books
            new Product
            {
                Name = "The Pragmatic Programmer",
                Description = "Timeless advice on software craftsmanship from Andrew Hunt and David Thomas, covering career growth and clean code habits.",
                Price = 39.99m,
                Category = books
            },
            new Product
            {
                Name = "Clean Code",
                Description = "Robert C. Martin's principles for writing readable, maintainable code with real-world refactoring examples.",
                Price = 34.99m,
                Category = books
            },
            new Product
            {
                Name = "Designing Data-Intensive Applications",
                Description = "Deep dive into distributed systems, data storage, and replication by Martin Kleppmann. Essential for backend engineers.",
                Price = 49.99m,
                Category = books
            },
            new Product
            {
                Name = "Atomic Habits",
                Description = "James Clear on the science of habit formation, behaviour change, and building systems for continuous improvement.",
                Price = 18.99m,
                Category = books
            },
            new Product
            {
                Name = "Domain-Driven Design",
                Description = "Eric Evans' foundational text on modelling complex software domains using ubiquitous language and bounded contexts.",
                Price = 55.99m,
                Category = books
            },

            // Kitchen & Home
            new Product
            {
                Name = "Cast Iron Skillet 12\"",
                Description = "Pre-seasoned cast iron skillet that retains heat evenly for searing, baking, and braising on any cooktop.",
                Price = 39.99m,
                Category = kitchen
            },
            new Product
            {
                Name = "Electric Kettle 1.7L",
                Description = "Stainless steel kettle that boils in under 3 minutes with a keep-warm function and auto shut-off.",
                Price = 49.99m,
                Category = kitchen
            },
            new Product
            {
                Name = "Bamboo Cutting Board Set",
                Description = "Three-piece set with juice grooves, dishwasher-safe finish, and non-slip rubber feet.",
                Price = 29.99m,
                Category = kitchen
            },
            new Product
            {
                Name = "Pour-Over Coffee Set",
                Description = "Chemex-style glass carafe with a reusable stainless steel filter and a precision pour scale for café-quality coffee.",
                Price = 74.99m,
                Category = kitchen
            },

            // Sports & Outdoors
            new Product
            {
                Name = "Yoga Mat Non-Slip 6mm",
                Description = "Extra-thick yoga mat with alignment lines, non-slip texture, and a carry strap for studio or home practice.",
                Price = 34.99m,
                Category = sports
            },
            new Product
            {
                Name = "Resistance Bands Set",
                Description = "Five resistance levels from light to extra-heavy, latex-free material with door anchor and ankle straps included.",
                Price = 24.99m,
                Category = sports
            },
            new Product
            {
                Name = "Trekking Poles (Pair)",
                Description = "Lightweight aluminium collapsible poles with EVA foam grips, wrist straps, and carbide tips for all terrain.",
                Price = 54.99m,
                Category = sports
            },
            new Product
            {
                Name = "Running Backpack 15L",
                Description = "Lightweight hydration pack with a 2L water bladder, chest strap, and reflective strips for trail running.",
                Price = 89.99m,
                Category = sports
            },

            // Beauty & Personal Care
            new Product
            {
                Name = "Vitamin C Serum 30ml",
                Description = "20% stabilised ascorbic acid with hyaluronic acid and vitamin E for brightening and anti-ageing.",
                Price = 24.99m,
                Category = beauty
            },
            new Product
            {
                Name = "Electric Toothbrush",
                Description = "40,000 brush strokes per minute with a pressure sensor, 2-minute timer, and 4 cleaning modes.",
                Price = 69.99m,
                Category = beauty
            },
            new Product
            {
                Name = "Argan Oil Hair Mask",
                Description = "Deep conditioning treatment with pure argan oil, sulphate-free, 200ml. Restores shine and reduces frizz.",
                Price = 18.99m,
                Category = beauty
            },

            // Toys & Games
            new Product
            {
                Name = "Classic Chess Set",
                Description = "Tournament-weight pieces with a roll-up vinyl board, storage bag, and a compact travel tin.",
                Price = 44.99m,
                Category = toys
            },
            new Product
            {
                Name = "LEGO Architecture Set 600pc",
                Description = "Modular skyline building kit for ages 10+, includes printed instructions and collector display base.",
                Price = 64.99m,
                Category = toys
            },
            new Product
            {
                Name = "Magnetic Tile Set 100pc",
                Description = "STEM building blocks with clear windows, compatible with major brands, includes storage bag and idea booklet.",
                Price = 49.99m,
                Category = toys
            }
        ];
    }

    private static List<ProductReview> BuildReviews(List<Product> products)
    {
        // Helper to find a product by name
        Product P(string name) => products.First(p => p.Name == name);

        return
        [
            // Wireless Noise-Cancelling Headphones — 2 approved, 1 spam (unapproved)
            new ProductReview { Product = P("Wireless Noise-Cancelling Headphones"), AuthorName = "Alice M.", Rating = 5, Content = "Best headphones I have ever owned. The noise cancellation is phenomenal on flights and in open offices.", IsApproved = true },
            new ProductReview { Product = P("Wireless Noise-Cancelling Headphones"), AuthorName = "Bob K.",   Rating = 4, Content = "Great sound quality but the ear cups get warm after about two hours of continuous use.", IsApproved = true },
            new ProductReview { Product = P("Wireless Noise-Cancelling Headphones"), AuthorName = "SpamBot9000", Rating = 1, Content = "Buy cheap headphones at my website for half the price!!", IsApproved = false },

            // Mechanical Keyboard (TKL) — 2 approved, 1 unapproved
            new ProductReview { Product = P("Mechanical Keyboard (TKL)"), AuthorName = "Carol T.",   Rating = 5, Content = "Typing on this keyboard is a joy. The brown switches are satisfying without being too loud for the office.", IsApproved = true },
            new ProductReview { Product = P("Mechanical Keyboard (TKL)"), AuthorName = "Dave R.",    Rating = 4, Content = "Build quality is excellent and the PBT keycaps feel premium. Detachable cable is a nice touch.", IsApproved = true },
            new ProductReview { Product = P("Mechanical Keyboard (TKL)"), AuthorName = "Anonymous",  Rating = 1, Content = "Arrived with a broken keycap. Customer service did not respond.", IsApproved = false },

            // The Pragmatic Programmer — 2 approved, 1 unapproved
            new ProductReview { Product = P("The Pragmatic Programmer"), AuthorName = "Eve S.",    Rating = 5, Content = "Every developer should read this book early in their career. Changed how I approach software craftsmanship.", IsApproved = true },
            new ProductReview { Product = P("The Pragmatic Programmer"), AuthorName = "Frank L.",   Rating = 4, Content = "Still highly relevant in 2026 despite being over twenty years old. The DRY and orthogonality chapters are timeless.", IsApproved = true },
            new ProductReview { Product = P("The Pragmatic Programmer"), AuthorName = "Reviewer99", Rating = 1, Content = "Pages were misprinted. Half the book was blank. Returning it.", IsApproved = false },

            // Designing Data-Intensive Applications — 2 approved
            new ProductReview { Product = P("Designing Data-Intensive Applications"), AuthorName = "Grace H.",  Rating = 5, Content = "The most thorough treatment of distributed systems fundamentals I have read. A must-read for senior engineers.", IsApproved = true },
            new ProductReview { Product = P("Designing Data-Intensive Applications"), AuthorName = "Henry P.",  Rating = 5, Content = "Dense but worth every page. The chapters on replication and consensus algorithms alone justify the price.", IsApproved = true },

            // Cast Iron Skillet 12" — 2 approved
            new ProductReview { Product = P("Cast Iron Skillet 12\""), AuthorName = "Irene W.",  Rating = 5, Content = "Perfect sear on steaks every time. Seasoned it twice and it is essentially non-stick now.", IsApproved = true },
            new ProductReview { Product = P("Cast Iron Skillet 12\""), AuthorName = "Jack N.",   Rating = 4, Content = "Heavy but worth it for the heat retention. Great for cornbread too.", IsApproved = true },

            // Yoga Mat — 2 approved, 1 unapproved
            new ProductReview { Product = P("Yoga Mat Non-Slip 6mm"), AuthorName = "Karen Y.",  Rating = 5, Content = "The alignment lines are genuinely useful for checking my form. Grip is excellent even during hot yoga.", IsApproved = true },
            new ProductReview { Product = P("Yoga Mat Non-Slip 6mm"), AuthorName = "Liam F.",   Rating = 3, Content = "Good mat but it has a slight rubbery smell that takes a week or two to fade.", IsApproved = true },
            new ProductReview { Product = P("Yoga Mat Non-Slip 6mm"), AuthorName = "FakeReview", Rating = 5, Content = "Amazing product! I sell similar mats at my shop.", IsApproved = false },

            // Vitamin C Serum — 2 approved
            new ProductReview { Product = P("Vitamin C Serum 30ml"), AuthorName = "Mia B.",   Rating = 5, Content = "Visible difference in skin brightness after two weeks. Layers well under SPF without pilling.", IsApproved = true },
            new ProductReview { Product = P("Vitamin C Serum 30ml"), AuthorName = "Noah C.",  Rating = 4, Content = "Effective and well-priced. The pump dispenser is a bit stiff at first but improves with use.", IsApproved = true },
        ];
    }
}
