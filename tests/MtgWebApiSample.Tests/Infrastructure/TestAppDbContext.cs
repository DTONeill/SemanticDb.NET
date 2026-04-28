using Microsoft.EntityFrameworkCore;
using MtgWebApiSample.SqlServer.Data;
using SemanticDb.Core.Models;

namespace MtgWebApiSample.Tests.Infrastructure;

public sealed class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }

    protected override void ConfigureVectorSearch(ModelBuilder modelBuilder)
    {
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
