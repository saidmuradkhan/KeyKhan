namespace PassIn.Vault;

public static class EntrySearch
{
    /// <summary>
    /// Every whitespace-separated term must match a field. Title prefix matches rank first,
    /// then title matches, then matches on username, URL or folder; favorites win ties.
    /// </summary>
    public static IEnumerable<VaultEntry> Search(IEnumerable<VaultEntry> entries, string? query)
    {
        string[] terms = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return entries
            .Select(entry => (Entry: entry, Score: Score(entry, terms)))
            .Where(x => x.Score >= 0)
            .OrderBy(x => x.Score)
            .ThenByDescending(x => x.Entry.IsFavorite)
            .ThenBy(x => x.Entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.Entry);
    }

    private static int Score(VaultEntry entry, string[] terms)
    {
        int total = 0;
        foreach (string term in terms)
        {
            int score = TermScore(entry, term);
            if (score < 0)
            {
                return -1;
            }
            total += score;
        }
        return total;
    }

    private static int TermScore(VaultEntry entry, string term)
    {
        const StringComparison comparison = StringComparison.CurrentCultureIgnoreCase;

        if (entry.Title.StartsWith(term, comparison)) return 0;
        if (entry.Title.Contains(term, comparison)) return 1;
        if (entry.Username.Contains(term, comparison) || entry.Url.Contains(term, comparison)) return 2;
        if (entry.Folder.Contains(term, comparison)) return 3;
        return -1;
    }
}
