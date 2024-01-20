using System.Linq.Expressions;

namespace RaccoonSql.Core;

internal static class AutoMapper
{
    private static readonly Dictionary<Type, List<Action<object, object>>> Mappings = new();
    private static readonly Dictionary<Type, Dictionary<string, Action<object, object>>> Setters = new();

    public static void ApplyChanges<T>(T t, Dictionary<string, object?> changes)
    {
        var setters = Setters[typeof(T)];
        foreach (var (key, value) in changes)
        {
            setters[key].Invoke(t!, value!);
        }
    }

    public static void Map<T>(T source, T target)
    {
        var type = typeof(T);
        if (!Mappings.TryGetValue(type, out var mappings))
        {
            mappings = [];
            var setters = new Dictionary<string, Action<object, object>>();
            var propertyInfos = type.GetProperties();

            foreach (var propertyInfo in propertyInfos)
            {
                var pSource = Expression.Parameter(type);
                var pTarget = Expression.Parameter(type);
                var getter = Expression.Call(pSource, propertyInfo.GetMethod!);
                var setter = Expression.Call(pTarget, propertyInfo.SetMethod!, getter);
                var action = Expression.Lambda<Action<T, T>>(setter, [pSource, pTarget]);

                var compiledCopy = action.Compile();
                var copyLambda = (object mSource, object mTarget) => compiledCopy((T)mSource, (T)mTarget);
                mappings.Add(copyLambda);

                var setterValue = Expression.Parameter(typeof(object));
                var castedValue = Expression.Convert(setterValue, propertyInfo.PropertyType);
                var valueSetter = Expression.Call(pTarget, propertyInfo.SetMethod!, castedValue);
                var setterAction = Expression.Lambda<Action<T, object>>(valueSetter, [pTarget, setterValue]);
                var compiledSetter = setterAction.Compile();
                var setterLambda = (object mTarget, object mValue) => compiledSetter((T)mTarget, mValue);

                setters[propertyInfo.SetMethod!.Name] = setterLambda;
            }

            Mappings[type] = mappings;
            Setters[type] = setters;
        }

        foreach (var mapping in mappings)
        {
            mapping(source!, target!);
        }
    }
}