using System.Text.RegularExpressions;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Tools.Shared;

namespace TechDebtMaster.Cli.Tools.Utils;

public static class TechDebtToolsUtils
{
    public static bool TryParseDebtId(string debtId, out string filePath, out string itemId)
    {
        filePath = string.Empty;
        itemId = string.Empty;

        if (string.IsNullOrWhiteSpace(debtId))
        {
            return false;
        }

        var lastColonIndex = debtId.LastIndexOf(':');
        if (lastColonIndex <= 0 || lastColonIndex == debtId.Length - 1)
        {
            return false;
        }

        filePath = debtId[..lastColonIndex];
        itemId = debtId[(lastColonIndex + 1)..];

        return !string.IsNullOrWhiteSpace(filePath) && !string.IsNullOrWhiteSpace(itemId);
    }

    public static List<DebtItemWithFile> ExtractDebtItemsWithFilePath(
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap
    )
    {
        var debtItems = new List<DebtItemWithFile>();

        foreach (var (filePath, items) in fileDebtMap)
        {
            foreach (var item in items)
            {
                debtItems.Add(new DebtItemWithFile { DebtItem = item, FilePath = filePath });
            }
        }

        return debtItems;
    }

    public static DebtItemWithFile? FindSpecificDebtItem(
        List<DebtItemWithFile> debtItems,
        string targetFilePath,
        string targetItemId
    )
    {
        // First try exact file path match
        var exactMatch = debtItems.FirstOrDefault(item =>
            string.Equals(item.FilePath, targetFilePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.DebtItem.Id, targetItemId, StringComparison.OrdinalIgnoreCase)
        );

        if (exactMatch != null)
        {
            return exactMatch;
        }

        // Try relative path match (match end of file path)
        var relativeMatch = debtItems.FirstOrDefault(item =>
            item.FilePath.EndsWith(targetFilePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.DebtItem.Id, targetItemId, StringComparison.OrdinalIgnoreCase)
        );

        return relativeMatch;
    }

    public static Dictionary<string, List<TechnicalDebtItem>> ApplyFiltering(
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap,
        string? includePattern,
        string? excludePattern,
        string? severityFilter,
        string? tagFilter
    )
    {
        var filteredMap = new Dictionary<string, List<TechnicalDebtItem>>();

        Regex? includeRegex = null;
        Regex? excludeRegex = null;
        DebtSeverity? parsedSeverity = null;
        DebtTag? parsedTag = null;

        // Parse filters
        if (!string.IsNullOrWhiteSpace(includePattern))
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                includeRegex = new Regex(includePattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                // Invalid regex, ignore
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        if (!string.IsNullOrWhiteSpace(excludePattern))
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                excludeRegex = new Regex(excludePattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                // Invalid regex, ignore
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        if (!string.IsNullOrWhiteSpace(severityFilter))
        {
            Enum.TryParse<DebtSeverity>(severityFilter, true, out var severity);
            parsedSeverity = severity;
        }

        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            Enum.TryParse<DebtTag>(tagFilter, true, out var tag);
            parsedTag = tag;
        }

        foreach (var (filePath, debtItems) in fileDebtMap)
        {
            // Apply file pattern filtering
            if (includeRegex != null && !includeRegex.IsMatch(filePath))
            {
                continue;
            }

            if (excludeRegex != null && excludeRegex.IsMatch(filePath))
            {
                continue;
            }

            // Apply item filtering
            var filteredItems = debtItems
                .Where(item =>
                {
                    if (parsedSeverity.HasValue && item.Severity != parsedSeverity.Value)
                    {
                        return false;
                    }

                    if (parsedTag.HasValue && !item.Tags.Contains(parsedTag.Value))
                    {
                        return false;
                    }

                    return true;
                })
                .ToList();

            filteredMap[filePath] = filteredItems;
        }

        return filteredMap;
    }
}
