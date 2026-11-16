using System.Reflection;
using Magpie.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Magpie.Defaults;

public static class MediatorRegistration
{
    /// <summary>
    /// Registers the mediator and every request handler found in the given
    /// assemblies, so adding a new handler class needs no extra wiring line.
    /// </summary>
    public static IServiceCollection AddMagpieMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddScoped<IMediator, Magpie.Mediator.Mediator>();

        foreach (var type in assemblies.SelectMany(assembly => assembly.GetTypes()))
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            // A class may implement IRequestHandler<,> for one or more requests.
            foreach (var handlerInterface in type.GetInterfaces().Where(IsRequestHandler))
            {
                services.AddScoped(handlerInterface, type);
            }
        }

        return services;
    }

    private static bool IsRequestHandler(Type candidate) =>
        candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IRequestHandler<,>);
}
