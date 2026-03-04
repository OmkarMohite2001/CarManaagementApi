using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Shared;

public static class IdGenerator
{
    public static async Task<string> NextAsync(IQueryable<string> idQuery, string prefix, int startAt = 1001)
    {
        var values = await idQuery.ToListAsync();
        var max = values
            .Select(x => TryParseSuffix(x, prefix))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty(startAt - 1)
            .Max();

        return $"{prefix}-{max + 1}";
    }

    private static int? TryParseSuffix(string value, string prefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var pattern = $"^{Regex.Escape(prefix)}-(\\d+)$";
        var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var number) ? number : null;
    }
}
