namespace IoTEdge.BasicRuntime.Tests;

public sealed class CoreProfileConsistencyTests
{
    public static TheoryData<string> OutputCases => new()
    {
        "class-lambda",
        "print-empty-line",
        "print-separators"
    };

    [Theory]
    [MemberData(nameof(OutputCases))]
    public void Shared_core_profile_output_cases_match_expected_output(string caseName)
    {
        var runtime = new BasicRuntime();
        var result = runtime.ExecuteFile(TestSupport.CoreProfileCasePath($"{caseName}.bas"));
        var expected = File.ReadAllText(TestSupport.CoreProfileCasePath($"{caseName}.out"));

        Assert.Equal(TestSupport.Normalize(expected), TestSupport.OutputText(result));
    }
}
