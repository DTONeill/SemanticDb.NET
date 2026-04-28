namespace SemanticDb.IntegrationTests.Infrastructure;

public sealed class TestProduct
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? TenantId { get; set; }
}
