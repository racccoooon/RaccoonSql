using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RaccoonSql.CoreRework.Serializer;

// TODO: a thonk should be able to contain null and a thonk schema should be able to represent nullable types, but how to represent that?

public enum ThonkKind
{
    Primitive,
    Array,
    List,
    HashSet,
    Dictionary,
    Class,
}

public class ThonkSchema
{
    private ThonkKind _kind;
    private Type _primitiveType;
    private ThonkSchema[] _typeParams;
    private List<(string Name, ThonkSchema Schema)> _properties;
    private Dictionary<string, ThonkSchema> _propertyDictionary;

    public ThonkKind Kind => _kind;

    public bool IsPrimitive => _kind == ThonkKind.Primitive;
    public bool IsArray => _kind == ThonkKind.Array;
    public bool IsList => _kind == ThonkKind.List;
    public bool IsHashSet => _kind == ThonkKind.HashSet;
    public bool IsDictionary => _kind == ThonkKind.Dictionary;
    public bool IsClass => _kind == ThonkKind.Class;


    public ThonkSchema ForType<T>() => ForType(typeof(T));

    public ThonkSchema ForType(Type type)
    {
        if (type.IsArray)
        {
            return new ThonkSchema
            {
                _kind = ThonkKind.Array,
                _typeParams = [ForType(type.GetElementType()!)],
            };
        }
        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            if (genericDefinition == typeof(List<>))
            {
                return new ThonkSchema
                {
                    _kind = ThonkKind.List,
                    _typeParams = [ForType(type.GenericTypeArguments[0])],
                };
            }
            if (genericDefinition == typeof(HashSet<>))
            {
                return new ThonkSchema
                {
                    _kind = ThonkKind.HashSet,
                    _typeParams = [ForType(type.GenericTypeArguments[0])],
                };
            }
            if (genericDefinition == typeof(Dictionary<,>))
            {
                return new ThonkSchema
                {
                    _kind = ThonkKind.Dictionary,
                    _typeParams = [ForType(type.GenericTypeArguments[0]), ForType(type.GenericTypeArguments[1])],
                };
            }
        }
        if (type.IsClass)
        {
            var properties = type.GetProperties();
            var schemaProperties = properties.Select(propertyInfo => (propertyInfo.Name, ForType(propertyInfo.PropertyType))).ToList();
            return new ThonkSchema
            {
                _kind = ThonkKind.Class,
                _properties = schemaProperties,
                _propertyDictionary = schemaProperties.ToDictionary(),
            };
        }
        throw new WrongThonkException($"Can not make a ThonkSchema from type {type}");
    }


    public Type? PrimitiveType => IsPrimitive ? _primitiveType : null;
    public ThonkSchema? ArrayElementType => IsArray ? _typeParams[0] : null;
    public ThonkSchema? ListType => IsArray ? _typeParams[0] : null;
    public ThonkSchema? HashSetType => IsArray ? _typeParams[0] : null;
    public ThonkSchema? DictionaryKeyType => IsArray ? _typeParams[0] : null;
    public ThonkSchema? DictionaryValueType => IsArray ? _typeParams[1] : null;

    public IEnumerable<(string Name, ThonkSchema Type)> Properties => _properties;
    
    public ThonkSchema? Property(string name) => !IsClass ? null : _propertyDictionary.GetValueOrDefault(name);

    public void RenameProperty(string name, string newName)
    {

    }

    public void MoveProperty(string name, int position)
    {

    }

    public void RemoveProperty(string name)
    {

    }

    public void AddProperty(string name, ThonkSchema newSchema)
    {

    }

    public void ChangeProperty(string name, ThonkSchema newSchema)
    {

    }


}

public class Thonk
{
    private ThonkKind _kind;
    private object _primitiveValue;
    private List<Thonk> _listValues; // for array, list and hashset
    private List<(Thonk, Thonk)> _dictValues;
    private List<(string Name, Thonk Value)> _properties;
    private Dictionary<string, Thonk> _propertiesDictionary;

    private static Thonk MakeArray(Type type, object value)
    {
        return (Thonk)typeof(Thonk).GetMethod(nameof(MakeArray), 1, [type.MakeArrayType()])!
            .MakeGenericMethod([type])
            .Invoke(null, [value])!;
    }

    private static Thonk MakeArray<T>(T[] value)
    {
        return new Thonk
        {
            _kind = ThonkKind.Array,
            _listValues = value.Select(Make).ToList(),
        };
    }

    private static Thonk MakeList(Type type, object value)
    {
        return (Thonk)typeof(Thonk).GetMethod(nameof(MakeList), 1, [typeof(List<>).MakeGenericType(type)])!
            .MakeGenericMethod([type])
            .Invoke(null, [value])!;
    }

    private static Thonk MakeList<T>(List<T> value)
    {
        return new Thonk
        {
            _kind = ThonkKind.List,
            _listValues = value.Select(Make).ToList(),
        };
    }


    private static Thonk MakeHashSet(Type type, object value)
    {
        return (Thonk)typeof(Thonk).GetMethod(nameof(MakeHashSet), 1, [typeof(HashSet<>).MakeGenericType(type)])!
            .MakeGenericMethod([type])
            .Invoke(null, [value])!;
    }

    private static Thonk MakeHashSet<T>(HashSet<T> value)
    {
        return new Thonk
        {
            _kind = ThonkKind.HashSet,
            _listValues = value.Select(Make).ToList(),
        };
    }


    private static Thonk MakeDictionary(Type keyType, Type valueType, object value)
    {
        return (Thonk)typeof(Thonk).GetMethod(nameof(MakeDictionary), 2, [typeof(Dictionary<,>).MakeGenericType([keyType, valueType])])!
            .MakeGenericMethod([keyType, valueType])
            .Invoke(null, [value])!;
    }

    private static Thonk MakeDictionary<TKey, TValue>(Dictionary<TKey, TValue> value)
        where TKey : notnull
    {
        return new Thonk
        {
            _kind = ThonkKind.Dictionary,
            _dictValues = value.Select(x => (Make(x.Key), Make(x.Value))).ToList(),
        };
    }

    private static Thonk MakeClass<T>(T value)
        where T : class
        => MakeClass(typeof(T), value);

    private static Thonk MakeClass(Type type, object value)
    {
        var classProperties = type.GetProperties();
        var thonkProperties = classProperties
            .Select(property => (property.Name, Make(property.PropertyType, property.GetValue(value))))
            .ToList();
        return new Thonk
        {
            _kind = ThonkKind.Class,
            _properties = thonkProperties,
            _propertiesDictionary = thonkProperties.ToDictionary(),
        };
    }

    private static Thonk MakePrimitive(object value)
    {
        return new Thonk
        {
            _kind = ThonkKind.Primitive,
            _primitiveValue = value,
        };
    }

    public static Thonk Make<T>(T value) => Make(typeof(T), value);

    public static Thonk Make(object value) => Make(value.GetType(), value);

    public static Thonk Make(Type type, object? value)
    {
        if (type.IsArray)
        {
            return MakeArray(type.GetElementType()!, value!);
        }
        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            if (genericDefinition == typeof(List<>))
            {
                return MakeList(type.GenericTypeArguments[0], value!);
            }
            if (genericDefinition == typeof(HashSet<>))
            {
                return MakeHashSet(type.GenericTypeArguments[0], value!);
            }
            if (genericDefinition == typeof(Dictionary<,>))
            {
                return MakeDictionary(type.GenericTypeArguments[0], type.GenericTypeArguments[1], value!);
            }
        }
        if (type.IsClass)
        {
            return MakeClass(type, value!);
        }
        throw new WrongThonkException($"Can not make a Thonk from type {type}");
    }

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

            case ThonkKind.Class:
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

    public Thonk[] AsArray()
    {
        EnsureCompatibleWith<object[]>();
        return _listValues.ToArray();
    }

    public List<Thonk> AsList()
    {
        EnsureCompatibleWith<List<object>>();
        return _listValues.ToList();
    }

    public HashSet<Thonk> AsHashSet()
    {
        EnsureCompatibleWith<HashSet<object>>();
        return _listValues.ToHashSet(); // TODO: this requires Thonk to be equatable and hashed based on its content
    }

    public Dictionary<TKey, Thonk> AsDictionary<TKey>()
        where TKey : notnull
    {
        EnsureCompatibleWith<Dictionary<TKey, object>>();
        return _dictValues.Select(x => (x.Item1.As<TKey>(), x.Item2)).ToDictionary();
    }

    public Dictionary<Thonk, Thonk> AsDictionary()
    {
        EnsureCompatibleWith<Dictionary<object, object>>();
        // TODO: for this to work `Thonk` needs to be comparable and hashed based on its content. this might be very difficult?
        return _dictValues.ToDictionary();
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
            case ThonkKind.Array when !type.IsArray: return false;
            case ThonkKind.Array:
                return _listValues.Count == 0 || _listValues[0].IsCompatibleWith(type.GetElementType()!);
            case ThonkKind.List when !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)): return false;
            case ThonkKind.List:
                return _listValues.Count == 0 || _listValues[0].IsCompatibleWith(type.GenericTypeArguments[0]);
            case ThonkKind.HashSet when !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>)): return false;
            case ThonkKind.HashSet:
                return _listValues.Count == 0 || _listValues[0].IsCompatibleWith(type.GenericTypeArguments[0]);
            case ThonkKind.Dictionary when !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)): return false;
            case ThonkKind.Dictionary:
                return _dictValues.Count == 0 || (_dictValues[0].Item1.IsCompatibleWith(type.GenericTypeArguments[0])
                                                  && _dictValues[1].Item1.IsCompatibleWith(type.GenericTypeArguments[0]));
            case ThonkKind.Class when !type.IsClass:
                return false;
            case ThonkKind.Class:
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
        if (_kind != ThonkKind.Class) throw new WrongThonkException("This Thonk is not a class-like Thonk");
    }

    public IEnumerable<(string Name, Thonk Value)> EnumerateProperties()
    {
        EnsureClassLike();
        return _properties;
    }

    public bool HasProperty(string name)
    {
        return _propertiesDictionary.ContainsKey(name);
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
        EnsureHasProperty(name);
        return _propertiesDictionary[name];
    }

    public T Property<T>(string name)
    {
        EnsureClassLike();
        return Property(name).As<T>();
    }

    private void EnsureHasProperty(string name)
    {
        if (!_propertiesDictionary.ContainsKey(name))
        {
            throw new WrongThonkException($"This Thonk has no property named {name}");
        }
    }

    private void EnsureNotHasProperty(string name)
    {
        if (_propertiesDictionary.ContainsKey(name))
        {
            throw new WrongThonkException($"This Thonk already has a property named {name}");
        }
    }

    public void RenameProperty(string oldName, string newName)
    {
        EnsureClassLike();
        EnsureHasProperty(oldName);
        EnsureNotHasProperty(newName);

        _propertiesDictionary.Remove(oldName);
        for (var i = 0; i < _properties.Count; i++)
        {
            var (name, value) = _properties[i];
            if (name != oldName) continue;

            _properties[i] = (newName, value);
            _propertiesDictionary[newName] = value;
            return;
        }
        throw new UnreachableException();
    }

    public void MoveProperty(string name, int newPosition)
    {
        EnsureClassLike();
        EnsureHasProperty(name);
        var actualNewPosition = newPosition;
        if (actualNewPosition < 0)
        {
            actualNewPosition = _properties.Count - actualNewPosition;
        }
        if (actualNewPosition > _properties.Count - 1 || actualNewPosition < 0)
        {
            throw new WrongThonkException($"This Thonk can not move a property to position {newPosition}");
        }

        var oldPos = _properties.FindIndex(x => x.Name == name);
        Debug.Assert(oldPos >= 0);
        var value = _properties[oldPos];
        _properties.RemoveAt(oldPos);
        _properties.Insert(actualNewPosition, value);
    }

    public void AddProperty(string name, Thonk value)
    {
        AddProperty(name, -1, value);
    }

    public void AddProperty(string name, int position, Thonk value)
    {
        // what about position? 
        EnsureClassLike();
        EnsureNotHasProperty(name);

        var actualPosition = position;
        if (actualPosition < 0)
        {
            actualPosition = _properties.Count - actualPosition;
        }
        if (actualPosition > _properties.Count - 1 || actualPosition < 0)
        {
            throw new WrongThonkException($"This Thonk can not add a property at position {position}");
        }

        _propertiesDictionary[name] = value;
        _properties.Insert(actualPosition, (name, value));
    }

    public void AddProperty<T>(string name, T value)
    {
        EnsureClassLike();
        AddProperty(name, -1, Make(value));
    }

    public void AddProperty<T>(string name, int position, T value)
    {
        EnsureClassLike();
        EnsureNotHasProperty(name);
        AddProperty(name, position, Make(value));
    }


    public void RemoveProperty(string name)
    {
        EnsureClassLike();
        EnsureHasProperty(name);
        _propertiesDictionary.Remove(name);
        var pos = _properties.FindIndex(x => x.Name == name);
        Debug.Assert(pos >= 0);
        _properties.RemoveAt(pos);
    }

    public void SetProperty(string name, Thonk value)
    {
        EnsureClassLike();
        EnsureHasProperty(name);
        _propertiesDictionary[name] = value;
        for (var i = 0; i < _properties.Count; i++)
        {
            if (_properties[i].Name == name)
            {
                _properties[i] = (name, value);
                return;
            }
        }
        throw new UnreachableException();
    }


    public void SetProperty<T>(string name, T value)
    {
        EnsureClassLike();
        EnsureHasProperty(name);
        SetProperty(name, Make(value));
    }


    public ThonkSchema GetSchema()
    {
        // TODO
    }

    public static Thonk FromThonks(Thonk[] items)
    {
        return new Thonk
        {
            _kind = ThonkKind.Array,
            _listValues = items.ToList(),
        };
    }

    public static Thonk FromThonks(List<Thonk> items)
    {
        return new Thonk
        {
            _kind = ThonkKind.List,
            _listValues = items.ToList(),
        };
    }

    public static Thonk FromThonks(HashSet<Thonk> items)
    {
        return new Thonk
        {
            _kind = ThonkKind.HashSet,
            _listValues = items.ToList(),
        };
    }

    public static Thonk FromThonks(Dictionary<Thonk,Thonk> items)
    {
        return new Thonk
        {
            _kind = ThonkKind.HashSet,
            _dictValues = items.Select(x => (x.Key, x.Value)).ToList(),
        };
    }
}

public readonly struct ThonkSerializer : ISerializer
{
    private readonly ISerializer _serializer;

    public ThonkSerializer(ThonkSchema schema)
    {
        _serializer = schema.Kind switch
        {
            ThonkKind.Primitive => RaccSerializer.GetSerializer(schema.PrimitiveType!),
            ThonkKind.Array => new ThonkArraySerializer(schema),
            ThonkKind.List => new ThonkListSerializer(schema),
            ThonkKind.HashSet => new ThonkHashSetSerializer(schema),
            ThonkKind.Dictionary => new ThonkDictionarySerializer(schema),
            ThonkKind.Class => new ThonkClassSerializer(schema),
            _ => throw new UnreachableException(),
        };
    }

    public object Deserialize(Stream stream)
    {
        return _serializer.Deserialize(stream);
    }

    public void Serialize(Stream stream, object o)
    {
        _serializer.Serialize(stream, o);
    }

    public Type SerializedType => typeof(Thonk);
}

public class ThonkArraySerializer : ISerializer
{
    private readonly ISerializer _valueSerializer;
    private static readonly ValueSerializer<int> SizeSerializer = new();

    public ThonkArraySerializer(ThonkSchema schema)
    {
        _valueSerializer = new ThonkSerializer(schema.ArrayElementType!);
    }

    public Thonk Deserialize(Stream stream)
    {
        var size = SizeSerializer.Deserialize(stream);
        var items = new Thonk[size];
        for (var i = 0; i < size; i++)
        {
            items[i] = (Thonk)_valueSerializer.Deserialize(stream);
        }
        return Thonk.FromThonks(items);
    }

    object ISerializer.Deserialize(Stream stream)
    {

        return Deserialize(stream);
    }

    public void Serialize(Stream stream, Thonk thonkArray)
    {
        var array = thonkArray.AsArray();
        SizeSerializer.Serialize(stream, array.Length);
        foreach (var thonk in array)
        {
            _valueSerializer.Serialize(stream, thonk);
        }
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (Thonk)o);
    }

    public Type SerializedType => typeof(Thonk);
}

public class ThonkListSerializer : ISerializer
{
    private readonly ISerializer _valueSerializer;
    private static readonly ValueSerializer<int> SizeSerializer = new();

    public ThonkListSerializer(ThonkSchema schema)
    {
        _valueSerializer = new ThonkSerializer(schema.HashSetType);
    }

    public Thonk Deserialize(Stream stream)
    {
        var size = SizeSerializer.Deserialize(stream);
        var items = new List<Thonk>(size);
        for (var i = 0; i < size; i++)
        {
            items[i] = (Thonk)_valueSerializer.Deserialize(stream);
        }
        return Thonk.FromThonks(items);
    }

    object ISerializer.Deserialize(Stream stream)
    {

        return Deserialize(stream);
    }

    public void Serialize(Stream stream, Thonk thonkList)
    {
        var list = thonkList.AsList();
        SizeSerializer.Serialize(stream, list.Count);
        foreach (var thonk in list)
        {
            _valueSerializer.Serialize(stream, thonk);
        }
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (Thonk)o);
    }

    public Type SerializedType => typeof(Thonk);
}

public class ThonkHashSetSerializer : ISerializer
{
    private readonly ISerializer _valueSerializer;
    private static readonly ValueSerializer<int> SizeSerializer = new();

    public ThonkHashSetSerializer(ThonkSchema schema)
    {
        _valueSerializer = new ThonkSerializer(schema.HashSetType!);
    }

    public Thonk Deserialize(Stream stream)
    {
        var size = SizeSerializer.Deserialize(stream);
        var items = new Thonk[size];
        for (var i = 0; i < size; i++)
        {
            items[i] = (Thonk)_valueSerializer.Deserialize(stream);
        }
        return Thonk.FromThonks(items.ToHashSet());
    }

    object ISerializer.Deserialize(Stream stream)
    {

        return Deserialize(stream);
    }

    public void Serialize(Stream stream, Thonk thonkHashSet)
    {
        var hashSet = thonkHashSet.AsHashSet();
        SizeSerializer.Serialize(stream, hashSet.Count);
        foreach (var thonk in hashSet)
        {
            _valueSerializer.Serialize(stream, thonk);
        }
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (Thonk)o);
    }

    public Type SerializedType => typeof(Thonk);
}

public class ThonkDictionarySerializer : ISerializer
{
    private readonly ISerializer _keySerializer;
    private readonly ISerializer _valueSerializer;
    private static readonly ValueSerializer<int> SizeSerializer = new();

    public ThonkDictionarySerializer(ThonkSchema schema)
    {
        _keySerializer = new ThonkSerializer(schema.DictionaryKeyType!);
        _valueSerializer = new ThonkSerializer(schema.DictionaryValueType!);
    }

    public Thonk Deserialize(Stream stream)
    {
        var size = SizeSerializer.Deserialize(stream);
        var items = new Dictionary<Thonk, Thonk>(size);
        for (var i = 0; i < size; i++)
        {
            var key = (Thonk)_keySerializer.Deserialize(stream);
            var value = (Thonk)_valueSerializer.Deserialize(stream);
            items[key] = value;
        }
        return Thonk.FromThonks(items);
    }

    object ISerializer.Deserialize(Stream stream)
    {

        return Deserialize(stream);
    }

    public void Serialize(Stream stream, Thonk thonkDictionary)
    {
        var dictionary = thonkDictionary.AsDictionary();
        SizeSerializer.Serialize(stream, dictionary.Count);
        foreach (var (key, value) in dictionary)
        {
            _keySerializer.Serialize(stream, key);
            _valueSerializer.Serialize(stream, value);
        }
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (Thonk)o);
    }

    public Type SerializedType => typeof(Thonk);
}

public readonly struct ThonkClassSerializer : ISerializer
{
    private readonly ThonkSchema _schema;

    public ThonkClassSerializer(ThonkSchema schema)
    {
        _schema = schema;

    }

    public Thonk Deserialize(Stream stream)
    {
        var thonk = Thonk.Make(typeof(object));
        foreach (var (name, schema) in _schema.Properties)
        {
            var value = (new ThonkSerializer(schema).Deserialize(stream));
            thonk.AddProperty(name, value);
        }
        return thonk;
    }

    object ISerializer.Deserialize(Stream stream)
    {
        return Deserialize(stream);
    }

    public void Serialize(Stream stream, Thonk thonk)
    {
        foreach (var (name, schema) in _schema.Properties)
        {
            new ThonkSerializer(schema).Serialize(stream, thonk.Property(name));
        }
    }

    void ISerializer.Serialize(Stream stream, object o)
    {
        Serialize(stream, (Thonk)o);
    }

    public Type SerializedType => typeof(Thonk);
}

public class WrongThonkException : Exception
{
    public WrongThonkException(string msg) : base(msg)
    {
    }
}