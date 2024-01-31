using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace RaccoonSql.CoreRework.Serializer;

enum ThonkKind
{
    // TODO: what about special stuff like Lists and Dictionaries? => they get their own ThonkKind each, and the ThonkSchema gets their type params as ThonkSchemas
    Primitive,
    Array,
    List,
    Dictionary,
    HashSet,
    SomethingLikeAClass,
}

public class ThonkSchema
{
    private ThonkKind _kind;
    private Type _primitiveType;
    private ThonkSchema _typeParam1; // array, list, hashset, dictionary
    private ThonkSchema _typeParam2; // dictionary
    private List<(string Name, ThonkSchema Schema)> _properties;
}

public class Thonk
{
    private ThonkKind _kind;
    private object _primitiveValue;
    private List<Thonk> _listValues; // for array, list and hashset
    private List<(Thonk, Thonk)> _dictValues;
    private List<(string Name, Thonk Value)> _properties;
    private Dictionary<string, Thonk> _propertiesDictionary;

    public T As<T>() => (T)As(typeof(T));

    public object As(Type type)
    {
        EnsureCompatibleWith(type);

        switch (_kind)
        {
            case ThonkKind.Primitive:
                return _primitiveValue;
            case ThonkKind.Array:
                return AsArray(type.GetElementType()!);
            case ThonkKind.List:
            {
                return AsList(type.GenericTypeArguments[0]);
            }
            case ThonkKind.HashSet:
            {
                return AsHashSet(type.GenericTypeArguments[0]);
            }
            case ThonkKind.Dictionary:
            {
                return AsDictionary(type.GenericTypeArguments[0], type.GenericTypeArguments[1]);
            }

            case ThonkKind.SomethingLikeAClass:
            {
                var obj = RuntimeHelpers.GetUninitializedObject(type);

                foreach (var (name, thonk) in _properties)
                {
                    var prop = type.GetProperty(name)!;
                    var propVal = thonk.As(prop.PropertyType);
                    prop.SetValue(obj, propVal);
                }
                return obj;
            }
            default:
                throw new UnreachableException();
        }

    }

    public T[] AsArray<T>()
    {
        EnsureCompatibleWith<T[]>();
        var arr = new T[_listValues.Count];

        for (var i = 0; i < _listValues.Count; i++)
        {
            arr[i] = _listValues[i].As<T>();
        }
        return arr;
    }

    public List<T> AsList<T>()
    {
        EnsureCompatibleWith<List<T>>();
        var list = new List<T>(_listValues.Count);
        foreach (var listValue in _listValues)
        {
            list.Add(listValue.As<T>());
        }
        return list;
    }

    public HashSet<T> AsHashSet<T>()
    {
        EnsureCompatibleWith<HashSet<T>>();
        var hashset = new HashSet<T>(_listValues.Count);
        foreach (var listValue in _listValues)
        {
            hashset.Add(listValue.As<T>());
        }
        return hashset;
    }

    public Dictionary<TKey, TValue> AsDictionary<TKey, TValue>() where TKey : notnull
    {
        EnsureCompatibleWith<Dictionary<TKey, TValue>>();
        var dict = new Dictionary<TKey, TValue>(_dictValues.Count);
        foreach (var (keyThonk, valueThonk) in _dictValues)
        {
            dict.Add(keyThonk.As<TKey>(), valueThonk.As<TValue>());
        }
        return dict;
    }

    private object AsArray(Type elementType)
    {
        return typeof(Thonk)
            .GetMethod(nameof(AsArray), 1, [])!
            .MakeGenericMethod([elementType])
            .Invoke(this, [])!;
    }

    private object AsList(Type elementType)
    {
        return typeof(Thonk)
            .GetMethod(nameof(AsList), 1, [])!
            .MakeGenericMethod([elementType])
            .Invoke(this, [])!;
    }

    private object AsHashSet(Type elementType)
    {
        return typeof(Thonk)
            .GetMethod(nameof(AsHashSet), 1, [])!
            .MakeGenericMethod([elementType])
            .Invoke(this, [])!;
    }

    private object AsDictionary(Type keyType, Type valueType)
    {
        return typeof(Thonk)
            .GetMethod(nameof(AsDictionary), 2, [])!
            .MakeGenericMethod([keyType, valueType])
            .Invoke(this, [])!;
    }

    public bool IsCompatibleWith<T>() => IsCompatibleWith(typeof(T));

    public bool IsCompatibleWith(Type type)
    {
        switch (_kind)
        {
            // TODO 
            case ThonkKind.Primitive:
                return _primitiveValue.GetType().IsAssignableTo(type);
            case ThonkKind.SomethingLikeAClass when !type.IsClass:
                return false;
            case ThonkKind.SomethingLikeAClass:
            {
                var props = type.GetProperties();
                return props.Length == _properties.Count
                       && props.All(prop => _propertiesDictionary[prop.Name].IsCompatibleWith(prop.PropertyType));
            }
            default:
                throw new UnreachableException();
        }

    }

    private void EnsureCompatibleWith<T>() => EnsureCompatibleWith(typeof(T));

    private void EnsureCompatibleWith(Type type)
    {
        if (!IsCompatibleWith(type))
        {
            throw new WrongThonkException($"This Thonk is not compatible with type {type}");
        }
    }

    private void EnsureClassLike()
    {
        if (_kind != ThonkKind.SomethingLikeAClass) throw new WrongThonkException("This Thonk is not a class-like Thonk");
    }

    public IEnumerable<(string Name, Thonk Value)> EnumerateProperties()
    {
        EnsureClassLike();
        return _properties;
    }

    public bool TryGetProperty(string name, [NotNullWhen(true)] out Thonk? value)
    {
        EnsureClassLike();
        return _propertiesDictionary.TryGetValue(name, out value);
    }

    public bool TryGetProperty<T>(string name, [NotNullWhen(true)] out T? value)
    {
        EnsureClassLike();
        if (_propertiesDictionary.TryGetValue(name, out var thonkValue))
        {
            value = thonkValue.As<T>()!;
            return true;
        }
        value = default;
        return false;
    }

    public Thonk Property(string name)
    {
        EnsureClassLike();
        if (_propertiesDictionary.TryGetValue(name, out var value))
        {
            return value!;
        }
        throw new WrongThonkException($"This thonk as no property named {name}");
    }

    public T Property<T>(string name)
    {
        EnsureClassLike();
        return Property(name).As<T>();
    }

    public void Rename(string oldName, string newName)
    {
        EnsureClassLike();
        // TODO
    }

    public void Reorder(int TODO)
    {
        EnsureClassLike();
        // TODO
        // what parameters do we take?
    }

    public void Add(string name, Func<Thonk, Thonk> factory)
    {
        // what about position? 
        EnsureClassLike();
        if (_propertiesDictionary.ContainsKey(name))
        {
            throw new WrongThonkException($"This Thonk already has a property named {name}");
        }
        // TODO
    }

    public void Add<T>(string name, Func<Thonk, T> factory)
    {
        EnsureClassLike();

    }

    public void Add(string name, Func<Thonk> factory)
    {
        EnsureClassLike();

    }

    public void Add<T>(string name, Func<T> factory)
    {
        EnsureClassLike();

    }

    public void Remove(string name)
    {
        EnsureClassLike();

    }

    public void ModifyValue(string name, Func<Thonk, Thonk> mapper)
    {
        EnsureClassLike();

    }


    public void ModifyValue(string name, Func<Thonk, Thonk, Thonk> mapper)
    {
        EnsureClassLike();

    }

    public void ModifyValue<TFrom, TTo>(string name, Func<TFrom, TTo> mapper)
        where TFrom : unmanaged
        where TTo : unmanaged
    {
        EnsureClassLike();

    }

    public void ModifyValue<TFrom, TTo>(string name, Func<Thonk, TFrom, TTo> mapper)
        where TFrom : unmanaged
        where TTo : unmanaged
    {
        EnsureClassLike();

    }

    public void ModifyValue<TFrom>(string name, Func<Thonk, TFrom, Thonk> mapper)
        where TFrom : unmanaged
    {
        EnsureClassLike();

    }
}

public readonly struct ThonkSerializer : ISerializer
{
    public ThonkSerializer(ThonkSchema schema)
    {

    }
}

public class WrongThonkException : Exception
{
    public WrongThonkException(string msg) : base(msg)
    {
    }
}