using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using TechDebtMaster.Cli.Services;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Services.Analysis.Handlers;
using TechDebtMaster.Cli.Tools.Responses;
using TechDebtMaster.Cli.Tools.Shared;
using TechDebtMaster.Cli.Tools.Utils;
using TechDebtMaster.Cli.Utilities;

namespace TechDebtMaster.Cli.Tools;

[McpServerToolType]
public static class TechDebtManagementTools
{
    [
        McpServerTool(Name = "tdm-remove-debt-item"),
        Description(
            "Remove a specific technical debt item by its ID in the format 'filePath:id'. This will delete both the debt item metadata from analysis and its associated content file."
        )
    ]
    public static async Task<RemoveDebtResponse> RemoveDebt(
        [FromServices] IRepositoryPathProvider pathProvider,
        [FromServices] IAnalysisService analysisService,
        [FromServices] ITechDebtStorageService techDebtStorage,
        [Description(
            "The debt item identifier in the format 'filePath:id' (e.g., 'src/MyClass.cs:DEBT001')"
        )]
            string debtId
    )
    {
        var repositoryPath = pathProvider.RepositoryPath;

        // Validate and parse the debt ID format
        if (!TechDebtToolsUtils.TryParseDebtId(debtId, out var targetFilePath, out var targetItemId))
        {
            return new RemoveDebtResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Success = false,
                Error =
                    "Invalid debt ID format. Expected 'filePath:id' (e.g., 'src/MyClass.cs:DEBT001')",
            };
        }

        // Load analysis report
        var analysisReport = await analysisService.LoadAnalysisReportAsync(repositoryPath);
        if (analysisReport == null)
        {
            return new RemoveDebtResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Success = false,
                Error =
                    "No analysis data found. Run 'analyze debt <path>' first to generate analysis data.",
            };
        }

        // Extract debt items from analysis report
        var fileDebtMap = AnalysisResultUtils.ExtractDebtItems(analysisReport);
        var debtItems = TechDebtToolsUtils.ExtractDebtItemsWithFilePath(fileDebtMap);

        // Find the specific debt item
        var targetItem = TechDebtToolsUtils.FindSpecificDebtItem(debtItems, targetFilePath, targetItemId);
        if (targetItem == null)
        {
            return new RemoveDebtResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Success = false,
                Error =
                    $"Debt item '{debtId}' not found. Use tdm-list-file-issues to list available items.",
            };
        }

        var result = new RemoveDebtResponse
        {
            RepositoryPath = repositoryPath,
            DebtId = debtId,
            FilePath = targetItem.FilePath,
            ItemId = targetItem.DebtItem.Id,
        };

        try
        {
            // Step 1: Try to delete the detailed content file
            try
            {
                result.ContentDeleted = await techDebtStorage.DeleteAsync(
                    targetItem.DebtItem.Reference
                );
            }
            catch (FileNotFoundException)
            {
                result.ContentDeleted = false;
            }
            catch (UnauthorizedAccessException)
            {
                result.ContentDeleted = false;
            }
            catch (IOException)
            {
                result.ContentDeleted = false;
            }

            // Step 2: Remove the debt item from the analysis report
            if (
                analysisReport.FileHistories.TryGetValue(targetItem.FilePath, out var fileHistory)
                && fileHistory.Current.AnalysisResults.TryGetValue(
                    TechDebtAnalysisHandler.ResultKey,
                    out var resultObj
                )
            )
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                };

                var json = JsonSerializer.Serialize(resultObj, jsonOptions);
                var techDebtResult = JsonSerializer.Deserialize<TechDebtAnalysisResult>(
                    json,
                    jsonOptions
                );

                if (techDebtResult?.Items != null)
                {
                    var originalCount = techDebtResult.Items.Count;

                    // Remove the specific debt item
                    techDebtResult.Items.RemoveAll(item =>
                        string.Equals(
                            item.Id,
                            targetItem.DebtItem.Id,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                    // Check if we actually removed an item
                    if (techDebtResult.Items.Count < originalCount)
                    {
                        // Update the analysis results
                        fileHistory.Current.AnalysisResults[TechDebtAnalysisHandler.ResultKey] =
                            techDebtResult;

                        // Save the updated analysis report
                        await analysisService.SaveAnalysisReportAsync(
                            repositoryPath,
                            analysisReport
                        );
                        result.MetadataDeleted = true;
                    }
                }
            }

            result.Success = result.ContentDeleted || result.MetadataDeleted;

            if (result.Success)
            {
                if (result.ContentDeleted && result.MetadataDeleted)
                {
                    result.Message =
                        $"Successfully removed debt item '{targetItem.DebtItem.Id}' and its content.";
                }
                else if (result.MetadataDeleted)
                {
                    result.Message =
                        $"Successfully removed debt item '{targetItem.DebtItem.Id}' from analysis (content was already missing).";
                }
                else
                {
                    result.Message =
                        $"Successfully deleted content for '{targetItem.DebtItem.Id}' (metadata will be cleaned up later).";
                }
            }
            else
            {
                result.Error =
                    $"Failed to remove debt item '{targetItem.DebtItem.Id}'. The item may have already been deleted or is inaccessible.";
            }

            return result;
        }
        catch (InvalidOperationException ex)
        {
            return new RemoveDebtResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Success = false,
                Error = $"An error occurred while removing the debt item: {ex.Message}",
                FilePath = targetItem.FilePath,
                ItemId = targetItem.DebtItem.Id,
            };
        }
        catch (JsonException ex)
        {
            return new RemoveDebtResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Success = false,
                Error = $"Failed to process analysis data: {ex.Message}",
                FilePath = targetItem.FilePath,
                ItemId = targetItem.DebtItem.Id,
            };
        }
    }
}
