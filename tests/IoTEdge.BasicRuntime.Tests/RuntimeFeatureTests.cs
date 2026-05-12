namespace IoTEdge.BasicRuntime.Tests;

public sealed class RuntimeFeatureTests
{
    [Fact]
    public void Lambda_closes_over_outer_variables()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            x = 1
            f = lambda (a)
            (
              x = a + 1
            )
            f(4)
            return x
            """);

        Assert.Equal(5L, result.ReturnValue);
    }

    [Fact]
    public void Variadic_functions_consume_ellipsis_arguments()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            def sum(...)
              total = 0
              while len(...)
                total = total + ...
              wend
              return total
            enddef
            return sum(1, 2, 3, 4)
            """);

        Assert.Equal(10L, result.ReturnValue);
    }

    [Fact]
    public void Inherited_methods_can_access_me()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            class point
              var x = 2
              def get_x()
                return me.x
              enddef
            endclass

            p = new(point)
            p.x = 5
            return p.get_x()
            """);

        Assert.Equal(5L, result.ReturnValue);
    }

    [Fact]
    public void Import_resolves_relative_paths_from_the_source_file()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(Path.Combine(root, "lib.bas"), """
                def greet(name)
                  return "hello " + name
                enddef
                """);

            File.WriteAllText(Path.Combine(root, "main.bas"), """
                import "lib.bas"
                return greet("codex")
                """);

            var runtime = new BasicRuntime();
            var result = runtime.ExecuteFile(Path.Combine(root, "main.bas"));
            Assert.Equal("hello codex", result.ReturnValue);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
