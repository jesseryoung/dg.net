using Microsoft.Extensions.DependencyInjection;

namespace DG.Net.Handlers;

public interface IPluginEventHandler<TMessage>
{
    ValueTask Handle(TMessage message);
}


public static class PluginEventHandlerExtensions
{
    public static IServiceCollection AddPluginEventHandlers(this IServiceCollection services)
    {
        var pluginHandlerType = typeof(IPluginEventHandler<>);

        var handlers = typeof(PluginEventHandlerExtensions)
            .Assembly
            .GetTypes()
            .Where(t => !t.IsGenericTypeDefinition)
            .SelectMany(t =>
                t.GetInterfaces()
                    .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == pluginHandlerType)
                    .Select(x => new
                    {
                        ImplementationType = t,
                        ServiceType = x
                    })
            ).ToList();

        foreach (var handler in handlers)
        {
            services.AddTransient(handler.ServiceType, handler.ImplementationType);
        }

        return services;
    }

    public static IServiceCollection AddGameRules(this IServiceCollection services)
    {
        var ruleType = typeof(IGameRule);

        var handlers = typeof(PluginEventHandlerExtensions)
            .Assembly
            .GetTypes()
            .SelectMany(t =>
                t.GetInterfaces()
                    .Where(x => x.IsAssignableFrom(ruleType))
                    .Select(x => new
                    {
                        ImplementationType = t,
                        ServiceType = x
                    })
            ).ToList();

        foreach (var handler in handlers)
        {
            services.AddTransient(handler.ServiceType, handler.ImplementationType);
        }

        return services;
    }
}