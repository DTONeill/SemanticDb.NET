using Microsoft.EntityFrameworkCore.Storage;

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
        if (mappingInfo.StoreTypeName?.StartsWith("VECTOR", StringComparison.OrdinalIgnoreCase) == true)
            return new VectorTypeMapping(_dimensions);

        return null;
    }
}
