using System.Globalization;

namespace IoTSharp.Edge.BasicRuntime;

internal enum BasicValueKind
{
    Nil,
    Number,
    String,
    List,
    Array,
    Dictionary,
    Iterator,
    Class,
    Instance,
    Callable
}

internal readonly struct BasicValue : IEquatable<BasicValue>
{
    private readonly double _number;
    private readonly string? _string;
    private readonly BasicList? _list;
    private readonly BasicArray? _array;
    private readonly object? _reference;

    private BasicValue(BasicValueKind kind, double number = 0, string? text = null, BasicList? list = null, BasicArray? array = null, object? reference = null)
    {
        Kind = kind;
        _number = number;
        _string = text;
        _list = list;
        _array = array;
        _reference = reference;
    }

    public static BasicValue Nil { get; } = new(BasicValueKind.Nil);

    public BasicValueKind Kind { get; }

    public BasicList List => Kind == BasicValueKind.List && _list is not null
        ? _list
        : throw new InvalidOperationException("Value is not a LIST.");

    public double Number => Kind == BasicValueKind.Number ? _number : AsNumber();

    public string Text => Kind == BasicValueKind.String ? _string ?? string.Empty : AsString();

    public BasicArray Array => Kind == BasicValueKind.Array && _array is not null
        ? _array
        : throw new InvalidOperationException("Value is not an ARRAY.");

    public BasicDictionary Dictionary => Kind == BasicValueKind.Dictionary && _reference is BasicDictionary dictionary
        ? dictionary
        : throw new InvalidOperationException("Value is not a DICTIONARY.");

    public BasicIterator Iterator => Kind == BasicValueKind.Iterator && _reference is BasicIterator iterator
        ? iterator
        : throw new InvalidOperationException("Value is not an ITERATOR.");

    public BasicObjectValue ObjectValue => Kind is BasicValueKind.Class or BasicValueKind.Instance && _reference is BasicObjectValue value
        ? value
        : throw new InvalidOperationException("Value is not an object.");

    public IBasicCallable Callable => Kind == BasicValueKind.Callable && _reference is IBasicCallable callable
        ? callable
        : throw new InvalidOperationException("Value is not callable.");

    public static BasicValue FromNumber(double value)
        => new(BasicValueKind.Number, number: value);

    public static BasicValue FromBoolean(bool value)
        => FromNumber(value ? 1 : 0);

    public static BasicValue FromString(string? value)
        => new(BasicValueKind.String, text: value ?? string.Empty);

    public static BasicValue FromList(BasicList value)
        => new(BasicValueKind.List, list: value);

    public static BasicValue FromArray(BasicArray value)
        => new(BasicValueKind.Array, array: value);

    public static BasicValue FromDictionary(BasicDictionary value)
        => new(BasicValueKind.Dictionary, reference: value);

    public static BasicValue FromIterator(BasicIterator value)
        => new(BasicValueKind.Iterator, reference: value);

    public static BasicValue FromClass(BasicObjectValue value)
        => new(BasicValueKind.Class, reference: value);

    public static BasicValue FromInstance(BasicObjectValue value)
        => new(BasicValueKind.Instance, reference: value);

    public static BasicValue FromCallable(IBasicCallable value)
        => new(BasicValueKind.Callable, reference: value);

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
            byte[] byteArray => FromList(new BasicList(byteArray.Select(byteValue => FromNumber(byteValue)))),
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
            BasicDictionary dictionary => FromDictionary(dictionary),
            BasicIterator iterator => FromIterator(iterator),
            BasicObjectValue obj => obj.IsPrototype ? FromClass(obj) : FromInstance(obj),
            IBasicCallable callable => FromCallable(callable),
            IReadOnlyDictionary<string, object?> readOnlyDictionary => FromDictionary(ToBasicDictionary(readOnlyDictionary)),
            IDictionary<string, object?> mutableDictionary => FromDictionary(ToBasicDictionary(mutableDictionary)),
            IReadOnlyList<object?> values => FromList(new BasicList(values.Select(FromObject))),
            IEnumerable<byte> bytes => FromList(new BasicList(bytes.Select(byteValue => FromNumber(byteValue)))),
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
            BasicValueKind.Dictionary => DictionaryToObject(Dictionary),
            BasicValueKind.Iterator => _reference,
            BasicValueKind.Class or BasicValueKind.Instance or BasicValueKind.Callable => _reference,
            _ => _reference
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
            BasicValueKind.Dictionary => _reference is BasicDictionary dictionary && dictionary.Count > 0,
            BasicValueKind.Iterator => _reference is BasicIterator iterator && !iterator.IsExhausted,
            BasicValueKind.Class or BasicValueKind.Instance or BasicValueKind.Callable => true,
            _ => _reference is not null
        };
    }

    public double AsNumber()
    {
        return Kind switch
        {
            BasicValueKind.Nil => 0,
            BasicValueKind.Number => _number,
            BasicValueKind.String when double.TryParse(_string, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) => value,
            BasicValueKind.String when bool.TryParse(_string, out var boolValue) => boolValue ? 1 : 0,
            BasicValueKind.String => 0,
            BasicValueKind.List => _list?.Items.Count ?? 0,
            BasicValueKind.Array => _array?.Length ?? 0,
            BasicValueKind.Dictionary when _reference is BasicDictionary dictionary => dictionary.Count,
            BasicValueKind.Iterator => _reference is BasicIterator iterator ? iterator.Index + 1 : 0,
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
            BasicValueKind.Dictionary => "[dict]",
            BasicValueKind.Iterator => "[iterator]",
            BasicValueKind.Class or BasicValueKind.Instance => _reference is BasicObjectValue obj ? obj.DisplayName : string.Empty,
            BasicValueKind.Callable => _reference?.ToString() ?? string.Empty,
            _ => _reference?.ToString() ?? string.Empty
        };
    }

    public BasicValue CloneDeep()
    {
        return Kind switch
        {
            BasicValueKind.List => FromList(new BasicList(_list!.Items.Select(item => item.CloneDeep()))),
            BasicValueKind.Array => FromArray(_array!.CloneDeep()),
            BasicValueKind.Dictionary => FromDictionary(Dictionary.CloneDeep()),
            BasicValueKind.Iterator => FromIterator(Iterator.CloneDeep()),
            BasicValueKind.Class => FromClass(ObjectValue.CloneDeepPrototype()),
            BasicValueKind.Instance => FromInstance(ObjectValue.CloneDeepInstance()),
            _ => this
        };
    }

    public bool Equals(BasicValue other)
    {
        if (Kind is BasicValueKind.Number || other.Kind is BasicValueKind.Number)
        {
            return Math.Abs(AsNumber() - other.AsNumber()) < 0.0000000001d;
        }

        if (Kind is BasicValueKind.Class or BasicValueKind.Instance or BasicValueKind.Callable or BasicValueKind.Dictionary or BasicValueKind.Iterator
            || other.Kind is BasicValueKind.Class or BasicValueKind.Instance or BasicValueKind.Callable or BasicValueKind.Dictionary or BasicValueKind.Iterator)
        {
            return ReferenceEquals(_reference, other._reference);
        }

        return string.Equals(AsString(), other.AsString(), StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
        => obj is BasicValue other && Equals(other);

    public override int GetHashCode()
        => Kind switch
        {
            BasicValueKind.Number => AsNumber().GetHashCode(),
            BasicValueKind.Class or BasicValueKind.Instance or BasicValueKind.Callable or BasicValueKind.Dictionary or BasicValueKind.Iterator => _reference?.GetHashCode() ?? 0,
            _ => AsString().GetHashCode(StringComparison.Ordinal)
        };

    public override string ToString()
        => AsString();

    private static bool IsIntegral(double value)
        => double.IsFinite(value) && Math.Abs(value % 1) < 0.0000000001d && value <= long.MaxValue && value >= long.MinValue;

    private static BasicDictionary ToBasicDictionary(IEnumerable<KeyValuePair<string, object?>> values)
    {
        var dictionary = new BasicDictionary();
        foreach (var pair in values)
        {
            dictionary.Set(pair.Key, FromObject(pair.Value));
        }

        return dictionary;
    }

    private static Dictionary<string, object?> DictionaryToObject(BasicDictionary dictionary)
        => dictionary.Keys.ToDictionary(
            key => key,
            key => dictionary.Get(key).ToObject(),
            StringComparer.OrdinalIgnoreCase);

    private static string FormatNumber(double value)
    {
        if (IsIntegral(value))
        {
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("G6", CultureInfo.InvariantCulture);
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

    private BasicArray(int[] dimensions, BasicValue[] items)
    {
        _dimensions = dimensions;
        _items = items;
    }

    public int Length => _items.Length;

    public BasicValue Get(IReadOnlyList<int> indexes)
        => _items[Offset(indexes)];

    public void Set(IReadOnlyList<int> indexes, BasicValue value)
        => _items[Offset(indexes)] = value;

    public object?[] ToObjectArray()
        => _items.Select(item => item.ToObject()).ToArray();

    public BasicArray CloneDeep()
        => new((int[])_dimensions.Clone(), _items.Select(item => item.CloneDeep()).ToArray());

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
