using Castle.DynamicProxy;
using RaccoonSql.CoreRework.Internal.Utils;

namespace RaccoonSql.CoreRework.Internal;

internal static class ModelProxyFactory
{
    private static readonly ProxyGenerator ProxyGenerator = new();
    private static readonly PropertyInterceptor Interceptor = new();
    
    public static TModel GenerateProxy<TModel>(TModel source)
        where TModel : ModelBase
    {
        var model = (TModel) ProxyGenerator.CreateClassProxy(
            typeof(TModel),
            [typeof(IModelProxy)], 
            [Interceptor]);

        
        AutoMapper.Map(source, model);
        
        model.Changes = new Dictionary<string, object?>();
        model.TrackChanges = true;

        return model;
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
        if (!methodName.StartsWith("set_"))
        {
            invocation.Proceed();
            return;
        }

        if (model.OnChange != null)
        {
            model.OnChange.Invoke(model.Id);
            model.OnChange = null;
        }

        model.Changes[methodName[4..]] = invocation.Arguments[0];
        
        invocation.Proceed();
    }
}

public interface IModelProxy;