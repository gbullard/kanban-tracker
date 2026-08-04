using System.Text;

namespace Kanban.Runner.Prompting;

public static class BranchNaming
{
    private const int MaxSlugLength = 40;

    public static string ForCard(int cardId, string title)
    {
        var slug = Slugify(title);
        if (slug.Length == 0)
        {
            slug = "task";
        }

        return $"card/{cardId}-{slug}";
    }

    private static string Slugify(string title)
    {
        var builder = new StringBuilder();

        foreach (var ch in title.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                // Anything else becomes a single separator; runs collapse because
                // we refuse to append a hyphen after a hyphen.
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');

        if (slug.Length > MaxSlugLength)
        {
            slug = slug[..MaxSlugLength].TrimEnd('-');
        }

        return slug;
    }
}