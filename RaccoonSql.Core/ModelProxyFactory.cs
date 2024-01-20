using Castle.DynamicProxy;

namespace RaccoonSql.Core;

internal static class ModelProxyFactory
{
    private static IProxyGenerator _proxyGenerator = new ProxyGenerator();
    private static IInterceptor _interceptor = new PropertyInterceptor();
    
    public static TModel GenerateProxy<TModel>(TModel source)
        where TModel : ModelBase
    {
        var model = (TModel) _proxyGenerator.CreateClassProxy(typeof(TModel), [typeof(IModelProxy)], _interceptor);

        AutoMapper.Map(source, model);
        
        model.Changes = new Dictionary<string, object>();
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
        
        model.Changes[methodName] = invocation.Arguments[0];
        invocation.Proceed();
    }
}

internal interface IModelProxy;