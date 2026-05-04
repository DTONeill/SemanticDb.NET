using SemanticDb.Core.Abstractions;

namespace SemanticDb.IntegrationTests.Infrastructure;

public sealed class ProductChunk : ISearchableEntity<TestProduct, string>
{
    public int Version => 1;
    public bool IsDeleted(TestProduct entity) => entity.IsDeleted;
    public string ToSearchContent(TestProduct entity) => entity.Description;
    public string ToPromptContext(TestProduct entity) => $"{entity.Name}: {entity.Description}";
    public object? GetScopeKey(TestProduct entity) => entity.TenantId;
}
