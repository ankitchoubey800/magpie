namespace Magpie.Mediator;

/// <summary>
/// A request is a small object that says WHAT the caller wants. It carries the
/// input and names the type of the answer (TResult). It contains no logic.
/// </summary>
public interface IRequest<TResult>;

/// <summary>A request that CHANGES something. By convention it returns very little.</summary>
public interface ICommand<TResult> : IRequest<TResult>;

/// <summary>A request that only READS. It must never change anything.</summary>
public interface IQuery<TResult> : IRequest<TResult>;

/// <summary>The one class that knows how to carry out one kind of request.</summary>
public interface IRequestHandler<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    Task<TResult> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The switchboard. Callers hand it a request; it finds the matching handler
/// and returns the handler's answer. Callers never name a handler class.
/// </summary>
public interface IMediator
{
    Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default);
}
