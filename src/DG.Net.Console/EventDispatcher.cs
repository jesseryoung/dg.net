using DG.Net.Handlers;
using Microsoft.Extensions.Logging;

namespace DG.Net;

public class EventDispatcher
{
    private readonly IServiceProvider services;
    private readonly ILogger<EventDispatcher> logger;

    public EventDispatcher(IServiceProvider services, ILogger<EventDispatcher> logger)
    {
        this.services = services;
        this.logger = logger;
    }

    public async ValueTask HandleMessage(string rawMessage)
    {
        var message = EventMessage.Deserialize(rawMessage);
        var eventHandlerType = typeof(IPluginEventHandler<>).MakeGenericType(message.GetType());
        var eventHandler = this.services.GetService(eventHandlerType);

        if (eventHandler is null)
        {
            this.logger.LogInformation($"No handler found for event: {message.event_name}");
        }
        else
        {
            var handleMethod = eventHandler.GetType().GetMethod(nameof(IPluginEventHandler<EventMessage>.Handle))!;
            await (ValueTask)handleMethod.Invoke(eventHandler, new[] { message })!;
        }
    }
}