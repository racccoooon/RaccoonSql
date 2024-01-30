using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Expression = System.Linq.Expressions.Expression;

namespace RaccoonSql.CoreRework.Serializer;

public static class RaccSerializer
{
    private static readonly ConcurrentDictionary<Type, ISerializer> Serializers = new();

    private static readonly Dictionary<Type, Type> SpecialGenericSerializers = new()
    {
        { typeof(List<>), typeof(ListSerializer<>) },
        { typeof(Dictionary<,>), typeof(DictionarySerializer<,>) },
        { typeof(HashSet<>), typeof(HashSetSerializer<>) },
    };

    public static ISerializer GetSerializer<T>() => GetSerializer(typeof(T));

    public static ISerializer GetSerializer(Type type)
    {
        return Serializers.GetOrAdd(type, MakeSerializer);
    }

    public static ISerializer GetListSerializer<T>() => GetListSerializer(typeof(T));

    public static ISerializer GetListSerializer(Type t) => GetSerializer(typeof(List<>).MakeGenericType(t));

    public static ISerializer GetListSerializer<TElement, TCollector>() => GetListSerializer(typeof(TElement), typeof(TCollector));

    public static ISerializer GetListSerializer(Type elementType, Type collectorType)
    {
        return ConstructSerializer(typeof(ListSerializerWithCollector<,>), elementType, collectorType);
    }

    public static ISerializer GetTypedDictionarySerializer<T>() => GetTypedDictionarySerializer(typeof(T));

    public static ISerializer GetTypedDictionarySerializer(Type t)
    {
        return ConstructSerializer(typeof(TypedDictionarySerializer<>), t);
    }

    public static void Serialize(Stream stream, object o)
    {
        var serializer = GetSerializer(o.GetType());
        serializer.Serialize(stream, o);
    }

    public static T Deserialize<T>(Stream stream) => (T)Deserialize(stream, typeof(T));

    public static object Deserialize(Stream stream, Type type)
    {
        var serializer = GetSerializer(type);
        return serializer.Deserialize(stream);
    }

    private static ISerializer ConstructSerializer(Type serializerType, params Type[] typeArgs)
    {
        return (ISerializer)serializerType
            .MakeGenericType(typeArgs)
            .GetConstructor([])!
            .Invoke([]);
    }

    private static ISerializer MakeSerializer(Type type)
    {

        if (type.IsValueType)
        {
            return ConstructSerializer(typeof(ValueSerializer<>), type);
        }

        if (type.IsArray && type.GetElementType()!.IsValueType)
            return ConstructSerializer(typeof(ValueArraySerializer<>), type.GetElementType()!);
        if (type == typeof(string))
            return new StringSerializer();
        if (type.IsGenericType && SpecialGenericSerializers.TryGetValue(type.GetGenericTypeDefinition(), out var serializerType))
        {
            return ConstructSerializer(serializerType, type.GenericTypeArguments);
        }

        if (type.IsClass)
        {
            return ConstructSerializer(typeof(ClassSerializer<>), type);
        }

        throw new ArgumentException($"cannot serialize type {type.FullName}", nameof(type));
    }
}

public interface ISerializer
{
    object Deserialize(Stream stream);
    void Serialize(Stream stream, object o);
    Type SerializedType { get; }
}

public class TypedDictionarySerializer<T> : ISerializer
{
    private readonly Dictionary<string, (ISerializer serializer, PropertyInfo propertyInfo)> _serializers = [];
    private readonly StringSerializer _stringSerializer = new();
    private readonly ValueSerializer<int> _intSerializer = new();

    public TypedDictionarySerializer()
    {
        foreach (var propertyInfo in typeof(T).GetProperties())
        {
            var serializer = RaccSerializer.GetSerializer(propertyInfo.PropertyType);
            _serializers[propertyInfo.Name] = (serializer, propertyInfo);
        }
    }

    public Dictionary<PropertyInfo, object> Deserialize(Stream stream)
    {
        var result = new Dictionary<PropertyInfo, object>();
        var count = _intSerializer.Deserialize(stream);
        for (var i = 0; i < count; i++)
        {
            var name = _stringSerializer.Deserialize(stream);
            var value = _serializers[name].serializer.Deserialize(stream);
            result[_serializers[name].propertyInfo] = value;
        }
        return result;
    }

    object ISerializer.Deserialize(Stream stream)
    {
        return Deserialize(stream);
    }

    public void Serialize(Stream stream, Dictionary<PropertyInfo, object> dict)
    {
        _intSerializer.Serialize(stream, dict.Count);
        foreach (var (key, value) in dict)
        {
            _stringSerializer.Serialize(stream, key.Name);
            _serializers[key.Name].serializer.Serialize(stream, value);
        }
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (Dictionary<PropertyInfo, object>)o);
    }

    public Type SerializedType { get; } = typeof(Dictionary<PropertyInfo, object>);
}

public class DictionarySerializer<TKey, TValue> : ISerializer
    where TKey : notnull
    where TValue : notnull
{
    private readonly ValueSerializer<int> _intSerializer = new();
    private readonly ISerializer _keySerializer = RaccSerializer.GetSerializer<TKey>();
    private readonly ISerializer _valueSerializer = RaccSerializer.GetSerializer<TValue>();

    public Dictionary<TKey, TValue> Deserialize(Stream stream)
    {
        var count = _intSerializer.Deserialize(stream);
        var result = new Dictionary<TKey, TValue>(count);
        for (var i = 0; i < count; i++)
        {
            var key = _keySerializer.Deserialize(stream);
            var value = _valueSerializer.Deserialize(stream);

            result[(TKey)key] = (TValue)value;
        }
        return result;
    }

    object ISerializer.Deserialize(Stream stream)
    {
        return Deserialize(stream);
    }

    public void Serialize(Stream stream, Dictionary<TKey, TValue> dictionary)
    {
        _intSerializer.Serialize(stream, dictionary.Count);
        foreach (var (key, value) in dictionary)
        {
            _keySerializer.Serialize(stream, key);
            _valueSerializer.Serialize(stream, value);
        }
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (Dictionary<TKey, TValue>)o);
    }

    public Type SerializedType { get; } = typeof(Dictionary<TKey, TValue>);
}

public class HashSetSerializer<TElement> : ISerializer where TElement : notnull
{
    private readonly ValueSerializer<int> _intSerializer = new();
    private readonly ISerializer _elementSerializer = RaccSerializer.GetSerializer<TElement>();

    public HashSet<TElement> Deserialize(Stream stream)
    {
        var count = _intSerializer.Deserialize(stream);
        var result = new HashSet<TElement>(count);
        for (var i = 0; i < count; i++)
        {
            var value = _elementSerializer.Deserialize(stream);

            result.Add((TElement)value);
        }
        return result;
    }

    object ISerializer.Deserialize(Stream stream)
    {
        return Deserialize(stream);
    }

    public void Serialize(Stream stream, HashSet<TElement> hashSet)
    {
        _intSerializer.Serialize(stream, hashSet.Count);
        foreach (var value in hashSet)
        {
            _elementSerializer.Serialize(stream, value);
        }
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (HashSet<TElement>)o);
    }

    public Type SerializedType { get; } = typeof(HashSet<TElement>);
}

public class ListSerializerWithCollector<TElement, TCollector> : ISerializer
    where TElement : TCollector
{
    private readonly ValueSerializer<int> _intSerializer = new();
    private readonly ISerializer _elementSerializer = RaccSerializer.GetSerializer<TElement>();

    public List<TCollector> Deserialize(Stream stream)
    {
        var count = _intSerializer.Deserialize(stream);
        var result = new List<TCollector>(count);
        for (var i = 0; i < count; i++)
        {
            var value = _elementSerializer.Deserialize(stream);

            result.Add((TElement)value);
        }
        return result;
    }

    object ISerializer.Deserialize(Stream stream)
    {
        return Deserialize(stream);
    }

    public void Serialize(Stream stream, List<TCollector> list)
    {
        _intSerializer.Serialize(stream, list.Count);
        foreach (var value in list)
        {
            _elementSerializer.Serialize(stream, value!);
        }
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (List<TCollector>)o);
    }

    public Type SerializedType { get; } = typeof(List<>).MakeGenericType(typeof(TCollector));
}

public class ListSerializer<TElement> : ListSerializerWithCollector<TElement, TElement>;

public unsafe class ValueSerializer<T> : ISerializer
    where T : unmanaged
{
    private readonly int _size = sizeof(T);

    static ValueSerializer()
    {
        Debug.Assert(!RuntimeHelpers.IsReferenceOrContainsReferences<T>());
    }

    public T Deserialize(Stream stream)
    {
        var buffer = stackalloc byte[_size];
        stream.ReadExactly(new Span<byte>(buffer, _size));
        return Unsafe.Read<T>(buffer);
    }

    object ISerializer.Deserialize(Stream stream)
    {
        return Deserialize(stream);
    }

    public void Serialize(Stream stream, T t)
    {
        stream.Write(new ReadOnlySpan<byte>((byte*)&t, _size));
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (T)o);
    }

    public Type SerializedType { get; } = typeof(T);
}

public class ClassSerializer<T> : ISerializer
    where T : class
{
    private readonly Action<Stream, T> _serializer;
    private readonly Action<Stream, T> _deserializer;

    public ClassSerializer()
    {
        List<Expression> readers = [];
        List<Expression> writers = [];

        var tParam = Expression.Parameter(typeof(T), "t");
        var streamParam = Expression.Parameter(typeof(Stream), "stream");
        

        foreach (var propertyInfo in typeof(T).GetProperties())
        {
            if (!propertyInfo.CanRead || !propertyInfo.CanWrite) continue;
            var serializerInstance = RaccSerializer.GetSerializer(propertyInfo.PropertyType);

            var memberAccess = Expression.MakeMemberAccess(tParam, propertyInfo);
            var serializerExpr = Expression.Constant(serializerInstance); // TODO: this probably doesn't work, right?
            var deserializeCall = Expression.Call(serializerExpr, typeof(ISerializer).GetMethod(nameof(ISerializer.Deserialize))!, streamParam);
            readers.Add(Expression.Assign(memberAccess, Expression.Convert(deserializeCall, propertyInfo.PropertyType)));
            var serializeCall = Expression.Call(serializerExpr, typeof(ISerializer).GetMethod(nameof(ISerializer.Serialize))!, streamParam,
                Expression.Convert(memberAccess, typeof(object)));
            writers.Add(serializeCall);
        }

        _serializer = Expression.Lambda<Action<Stream, T>>(Expression.Block(writers), [streamParam, tParam]).Compile();
        _deserializer = Expression.Lambda<Action<Stream, T>>(Expression.Block(readers), [streamParam, tParam]).Compile();

    }

    public T Deserialize(Stream stream)
    {
        var t = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        _deserializer(stream, t);
        return t;
    }

    object ISerializer.Deserialize(Stream stream)
    {
        return Deserialize(stream);
    }

    public void Serialize(Stream stream, T t)
    {
        _serializer(stream, t);
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (T)o);
    }

    public Type SerializedType { get; } = typeof(T);
}

public class StringSerializer : ISerializer
{
    private readonly ByteSpanSerializer _byteArraySerializer = new();

    public string Deserialize(Stream stream)
    {
        var bytes = _byteArraySerializer.Deserialize(stream);
        return new string(MemoryMarshal.Cast<byte, char>(bytes));
    }

    object ISerializer.Deserialize(Stream stream)
    {
        return Deserialize(stream);
    }

    public void Serialize(Stream stream, string s)
    {
        var span = s.AsSpan();
        _byteArraySerializer.Serialize(stream, MemoryMarshal.AsBytes(span));
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (string)o);
    }

    public Type SerializedType { get; } = typeof(string);
}

internal class ByteSpanSerializer
{
    private readonly ValueSerializer<int> _intSerializer = new();

    public Span<byte> Deserialize(Stream stream)
    {
        var length = _intSerializer.Deserialize(stream);

        var array = new byte[length];
        var buffer = new Span<byte>(array);
        stream.ReadExactly(buffer);
        return buffer;
    }

    public void Serialize(Stream stream, ReadOnlySpan<byte> bytes)
    {
        _intSerializer.Serialize(stream, bytes.Length);
        stream.Write(bytes);
    }
}

public class ValueArraySerializer<T> : ISerializer where T : unmanaged
{
    private readonly ValueSerializer<int> _intSerializer = new();

    static ValueArraySerializer()
    {
        Debug.Assert(typeof(T).IsValueType);
    }

    public T[] Deserialize(Stream stream)
    {
        var length = _intSerializer.Deserialize(stream);

        var array = new T[length];
        var buffer = MemoryMarshal.AsBytes(new Span<T>(array));
        stream.ReadExactly(buffer);
        return array;
    }

    object ISerializer.Deserialize(Stream stream)
    {
        return Deserialize(stream);
    }

    public void Serialize(Stream stream, T[] ts)
    {
        _intSerializer.Serialize(stream, ts.Length);

        var bytes = MemoryMarshal.AsBytes(new ReadOnlySpan<T>(ts));
        stream.Write(bytes);
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (T[])o);
    }

    public Type SerializedType { get; } = typeof(T[]);
}