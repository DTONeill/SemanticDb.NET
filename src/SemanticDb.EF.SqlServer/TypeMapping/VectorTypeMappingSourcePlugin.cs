using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SemanticDb.EF.SqlServer.TypeMapping;

internal sealed class VectorTypeMappingSourcePlugin : IRelationalTypeMappingSourcePlugin
{
    private readonly int _dimensions;

    public VectorTypeMappingSourcePlugin(int dimensions)
    {
        _dimensions = dimensions;
    }

    public RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        if (mappingInfo.ClrType == typeof(float[]) ||
            mappingInfo.StoreTypeName?.StartsWith("VECTOR", StringComparison.OrdinalIgnoreCase) == true)
            return new VectorTypeMapping(_dimensions);

        return null;
    }
}
