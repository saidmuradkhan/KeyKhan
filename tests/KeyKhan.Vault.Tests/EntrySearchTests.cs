namespace KeyKhan.Vault.Tests;

public sealed class EntrySearchTests
{
    private static readonly VaultEntry[] Entries =
    [
        new() { Title = "My GitHub", Username = "octo" },
        new() { Title = "GitHub", Username = "work" },
        new() { Title = "Mail", Username = "github-bot@example.com" },
        new() { Title = "Bank", Folder = "Finance" },
    ];

    [Fact]
    public void Ranks_title_prefix_before_contains_before_other_fields()
    {
        var titles = EntrySearch.Search(Entries, "git").Select(e => e.Title);

        Assert.Equal(["GitHub", "My GitHub", "Mail"], titles);
    }

    [Fact]
    public void All_terms_must_match() =>
        Assert.Equal("GitHub", Assert.Single(EntrySearch.Search(Entries, "github work")).Title);

    [Fact]
    public void Matches_folder() =>
        Assert.Equal("Bank", Assert.Single(EntrySearch.Search(Entries, "finance")).Title);

    [Fact]
    public void Empty_query_returns_everything_with_favorites_first()
    {
        var entries = Entries.Append(new VaultEntry { Title = "Zoo", IsFavorite = true });

        Assert.Equal("Zoo", EntrySearch.Search(entries, "  ").First().Title);
    }
}
