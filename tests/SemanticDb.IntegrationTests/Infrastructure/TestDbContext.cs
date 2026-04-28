using Microsoft.EntityFrameworkCore;
using SemanticDb.Core.Models;
using SemanticDb.EF.Extensions;

namespace SemanticDb.IntegrationTests.Infrastructure;

public sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<TestProduct> Products => Set<TestProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySemanticDbConfiguration();

        // SQLite doesn't support float[] natively — store as BLOB
        modelBuilder.Entity<RagChunk>()
            .Property(x => x.Embedding)
            .HasConversion(
                v => FloatsToBytes(v),
                v => BytesToFloats(v));
    }

    private static byte[] FloatsToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToFloats(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
