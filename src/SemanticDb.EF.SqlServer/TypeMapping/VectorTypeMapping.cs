using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage;

namespace SemanticDb.EF.SqlServer.TypeMapping;

internal sealed class VectorTypeMapping : RelationalTypeMapping
{
    private readonly int _dimensions;

    public VectorTypeMapping(int dimensions)
        : base(new RelationalTypeMappingParameters(
            new CoreTypeMappingParameters(typeof(float[])),
            storeType: $"VECTOR({dimensions})"))
    {
        _dimensions = dimensions;
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new VectorTypeMapping(_dimensions);

    public override DbType? DbType => null;

    public override string GenerateSqlLiteral(object? value)
    {
        if (value is null)
            return "NULL";

        float[] floats = (float[])value;
        return "[" + string.Join(",",
            floats.Select(f => f.ToString("G9", CultureInfo.InvariantCulture))) + "]";
    }
}
