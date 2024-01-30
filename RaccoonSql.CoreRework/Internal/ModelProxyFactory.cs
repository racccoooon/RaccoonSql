using System.Collections.Concurrent;
using System.Reflection;
using Castle.DynamicProxy;
using RaccoonSql.CoreRework.Internal.Utils;

namespace RaccoonSql.CoreRework.Internal;

internal static class ModelProxyFactory
{
    private static readonly ProxyGenerator ProxyGenerator = new();
    private static readonly PropertyInterceptor Interceptor = new();
    private static readonly ConcurrentDictionary<Type, Dictionary<MethodInfo, PropertyInfo>> SetterPropertyMapsByType = new(); 
    
    public static TModel GenerateProxy<TModel>(TModel source)
        where TModel : ModelBase
    {
        var model = (TModel) ProxyGenerator.CreateClassProxy(
            typeof(TModel),
            [typeof(IModelProxy)], 
            [Interceptor]);
        
        AutoMapper.Map(source, model);

        model.SetterPropertyMap = SetterPropertyMapsByType.GetOrAdd(typeof(TModel), MakeSetterPropertyMap);
        model.Changes = new Dictionary<PropertyInfo, object?>();
        model.TrackChanges = true;

        return model;
    }

    private static Dictionary<MethodInfo, PropertyInfo> MakeSetterPropertyMap(Type modelType)
    {
        var result = new Dictionary<MethodInfo, PropertyInfo>();

        foreach (var propertyInfo in modelType.GetProperties())
        {
            result[propertyInfo.SetMethod!] = propertyInfo;
        }
        
        return result;
    }
}

public class PropertyInterceptor : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        var model = (ModelBase)invocation.Proxy;
        if (!model.TrackChanges)
        {
            invocation.Proceed();
            return;
        }
        
        var methodName = invocation.Method.Name;
        if (!model.SetterPropertyMap.TryGetValue(invocation.Method, out var propertyInfo))
        {
            invocation.Proceed();
            return;
        }

        if (model.OnChange != null)
        {
            model.OnChange.Invoke(model.Id);
            model.OnChange = null;
        }

        model.Changes[propertyInfo] = invocation.Arguments[0];
        
        invocation.Proceed();
    }
}

public interface IModelProxy;