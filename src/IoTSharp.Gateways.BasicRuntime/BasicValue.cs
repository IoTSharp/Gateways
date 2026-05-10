using System.Globalization;

namespace IoTSharp.Gateways.BasicRuntime;

internal enum BasicValueKind
{
    Nil,
    Number,
    String,
    List,
    Array
}

internal readonly struct BasicValue : IEquatable<BasicValue>
{
    private readonly double _number;
    private readonly string? _string;
    private readonly BasicList? _list;
    private readonly BasicArray? _array;

    private BasicValue(BasicValueKind kind, double number = 0, string? text = null, BasicList? list = null, BasicArray? array = null)
    {
        Kind = kind;
        _number = number;
        _string = text;
        _list = list;
        _array = array;
    }

    public static BasicValue Nil { get; } = new(BasicValueKind.Nil);

    public BasicValueKind Kind { get; }

    public double Number => Kind == BasicValueKind.Number ? _number : AsNumber();

    public string Text => Kind == BasicValueKind.String ? _string ?? string.Empty : AsString();

    public BasicList List => Kind == BasicValueKind.List && _list is not null
        ? _list
        : throw new InvalidOperationException("Value is not a LIST.");

    public BasicArray Array => Kind == BasicValueKind.Array && _array is not null
        ? _array
        : throw new InvalidOperationException("Value is not an ARRAY.");

    public static BasicValue FromNumber(double value)
        => new(BasicValueKind.Number, value);

    public static BasicValue FromBoolean(bool value)
        => FromNumber(value ? 1 : 0);

    public static BasicValue FromString(string? value)
        => new(BasicValueKind.String, text: value ?? string.Empty);

    public static BasicValue FromList(BasicList value)
        => new(BasicValueKind.List, list: value);

    public static BasicValue FromArray(BasicArray value)
        => new(BasicValueKind.Array, array: value);

    public static BasicValue FromObject(object? value)
    {
        if (value is null)
        {
            return Nil;
        }

        return value switch
        {
            BasicValue basicValue => basicValue,
            bool boolValue => FromBoolean(boolValue),
            byte byteValue => FromNumber(byteValue),
            sbyte sbyteValue => FromNumber(sbyteValue),
            short shortValue => FromNumber(shortValue),
            ushort ushortValue => FromNumber(ushortValue),
            int intValue => FromNumber(intValue),
            uint uintValue => FromNumber(uintValue),
            long longValue => FromNumber(longValue),
            ulong ulongValue => FromNumber(ulongValue),
            float floatValue => FromNumber(floatValue),
            double doubleValue => FromNumber(doubleValue),
            decimal decimalValue => FromNumber((double)decimalValue),
            string stringValue => FromString(stringValue),
            BasicList list => FromList(list),
            BasicArray array => FromArray(array),
            IReadOnlyList<object?> values => FromList(new BasicList(values.Select(FromObject))),
            IEnumerable<object?> values => FromList(new BasicList(values.Select(FromObject))),
            _ => FromString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
        };
    }

    public object? ToObject()
    {
        return Kind switch
        {
            BasicValueKind.Nil => null,
            BasicValueKind.Number => IsIntegral(_number) ? (object)(long)_number : _number,
            BasicValueKind.String => _string ?? string.Empty,
            BasicValueKind.List => _list!.Items.Select(item => item.ToObject()).ToArray(),
            BasicValueKind.Array => _array!.ToObjectArray(),
            _ => null
        };
    }

    public bool IsTruthy()
    {
        return Kind switch
        {
            BasicValueKind.Nil => false,
            BasicValueKind.Number => Math.Abs(_number) > double.Epsilon,
            BasicValueKind.String => !string.IsNullOrEmpty(_string),
            BasicValueKind.List => _list?.Items.Count > 0,
            BasicValueKind.Array => _array?.Length > 0,
            _ => false
        };
    }

    public double AsNumber()
    {
        return Kind switch
        {
            BasicValueKind.Nil => 0,
            BasicValueKind.Number => _number,
            BasicValueKind.String when double.TryParse(_string, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) => value,
            BasicValueKind.String when bool.TryParse(_string, out var value) => value ? 1 : 0,
            BasicValueKind.String => 0,
            BasicValueKind.List => _list?.Items.Count ?? 0,
            BasicValueKind.Array => _array?.Length ?? 0,
            _ => 0
        };
    }

    public string AsString()
    {
        return Kind switch
        {
            BasicValueKind.Nil => string.Empty,
            BasicValueKind.Number => FormatNumber(_number),
            BasicValueKind.String => _string ?? string.Empty,
            BasicValueKind.List => "[" + string.Join(", ", _list!.Items.Select(item => item.AsString())) + "]",
            BasicValueKind.Array => "[array]",
            _ => string.Empty
        };
    }

    public bool Equals(BasicValue other)
    {
        if (Kind is BasicValueKind.Number || other.Kind is BasicValueKind.Number)
        {
            return Math.Abs(AsNumber() - other.AsNumber()) < 0.0000000001d;
        }

        return string.Equals(AsString(), other.AsString(), StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
        => obj is BasicValue other && Equals(other);

    public override int GetHashCode()
        => Kind switch
        {
            BasicValueKind.Number => AsNumber().GetHashCode(),
            _ => AsString().GetHashCode(StringComparison.Ordinal)
        };

    public override string ToString()
        => AsString();

    private static bool IsIntegral(double value)
        => double.IsFinite(value) && Math.Abs(value % 1) < 0.0000000001d && value <= long.MaxValue && value >= long.MinValue;

    private static string FormatNumber(double value)
    {
        if (IsIntegral(value))
        {
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("G15", CultureInfo.InvariantCulture);
    }
}

internal sealed class BasicList
{
    public BasicList()
    {
    }

    public BasicList(IEnumerable<BasicValue> values)
    {
        Items.AddRange(values);
    }

    public List<BasicValue> Items { get; } = [];
}

internal sealed class BasicArray
{
    private readonly int[] _dimensions;
    private readonly BasicValue[] _items;

    public BasicArray(IReadOnlyList<int> dimensions)
    {
        if (dimensions.Count == 0)
        {
            throw new ArgumentException("Array requires at least one dimension.", nameof(dimensions));
        }

        _dimensions = dimensions.Select(value => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(dimensions), "Array dimensions must be positive.")).ToArray();
        var length = _dimensions.Aggregate(1, (current, value) => checked(current * value));
        _items = Enumerable.Repeat(BasicValue.Nil, length).ToArray();
    }

    public int Length => _items.Length;

    public BasicValue Get(IReadOnlyList<int> indexes)
        => _items[Offset(indexes)];

    public void Set(IReadOnlyList<int> indexes, BasicValue value)
        => _items[Offset(indexes)] = value;

    public object?[] ToObjectArray()
        => _items.Select(item => item.ToObject()).ToArray();

    private int Offset(IReadOnlyList<int> indexes)
    {
        if (indexes.Count != _dimensions.Length)
        {
            throw new BasicRuntimeException($"Array expects {_dimensions.Length} indexes but received {indexes.Count}.");
        }

        var offset = 0;
        var stride = 1;
        for (var index = _dimensions.Length - 1; index >= 0; index--)
        {
            var value = indexes[index];
            if (value < 0 || value >= _dimensions[index])
            {
                throw new BasicRuntimeException("Array index is out of bounds.");
            }

            offset += value * stride;
            stride *= _dimensions[index];
        }

        return offset;
    }
}
