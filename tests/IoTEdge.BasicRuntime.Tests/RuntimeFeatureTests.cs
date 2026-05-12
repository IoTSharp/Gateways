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

    [Fact]
    public void Runtime_exposes_standard_time_functions()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            before = TICKS()
            if DELAY(0) <> 1 then
              return "delay failed"
            endif
            if SLEEP(0) <> 1 then
              return "sleep failed"
            endif
            after = TICKS()
            if after < before then
              return "ticks moved backward"
            endif
            return "ok"
            """);

        Assert.Equal("ok", result.ReturnValue);
    }

    [Fact]
    public void End_if_is_accepted_as_endif_compatibility_alias()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            value = 0
            if true then
              if false then
                value = -1
              else
                value = 7
              end if
            end if
            return value
            """);

        Assert.Equal(7L, result.ReturnValue);
    }
}
