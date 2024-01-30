using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RaccoonSql.CoreRework.Serializer;

public static class RaccSerializer
{
    private static ConcurrentDictionary<Type, ISerializer> _serializers = new();
    
    public static ISerializer GetSerializer<T>() => GetSerializer(typeof(T));

    public static ISerializer GetSerializer(Type type)
    {
        return _serializers.GetOrAdd(type, t =>
        {
            if (t.IsPrimitive)
                return (ISerializer)typeof(PrimitiveSerializer<>)
                    .MakeGenericType(t)
                    .GetConstructor([])!
                    .Invoke([]);
            if (t.IsArray && t.GetElementType()!.IsPrimitive)
                return (ISerializer)typeof(PrimitiveArraySerializer<>)
                    .MakeGenericType(t.GetElementType()!)
                    .GetConstructor([])!
                    .Invoke([]);
            if (t == typeof(string))
                return new StringSerializer();
            
            throw new ArgumentException($"cannot serialize type {t.FullName}", nameof(type));
        });
    }

    public static void Serialize(Stream stream, object o)
    {
        var serializer = GetSerializer(o.GetType());
        throw new NotImplementedException();
    }

    public static T Deserialize<T>(Stream stream) => (T)Deserialize(stream, typeof(T));

    public static object Deserialize(Stream stream, Type type)
    {
        var serializer = GetSerializer(type);
        throw new NotImplementedException();
    }
}

public interface ISerializer
{
    object Deserialize(Stream stream);
    void Serialize(Stream stream, object o);
}

public class DictionarySerializer<TKey, TValue> : ISerializer 
    where TKey : notnull 
    where TValue : notnull
{
    private readonly PrimitiveSerializer<int> _intSerializer = new();
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
}

public class ClassSerializer<T> : ISerializer
    where T : class
{
    private readonly List<(ISerializer, Action<T, object>, Func<T, object>)> _serializers = [];
    
    public ClassSerializer()
    {
        foreach (var propertyInfo in typeof(T).GetProperties())
        {
            var serializer = RaccSerializer.GetSerializer(propertyInfo.PropertyType);
            
            var tParam = Expression.Parameter(typeof(T), "t");
            var vParam = Expression.Parameter(typeof(object), "v");
            var memberAccess = Expression.MakeMemberAccess(tParam, propertyInfo);
            var vCast = Expression.Convert(vParam, propertyInfo.PropertyType);
            var assignMember = Expression.Assign(memberAccess, vCast);
            var setter = Expression.Lambda<Action<T, object>>(assignMember, [tParam, vParam]);
            var memberCast = Expression.Convert(memberAccess, typeof(object));
            var getter = Expression.Lambda<Func<T, object>>(memberCast, [tParam]);
            
            _serializers.Add((serializer, setter.Compile(), getter.Compile()));
        }
    }
    
    public T Deserialize(Stream stream)
    {
        var t = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        foreach (var (serializer, setter, _) in _serializers)
        {
            var value = serializer.Deserialize(stream);
            setter(t, value);
        }
        return t;
    }

    object ISerializer.Deserialize(Stream stream)
    {
        return Deserialize(stream);
    }

    public void Serialize(Stream stream, T t)
    {
        foreach (var (serializer, _, getter) in _serializers)
        {
            var value = getter(t);
            serializer.Serialize(stream, value);
        }
    }
    
    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (T)o);
    }
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
}

internal class ByteSpanSerializer
{
    private readonly PrimitiveSerializer<int> _intSerializer = new();
    
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

public class PrimitiveArraySerializer<T> : ISerializer where T : unmanaged
{
    private readonly PrimitiveSerializer<int> _intSerializer = new();
    
    static PrimitiveArraySerializer()
    {
        Debug.Assert(typeof(T).IsPrimitive);
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
}

public unsafe class PrimitiveSerializer<T> : ISerializer where T : unmanaged
{
    static PrimitiveSerializer()
    {
        Debug.Assert(typeof(T).IsPrimitive);
    }
    
    public T Deserialize(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(T)];
        stream.ReadExactly(buffer);
        return MemoryMarshal.Read<T>(buffer);
    }

    object ISerializer.Deserialize(Stream stream)
    {
        return Deserialize(stream);
    }

    public void Serialize(Stream stream, T t)
    {
        Span<byte> buffer = stackalloc byte[sizeof(T)];
        MemoryMarshal.Write(buffer, t);
        stream.Write(buffer);
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (T)o);
    }
}