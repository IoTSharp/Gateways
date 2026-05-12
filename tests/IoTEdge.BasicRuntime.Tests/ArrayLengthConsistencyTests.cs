namespace IoTEdge.BasicRuntime.Tests;

public sealed class ArrayLengthConsistencyTests
{
    [Fact]
    public void Dim_uses_argument_as_length_for_one_dimensional_arrays()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            DIM values(3)
            values(0) = 10
            values(1) = 20
            values(2) = 30
            RETURN LEN(values) * 100 + values(0) + values(1) + values(2)
            """);

        Assert.Equal(360L, result.ReturnValue);
    }

    [Theory]
    [InlineData("values(3) = 40")]
    [InlineData("RETURN values(3)")]
    public void One_dimensional_arrays_reject_index_equal_to_length(string statement)
    {
        var runtime = new BasicRuntime();
        var exception = Assert.Throws<BasicRuntimeException>(() => runtime.Execute($"""
            DIM values(3)
            {statement}
            """));

        Assert.Contains("out of bounds", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dim_uses_each_argument_as_length_for_multidimensional_arrays()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            DIM grid(2, 3)
            grid(0, 0) = 1
            grid(0, 2) = 3
            grid(1, 0) = 10
            grid(1, 2) = 12
            RETURN LEN(grid) * 100 + grid(0, 0) + grid(0, 2) + grid(1, 0) + grid(1, 2)
            """);

        Assert.Equal(626L, result.ReturnValue);
    }

    [Theory]
    [InlineData("grid(2, 0) = 1")]
    [InlineData("grid(0, 3) = 1")]
    [InlineData("RETURN grid(2, 0)")]
    [InlineData("RETURN grid(0, 3)")]
    public void Multidimensional_arrays_reject_index_equal_to_dimension_length(string statement)
    {
        var runtime = new BasicRuntime();
        var exception = Assert.Throws<BasicRuntimeException>(() => runtime.Execute($"""
            DIM grid(2, 3)
            {statement}
            """));

        Assert.Contains("out of bounds", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
