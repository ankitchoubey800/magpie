using System.Collections.Concurrent;

namespace Magpie.Mediator;

/// <summary>
/// About forty lines, on purpose. Popular mediator libraries add many features
/// (and, since 2025, commercial licences). We need exactly one: "find the
/// handler for this request and call it".
/// </summary>
public sealed class Mediator(IServiceProvider services) : IMediator
{
    // Working out "which handler type fits this request type" needs reflection,
    // which is slow. We do it once per request type and remember the answer.
    private static readonly ConcurrentDictionary<Type, object> Invokers = new();

    public Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoker = (Invoker<TResult>)Invokers.GetOrAdd(
            request.GetType(),
            static (requestType, resultType) =>
                Activator.CreateInstance(typeof(Invoker<,>).MakeGenericType(requestType, resultType))!,
            typeof(TResult));

        return invoker.InvokeAsync(services, request, cancellationToken);
    }

    private abstract class Invoker<TResult>
    {
        public abstract Task<TResult> InvokeAsync(
            IServiceProvider services, IRequest<TResult> request, CancellationToken cancellationToken);
    }

    private sealed class Invoker<TRequest, TResult> : Invoker<TResult>
        where TRequest : IRequest<TResult>
    {
        public override Task<TResult> InvokeAsync(
            IServiceProvider services, IRequest<TResult> request, CancellationToken cancellationToken)
        {
            // Ask the container for the handler. If nobody registered one, say so clearly.
            var handler = (IRequestHandler<TRequest, TResult>?)services.GetService(typeof(IRequestHandler<TRequest, TResult>))
                ?? throw new InvalidOperationException(
                    $"No handler is registered for {typeof(TRequest).Name}. " +
                    "Did you pass its assembly to AddMagpieMediator(...)?");

            return handler.HandleAsync((TRequest)request, cancellationToken);
        }
    }
}
