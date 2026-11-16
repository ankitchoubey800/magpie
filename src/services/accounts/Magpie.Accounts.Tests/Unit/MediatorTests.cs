using Magpie.Defaults;
using Magpie.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Magpie.Accounts.Tests.Unit;

public class MediatorTests
{
    // A tiny request and handler that exist only for these tests.
    public sealed record AddNumbers(int Left, int Right) : IQuery<int>;

    public sealed class AddNumbersHandler : IRequestHandler<AddNumbers, int>
    {
        public Task<int> HandleAsync(AddNumbers request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Left + request.Right);
    }

    public sealed record NobodyHandlesThis : ICommand<string>;

    private static IMediator BuildMediator()
    {
        var services = new ServiceCollection();
        services.AddMagpieMediator(typeof(MediatorTests).Assembly);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task A_request_reaches_its_handler_and_the_answer_comes_back()
    {
        var answer = await BuildMediator().SendAsync(new AddNumbers(2, 3));

        Assert.Equal(5, answer);
    }

    [Fact]
    public async Task The_same_kind_of_request_can_be_sent_many_times()
    {
        var mediator = BuildMediator();

        Assert.Equal(3, await mediator.SendAsync(new AddNumbers(1, 2)));
        Assert.Equal(30, await mediator.SendAsync(new AddNumbers(10, 20)));
    }

    [Fact]
    public async Task A_request_with_no_handler_fails_with_a_message_that_names_it()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => BuildMediator().SendAsync(new NobodyHandlesThis()));

        Assert.Contains(nameof(NobodyHandlesThis), error.Message);
    }
}
