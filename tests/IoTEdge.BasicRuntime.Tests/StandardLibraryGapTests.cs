namespace IoTEdge.BasicRuntime.Tests;

public sealed class StandardLibraryGapTests
{
    [Fact]
    public void Math_gap_functions_match_my_basic_names()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            total = SGN(-12) + SGN(0) + SGN(9)
            total = total + ROUND(ASIN(0) + ACOS(1) + ATAN(0))
            return total
            """);

        Assert.Equal(0L, result.ReturnValue);
    }

    [Fact]
    public void Srnd_makes_random_sequence_repeatable()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            SRND(123)
            first = RND(1, 100)
            second = RND(1, 100)
            SRND(123)
            if first <> RND(1, 100) then
              return "first mismatch"
            endif
            if second <> RND(1, 100) then
              return "second mismatch"
            endif
            if RND() < 0 or RND() > 1 then
              return "range mismatch"
            endif
            return "ok"
            """);

        Assert.Equal("ok", result.ReturnValue);
    }

    [Fact]
    public void Collection_gap_functions_operate_on_lists()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            values = LIST(3, 1, 2)
            if BACK(values) <> 2 then
              return "back failed"
            endif
            SORT(values)
            if values(0) <> 1 or values(1) <> 2 or values(2) <> 3 then
              return "sort failed"
            endif
            if INDEX_OF(values, 2) <> 1 then
              return "index failed"
            endif
            if TYPE(INDEX_OF(values, 9)) <> "NIL" then
              return "missing index failed"
            endif
            return "ok"
            """);

        Assert.Equal("ok", result.ReturnValue);
    }

    [Fact]
    public void Get_and_set_support_list_dict_and_object_values()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            class point
              var x = 1
            endclass

            values = LIST("a", "b")
            SET(values, 1, "c")

            map = DICT()
            SET(map, "name", "edge")

            p = new(point)
            SET(p, "x", 42)

            if GET(values, 1) <> "c" then
              return "list failed"
            endif
            if GET(map, "name") <> "edge" then
              return "dict failed"
            endif
            if GET(p, "x") <> 42 then
              return "object failed"
            endif
            return "ok"
            """);

        Assert.Equal("ok", result.ReturnValue);
    }

    [Fact]
    public void Clone_creates_deep_copy_and_to_array_keeps_values_indexable()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            nested = DICT()
            SET(nested, "value", 1)
            original = LIST(nested)
            copied = CLONE(original)
            SET(GET(copied, 0), "value", 9)

            array = TO_ARRAY(LIST("x", "y"))
            if GET(GET(original, 0), "value") <> 1 then
              return "clone failed"
            endif
            if array(0) <> "x" or array(1) <> "y" then
              return "array failed"
            endif
            return "ok"
            """);

        Assert.Equal("ok", result.ReturnValue);
    }
}
