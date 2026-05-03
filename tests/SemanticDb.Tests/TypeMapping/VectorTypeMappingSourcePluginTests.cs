using Microsoft.EntityFrameworkCore.Storage;
using SemanticDb.EF.SqlServer.TypeMapping;

namespace SemanticDb.Tests.TypeMapping;

public class VectorTypeMappingSourcePluginTests
{
    private readonly VectorTypeMappingSourcePlugin _plugin = new(128);

    [Theory]
    [InlineData("VECTOR(128)")]
    [InlineData("vector(3)")]
    [InlineData("VECTOR(1536)")]
    public void FindMapping_ReturnsVectorMapping_WhenStoreTypeIsVector(string storeType)
    {
        var info = new RelationalTypeMappingInfo(storeType, null!, null, null, null, null);

        var result = _plugin.FindMapping(in info);

        Assert.IsType<VectorTypeMapping>(result);
    }

    [Fact]
    public void FindMapping_ReturnsNull_ForFloatArrayWithNoStoreType()
    {
        // Regression: before the fix, float[] alone triggered VECTOR mapping for any consumer entity.
        var info = new RelationalTypeMappingInfo(typeof(float[]), null, null, null!, false, null, null, null, null, null, null, null, false);

        var result = _plugin.FindMapping(in info);

        Assert.Null(result);
    }

    [Fact]
    public void FindMapping_ReturnsNull_ForUnrelatedClrType()
    {
        var info = new RelationalTypeMappingInfo(typeof(string), null, null, null!, false, null, null, null, null, null, null, null, false);

        var result = _plugin.FindMapping(in info);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("varbinary")]
    [InlineData("nvarchar(max)")]
    [InlineData("float")]
    public void FindMapping_ReturnsNull_ForNonVectorStoreType(string storeType)
    {
        var info = new RelationalTypeMappingInfo(storeType, null!, null, null, null, null);

        var result = _plugin.FindMapping(in info);

        Assert.Null(result);
    }

    [Fact]
    public void FindMapping_VectorMapping_HasCorrectStoreType()
    {
        var info = new RelationalTypeMappingInfo("VECTOR(128)", null!, null, null, null, null);

        var result = _plugin.FindMapping(in info);

        Assert.Equal("VECTOR(128)", result!.StoreType);
    }
}
