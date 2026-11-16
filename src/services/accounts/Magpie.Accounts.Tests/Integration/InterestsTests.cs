using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Magpie.Accounts.Application.Interests;

namespace Magpie.Accounts.Tests.Integration;

[Collection(AccountsApiCollection.Name)]
public class InterestsTests(AccountsApiFactory factory)
{
    private const string Password = "correct horse battery";

    private static string NewEmail() => $"reader-{Guid.NewGuid():N}@example.com";

    private async Task<ApiSession> NewSignedInReaderAsync(string? email = null)
    {
        var session = new ApiSession(factory.CreateBrowserLikeClient());
        await session.SignUpAndSignInAsync(factory.Emails, email ?? NewEmail(), Password);
        return session;
    }

    private static async Task<int[]> MyInterestsAsync(ApiSession session) =>
        [.. (await session.Client.GetFromJsonAsync<MyInterests>("/api/accounts/me/interests"))!.CategoryIds];

    private static async Task AssertInterestsAsync(ApiSession session, params int[] expected) =>
        Assert.Equal(expected, await MyInterestsAsync(session));

    [Fact]
    public async Task The_category_tree_has_six_groups_of_four_topics_from_the_migration()
    {
        using var client = factory.CreateBrowserLikeClient();

        var groups = await client.GetFromJsonAsync<List<CategoryGroup>>("/api/accounts/categories");

        Assert.Equal(6, groups!.Count);
        Assert.All(groups, group => Assert.Equal(4, group.Topics.Count));
        Assert.Equal("Technology", groups[0].Name);
        Assert.Equal("programming", groups[0].Topics[0].Slug);
    }

    [Fact]
    public async Task A_new_reader_has_no_interests_yet()
    {
        var session = await NewSignedInReaderAsync();

        Assert.Empty(await MyInterestsAsync(session));
    }

    [Fact]
    public async Task Saved_interests_come_back_and_survive_signing_out_and_in_again()
    {
        var email = NewEmail();
        var session = await NewSignedInReaderAsync(email);

        var save = await session.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 103, 101, 204 } });
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        await AssertInterestsAsync(session, 101, 103, 204);

        await session.PostAsync("/api/accounts/sign-out");
        var later = new ApiSession(factory.CreateBrowserLikeClient());
        await later.PostAsync("/api/accounts/sign-in", new { email, password = Password });

        await AssertInterestsAsync(later, 101, 103, 204);
    }

    [Fact]
    public async Task Saving_again_replaces_the_whole_set()
    {
        var session = await NewSignedInReaderAsync();
        await session.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 101, 102, 103 } });

        await session.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 103, 401, 402, 403 } });

        await AssertInterestsAsync(session, 103, 401, 402, 403);
    }

    [Fact]
    public async Task Sending_the_same_choice_twice_is_harmless()
    {
        var session = await NewSignedInReaderAsync();
        var body = new { categoryIds = new[] { 101, 102, 103 } };

        var first = await session.PutAsync("/api/accounts/me/interests", body);
        var second = await session.PutAsync("/api/accounts/me/interests", body);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        await AssertInterestsAsync(session, 101, 102, 103);
    }

    [Theory]
    [InlineData(new[] { 101, 102 }, "at least 3")]          // too few
    [InlineData(new[] { 101, 102, 999 }, "do not exist")]   // made-up topic
    [InlineData(new[] { 101, 102, 1 }, "do not exist")]     // 1 is a GROUP, not a topic
    public async Task An_invalid_choice_is_refused_and_changes_nothing(int[] categoryIds, string expectedMessage)
    {
        var session = await NewSignedInReaderAsync();
        await session.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 201, 202, 203 } });

        var response = await session.PutAsync("/api/accounts/me/interests", new { categoryIds });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(expectedMessage, problem.GetProperty("errors").GetProperty("categoryIds")[0].GetString());
        await AssertInterestsAsync(session, 201, 202, 203);
    }

    [Fact]
    public async Task One_readers_choices_are_invisible_to_another()
    {
        var asha = await NewSignedInReaderAsync();
        var ben = await NewSignedInReaderAsync();

        await asha.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 101, 102, 103 } });

        Assert.Empty(await MyInterestsAsync(ben));
    }

    [Fact]
    public async Task Interests_need_a_signed_in_reader()
    {
        var anonymous = new ApiSession(factory.CreateBrowserLikeClient());

        var read = await anonymous.Client.GetAsync("/api/accounts/me/interests");
        var write = await anonymous.PutAsync("/api/accounts/me/interests", new { categoryIds = new[] { 101, 102, 103 } });

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
    }

    [Fact]
    public async Task Changing_interests_needs_the_antiforgery_token_like_every_other_change()
    {
        var session = await NewSignedInReaderAsync();

        var response = await session.Client.PutAsJsonAsync("/api/accounts/me/interests", new { categoryIds = new[] { 101, 102, 103 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("antiforgery", problem.GetProperty("code").GetString());
    }
}
