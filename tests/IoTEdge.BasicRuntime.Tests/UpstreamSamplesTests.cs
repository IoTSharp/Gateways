namespace IoTEdge.BasicRuntime.Tests;

public sealed class UpstreamSamplesTests
{
    public static TheoryData<string, string?, string> Samples => new()
    {
        { "sample01.bas", null, "Hello world!" },
        { "sample02.bas", null, "Primes in 50: 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, " },
        { "sample03.bas", "10", "Input: Pi = 3.04184" },
        { "sample04.bas", null, "1, 1, 2, 3, 5, 8, 13, 21, 34, 55, " },
        { "sample05.bas", null, "3\n4\n12\n" },
        { "sample06.bas", null, "Meow!\nWoof!\n" },
        { "sample07.bas", "hello", "Input: Hello World!\n" }
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Upstream_sample_files_match_expected_output(string fileName, string? input, string expected)
    {
        var runtime = new BasicRuntime();
        var options = input is null
            ? null
            : new BasicRuntimeOptions { InputProvider = _ => input };

        var result = runtime.ExecuteFile(TestSupport.SamplePath(fileName), options: options);
        Assert.Equal(expected, TestSupport.OutputText(result));
    }

    [Fact]
    public void Yard_sample_runs_to_completion_with_quit_input()
    {
        var runtime = new BasicRuntime();
        var result = runtime.ExecuteFile(
            TestSupport.YardPath("start.bas"),
            options: new BasicRuntimeOptions { InputProvider = _ => "q" });

        var output = TestSupport.OutputText(result);
        Assert.Contains("Welcome to Yet Another RPG Dungeon!", output);
        Assert.Contains("Bye.", output);
    }
}
