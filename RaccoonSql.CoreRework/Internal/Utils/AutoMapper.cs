using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace RaccoonSql.CoreRework.Internal.Utils;

internal static class AutoMapper
{
    private static readonly Dictionary<Type, List<Action<object, object>>> Mappings = new();
    private static readonly Dictionary<Type, Dictionary<string, Action<object, object>>> Setters = new();

    public static void ApplyChanges<T>(T t, Dictionary<string, object?> changes)
    {
        var type = typeof(T);
        if (!Setters.TryGetValue(type, out var setters))
        {
            setters = [];
            var propertyInfos = type.GetProperties();

            foreach (var propertyInfo in propertyInfos)
            {
                var pTarget = Expression.Parameter(typeof(object));
                var setterValue = Expression.Parameter(typeof(object));
                var castedValue = Expression.Convert(setterValue, propertyInfo.PropertyType);
                var valueSetter = Expression.Call(Expression.Convert(pTarget, type), propertyInfo.SetMethod!, castedValue);
                var setterAction = Expression.Lambda<Action<object, object>>(valueSetter, [pTarget, setterValue]);
                var compiledSetter = setterAction.Compile();

                setters[propertyInfo.Name] = compiledSetter;
            }

            Setters[type] = setters;
        }
        
        foreach (var (key, value) in changes)
        {
            setters[key].Invoke(t!, value!);
        }
    }

    public static T Clone<T>(T source)
    {
        var cloned = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        Map(source, cloned);
        return cloned;
    }

    public static void Map<T>(T source, T target)
    {
        var type = typeof(T);
        if (!Mappings.TryGetValue(type, out var mappings))
        {
            mappings = [];
            var propertyInfos = type.GetProperties();

            foreach (var propertyInfo in propertyInfos)
            {
                var pSource = Expression.Parameter(typeof(object));
                var pTarget = Expression.Parameter(typeof(object));
                var getter = Expression.Call(
                    Expression.Convert(pSource, typeof(T)), 
                    propertyInfo.GetMethod!);
                var setter = Expression.Call(
                    Expression.Convert(pTarget, typeof(T)),
                    propertyInfo.SetMethod!, getter);
                var action = Expression.Lambda<Action<object, object>>(setter, [pSource, pTarget]);
                var compiledCopy = action.Compile();
                
                mappings.Add(compiledCopy);
                
            }

            Mappings[type] = mappings;
        }

        foreach (var mapping in mappings)
        {
            mapping(source!, target!);
        }
    }
}