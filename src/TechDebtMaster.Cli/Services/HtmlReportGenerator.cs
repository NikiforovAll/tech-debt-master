using System.Text;
using System.Text.Json;
using TechDebtMaster.Cli.Commands;

namespace TechDebtMaster.Cli.Services;

#pragma warning disable CA1305 // Specify IFormatProvider

public class HtmlReportGenerator : IHtmlReportGenerator
{
    private static readonly JsonSerializerOptions s_camelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public string GenerateReport(
        Dictionary<string, List<TechnicalDebtItemWithContent>> fileDebtMap,
        string repositoryName,
        DateTime analysisDate
    )
    {
        var totalItems = fileDebtMap.Values.Sum(items => items.Count);
        var allItems = fileDebtMap.Values.SelectMany(items => items).ToList();

        // Calculate statistics
        var severityStats = allItems
            .GroupBy(item => item.Severity)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        var tagStats = allItems
            .SelectMany(item => item.Tags)
            .GroupBy(tag => tag)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        // Prepare clean data for JavaScript (exclude markdown content to avoid JSON parsing issues)
        var cleanDebtData = fileDebtMap.ToDictionary(
            kvp => kvp.Key,
            kvp =>
                kvp.Value.Select(item => new
                    {
                        id = item.Id,
                        summary = item.Summary,
                        severity = item.Severity,
                        tags = item.Tags,
                        reference = item.Reference,
                        // markdownContent intentionally excluded
                    })
                    .ToList()
        );

        var debtData = JsonSerializer.Serialize(cleanDebtData, s_camelCaseOptions);

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine(
            "    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">"
        );
        html.AppendLine($"    <title>Technical Debt Report - {repositoryName}</title>");
        html.AppendLine(
            "    <script src=\"https://cdn.jsdelivr.net/npm/marked/marked.min.js\"></script>"
        );
        html.AppendLine("    <style>");
        html.AppendLine(GetStyles());
        html.AppendLine("    </style>");
        html.AppendLine("    <script type=\"application/json\" id=\"debt-state\">");
        html.AppendLine("        {\"hiddenItems\": {}, \"doneItems\": {}}");
        html.AppendLine("    </script>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"container\">");
        html.AppendLine("        <header>");
        html.AppendLine($"            <h1>Technical Debt Report</h1>");
        html.AppendLine($"            <div class=\"header-info\">");
        html.AppendLine($"                <span class=\"repo-name\">{repositoryName}</span>");
        html.AppendLine(
            $"                <span class=\"analysis-date\">Generated: {analysisDate:yyyy-MM-dd HH:mm:ss} UTC</span>"
        );
        html.AppendLine($"            </div>");
        html.AppendLine("        </header>");
        html.AppendLine();
        html.AppendLine("        <div class=\"summary-cards\">");
        html.AppendLine($"            <div class=\"summary-card\">");
        html.AppendLine($"                <div class=\"card-value\">{totalItems}</div>");
        html.AppendLine($"                <div class=\"card-label\">Total Debt Items</div>");
        html.AppendLine($"            </div>");
        html.AppendLine($"            <div class=\"summary-card\">");
        html.AppendLine($"                <div class=\"card-value\">{fileDebtMap.Count}</div>");
        html.AppendLine($"                <div class=\"card-label\">Files with Debt</div>");
        html.AppendLine($"            </div>");
        html.AppendLine($"            <div class=\"summary-card\">");
        html.AppendLine(
            $"                <div class=\"card-value\">{severityStats.GetValueOrDefault("Critical", 0)}</div>"
        );
        html.AppendLine($"                <div class=\"card-label\">Critical Issues</div>");
        html.AppendLine($"            </div>");
        html.AppendLine($"            <div class=\"summary-card\">");
        html.AppendLine($"                <div class=\"card-value\" id=\"done-count\">0</div>");
        html.AppendLine($"                <div class=\"card-label\">Completed Items</div>");
        html.AppendLine($"            </div>");
        html.AppendLine($"            <div class=\"summary-card\">");
        html.AppendLine($"                <div class=\"card-value\" id=\"hidden-count\">0</div>");
        html.AppendLine($"                <div class=\"card-label\">Hidden Items</div>");
        html.AppendLine($"            </div>");
        html.AppendLine("        </div>");
        html.AppendLine();
        html.AppendLine("        <div class=\"filters\">");
        html.AppendLine("            <div class=\"filter-group\">");
        html.AppendLine("                <label>Search:</label>");
        html.AppendLine(
            "                <input type=\"text\" id=\"search-input\" placeholder=\"Search debt items...\">"
        );
        html.AppendLine("            </div>");
        html.AppendLine("            <div class=\"filter-group\">");
        html.AppendLine("                <label>Filename:</label>");
        html.AppendLine(
            "                <input type=\"text\" id=\"filename-input\" placeholder=\"Filter by filename...\">"
        );
        html.AppendLine("            </div>");
        html.AppendLine("            <div class=\"filter-group\">");
        html.AppendLine("                <label>Severity:</label>");
        html.AppendLine("                <select id=\"severity-filter\">");
        html.AppendLine("                    <option value=\"\">All Severities</option>");
        html.AppendLine("                    <option value=\"Critical\">Critical</option>");
        html.AppendLine("                    <option value=\"High\">High</option>");
        html.AppendLine("                    <option value=\"Medium\">Medium</option>");
        html.AppendLine("                    <option value=\"Low\">Low</option>");
        html.AppendLine("                </select>");
        html.AppendLine("            </div>");
        html.AppendLine("            <div class=\"filter-group\">");
        html.AppendLine("                <label>Tag:</label>");
        html.AppendLine("                <select id=\"tag-filter\">");
        html.AppendLine("                    <option value=\"\">All Tags</option>");
        foreach (var tag in tagStats.Keys.OrderBy(t => t))
        {
            html.AppendLine($"                    <option value=\"{tag}\">{tag}</option>");
        }
        html.AppendLine("                </select>");
        html.AppendLine("            </div>");
        html.AppendLine("            <div class=\"filter-group\">");
        html.AppendLine(
            "                <button id=\"reset-filters\" class=\"btn btn-secondary\">Reset Filters</button>"
        );
        html.AppendLine(
            "                <button id=\"show-done\" class=\"btn btn-secondary\">Show Done</button>"
        );
        html.AppendLine(
            "                <button id=\"show-hidden\" class=\"btn btn-secondary\">Show Hidden</button>"
        );
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
        html.AppendLine();
        html.AppendLine("        <div class=\"stats-container\">");
        html.AppendLine("            <div class=\"chart-container\">");
        html.AppendLine("                <h3>Severity Distribution</h3>");
        html.AppendLine("                <div class=\"bar-chart\" id=\"severity-chart\"></div>");
        html.AppendLine("            </div>");
        html.AppendLine("            <div class=\"chart-container\">");
        html.AppendLine("                <h3>Top Tags</h3>");
        html.AppendLine("                <div class=\"bar-chart\" id=\"tag-chart\"></div>");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
        html.AppendLine();
        html.AppendLine("        <div id=\"debt-items\" class=\"debt-items\">");
        html.AppendLine("            <!-- Debt items will be populated by JavaScript -->");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");
        html.AppendLine();

        // Hidden storage for markdown content
        html.AppendLine("    <div id=\"markdown-storage\" style=\"display:none\">");
        foreach (var (filePath, items) in fileDebtMap)
        {
            foreach (var item in items)
            {
                var itemId = $"{filePath}-{item.Id}";
                var escapedContent = System.Web.HttpUtility.HtmlEncode(item.MarkdownContent);
                html.AppendLine(
                    $"        <div data-item-id=\"{System.Web.HttpUtility.HtmlAttributeEncode(itemId)}\">{escapedContent}</div>"
                );
            }
        }
        html.AppendLine("    </div>");
        html.AppendLine();

        html.AppendLine("    <script>");
        html.AppendLine($"        const debtData = {debtData};");
        html.AppendLine(
            "        const severityStats = " + JsonSerializer.Serialize(severityStats) + ";"
        );
        html.AppendLine(
            "        const tagStats = "
                + JsonSerializer.Serialize(
                    tagStats.Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                )
                + ";"
        );
        html.AppendLine();
        html.AppendLine(GetJavaScript());
        html.AppendLine("    </script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private static string GetStyles()
    {
        return @"
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background-color: #f5f7fa;
            color: #333;
            line-height: 1.6;
        }

        .container {
            max-width: 1600px;
            margin: 0 auto;
            padding: 20px;
        }

        header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            border-radius: 10px;
            margin-bottom: 30px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        }

        h1 {
            font-size: 2.5rem;
            margin-bottom: 10px;
        }

        .header-info {
            display: flex;
            justify-content: space-between;
            align-items: center;
            font-size: 1.1rem;
            opacity: 0.9;
        }

        .summary-cards {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .summary-card {
            background: white;
            padding: 25px;
            border-radius: 10px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
            text-align: center;
            transition: transform 0.2s;
        }

        .summary-card:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.15);
        }

        .card-value {
            font-size: 2.5rem;
            font-weight: bold;
            color: #667eea;
        }

        .card-label {
            color: #6b7280;
            margin-top: 5px;
        }

        .filters {
            background: white;
            padding: 20px;
            border-radius: 10px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
            margin-bottom: 30px;
            display: flex;
            gap: 20px;
            flex-wrap: wrap;
            align-items: center;
        }

        .filter-group {
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .filter-group label {
            font-weight: 500;
            color: #4b5563;
        }

        input[type=""text""],
        select {
            padding: 8px 12px;
            border: 1px solid #d1d5db;
            border-radius: 6px;
            font-size: 14px;
            background: white;
            transition: border-color 0.2s;
        }

        input[type=""text""]:focus,
        select:focus {
            outline: none;
            border-color: #667eea;
        }

        .btn {
            padding: 8px 16px;
            border: none;
            border-radius: 6px;
            font-size: 14px;
            cursor: pointer;
            transition: all 0.2s;
            font-weight: 500;
        }

        .btn-secondary {
            background: #6b7280;
            color: white;
        }

        .btn-secondary:hover {
            background: #4b5563;
        }

        .stats-container {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .chart-container {
            background: white;
            padding: 20px;
            border-radius: 10px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
        }

        .chart-container h3 {
            margin-bottom: 15px;
            color: #374151;
        }

        .bar-chart {
            display: flex;
            flex-direction: column;
            gap: 10px;
        }

        .bar-item {
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .bar-label {
            min-width: 100px;
            font-size: 14px;
            color: #4b5563;
        }

        .bar-container {
            flex: 1;
            background: #e5e7eb;
            border-radius: 4px;
            height: 24px;
            position: relative;
            overflow: hidden;
        }

        .bar-fill {
            height: 100%;
            background: #667eea;
            transition: width 0.3s ease;
            border-radius: 4px;
        }

        .bar-value {
            position: absolute;
            right: 8px;
            top: 50%;
            transform: translateY(-50%);
            font-size: 12px;
            font-weight: 500;
            color: #374151;
        }

        .debt-items {
            display: grid;
            gap: 20px;
        }

        .file-group {
            background: white;
            border-radius: 10px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
            overflow: hidden;
        }

        .file-header {
            background: #f3f4f6;
            padding: 15px 20px;
            font-weight: 600;
            color: #374151;
            border-bottom: 1px solid #e5e7eb;
            cursor: pointer;
            display: flex;
            justify-content: space-between;
            align-items: center;
            position: relative;
        }

        .file-header:hover {
            background: #e5e7eb;
        }
        
        .file-header::before {
            content: '\25BC';
            position: absolute;
            left: 8px;
            font-size: 12px;
        }
        
        .file-group.collapsed .file-header::before {
            transform: rotate(-90deg);
        }

        .file-path {
            font-family: 'Consolas', 'Monaco', monospace;
            font-size: 14px;
            margin-left: 20px;
        }
        
        .items-container {
            overflow: hidden;
        }
        
        .file-group.collapsed .items-container {
            display: none;
        }

        .file-count {
            background: #667eea;
            color: white;
            padding: 2px 8px;
            border-radius: 12px;
            font-size: 12px;
        }

        .debt-item {
            padding: 15px 20px;
            border-bottom: 1px solid #e5e7eb;
            transition: background 0.2s;
        }

        .debt-item:last-child {
            border-bottom: none;
        }

        .debt-item:hover {
            background: #f9fafb;
        }

        .debt-item.hidden {
            opacity: 0.5;
            background: #f3f4f6;
        }

        .debt-item.done {
            background: #f0fdf4;
            border-left: 4px solid #22c55e;
        }

        .debt-item.done .debt-id,
        .debt-item.done .debt-summary {
            text-decoration: line-through;
            opacity: 0.7;
        }

        .debt-item.done .severity-badge {
            opacity: 0.6;
        }

        .debt-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            margin-bottom: 10px;
        }

        .debt-info {
            flex: 1;
        }

        .debt-id {
            font-weight: 600;
            color: #374151;
            margin-bottom: 5px;
        }

        .debt-summary {
            color: #4b5563;
            line-height: 1.5;
        }

        .debt-meta {
            display: flex;
            gap: 10px;
            margin-top: 10px;
        }

        .severity-badge {
            padding: 4px 10px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: 500;
            text-transform: uppercase;
        }

        .severity-critical {
            background: #fee2e2;
            color: #dc2626;
        }

        .severity-high {
            background: #fed7aa;
            color: #ea580c;
        }

        .severity-medium {
            background: #fef3c7;
            color: #d97706;
        }

        .severity-low {
            background: #d1fae5;
            color: #059669;
        }

        .tag {
            padding: 4px 10px;
            background: #e0e7ff;
            color: #4338ca;
            border-radius: 4px;
            font-size: 12px;
        }

        .debt-actions {
            display: flex;
            gap: 10px;
        }

        .btn-hide {
            padding: 6px 12px;
            background: #ef4444;
            color: white;
            border: none;
            border-radius: 4px;
            font-size: 12px;
            cursor: pointer;
            transition: background 0.2s;
        }

        .btn-hide:hover {
            background: #dc2626;
        }

        .btn-unhide {
            background: #10b981;
        }

        .btn-unhide:hover {
            background: #059669;
        }

        .btn-done {
            padding: 6px 12px;
            background: #22c55e;
            color: white;
            border: none;
            border-radius: 4px;
            font-size: 12px;
            cursor: pointer;
            transition: background 0.2s;
        }

        .btn-done:hover {
            background: #16a34a;
        }

        .btn-undone {
            background: #6b7280;
        }

        .btn-undone:hover {
            background: #4b5563;
        }

        .empty-state {
            text-align: center;
            padding: 60px 20px;
            color: #6b7280;
        }

        .empty-state h3 {
            font-size: 1.5rem;
            margin-bottom: 10px;
        }

        .debt-content {
            margin-top: 15px;
            padding: 20px;
            background: #f9fafb;
            border-radius: 6px;
            border: 1px solid #e5e7eb;
            display: none;
        }

        .debt-content.expanded {
            display: block;
        }

        .btn-expand {
            padding: 4px 8px;
            background: #667eea;
            color: white;
            border: none;
            border-radius: 4px;
            font-size: 12px;
            cursor: pointer;
            transition: background 0.2s;
        }

        .btn-expand:hover {
            background: #5a67d8;
        }

        .btn-expand.expanded {
            background: #5a67d8;
        }

        /* Markdown content styles */
        .markdown-content {
            font-size: 14px;
            line-height: 1.6;
            color: #374151;
        }

        .markdown-content h1,
        .markdown-content h2,
        .markdown-content h3,
        .markdown-content h4,
        .markdown-content h5,
        .markdown-content h6 {
            margin-top: 1.5em;
            margin-bottom: 0.5em;
            font-weight: 600;
            color: #1f2937;
        }

        .markdown-content h1 { font-size: 1.5em; }
        .markdown-content h2 { font-size: 1.3em; }
        .markdown-content h3 { font-size: 1.1em; }

        .markdown-content p {
            margin-bottom: 1em;
        }

        .markdown-content ul,
        .markdown-content ol {
            margin-bottom: 1em;
            padding-left: 2em;
        }

        .markdown-content code {
            background: #e5e7eb;
            padding: 2px 4px;
            border-radius: 3px;
            font-family: 'Consolas', 'Monaco', monospace;
            font-size: 0.9em;
        }

        .markdown-content pre {
            background: #1f2937;
            color: #f3f4f6;
            padding: 15px;
            border-radius: 6px;
            overflow-x: auto;
            margin-bottom: 1em;
        }

        .markdown-content pre code {
            background: transparent;
            padding: 0;
            color: inherit;
        }

        .markdown-content blockquote {
            border-left: 4px solid #667eea;
            padding-left: 1em;
            margin-left: 0;
            margin-bottom: 1em;
            color: #6b7280;
        }

        .markdown-content table {
            border-collapse: collapse;
            width: 100%;
            margin-bottom: 1em;
        }

        .markdown-content th,
        .markdown-content td {
            border: 1px solid #e5e7eb;
            padding: 8px 12px;
            text-align: left;
        }

        .markdown-content th {
            background: #f3f4f6;
            font-weight: 600;
        }

        @media (max-width: 768px) {
            .header-info {
                flex-direction: column;
                align-items: flex-start;
                gap: 5px;
            }

            .filters {
                flex-direction: column;
                align-items: stretch;
            }

            .filter-group {
                width: 100%;
            }

            .filter-group input,
            .filter-group select {
                width: 100%;
            }

            .stats-container {
                grid-template-columns: 1fr;
            }

            .debt-header {
                flex-direction: column;
            }

            .debt-actions {
                margin-top: 10px;
            }
        }
        ";
    }

    private static string GetJavaScript()
    {
        return @"
        // State management - load from embedded JSON block
        function loadState() {
            const stateElement = document.getElementById('debt-state');
            try {
                return JSON.parse(stateElement.textContent);
            } catch {
                return {hiddenItems: {}, doneItems: {}};
            }
        }
        
        function saveState(state) {
            const stateElement = document.getElementById('debt-state');
            stateElement.textContent = JSON.stringify(state);
        }
        
        let currentState = loadState();
        let hiddenItems = currentState.hiddenItems || {};
        let doneItems = currentState.doneItems || {};
        let currentFilters = {
            search: '',
            filename: '',
            severity: '',
            tag: ''
        };
        let showHidden = false;
        let showDone = false;

        // Initialize
        document.addEventListener('DOMContentLoaded', () => {
            renderCharts();
            renderDebtItems();
            setupEventListeners();
            updateHiddenCount();
            updateDoneCount();
        });

        // Setup event listeners
        function setupEventListeners() {
            document.getElementById('search-input').addEventListener('input', (e) => {
                currentFilters.search = e.target.value.toLowerCase();
                renderDebtItems();
            });

            document.getElementById('filename-input').addEventListener('input', (e) => {
                currentFilters.filename = e.target.value.toLowerCase();
                renderDebtItems();
            });

            document.getElementById('severity-filter').addEventListener('change', (e) => {
                currentFilters.severity = e.target.value;
                renderDebtItems();
            });

            document.getElementById('tag-filter').addEventListener('change', (e) => {
                currentFilters.tag = e.target.value;
                renderDebtItems();
            });

            document.getElementById('reset-filters').addEventListener('click', () => {
                currentFilters = { search: '', filename: '', severity: '', tag: '' };
                document.getElementById('search-input').value = '';
                document.getElementById('filename-input').value = '';
                document.getElementById('severity-filter').value = '';
                document.getElementById('tag-filter').value = '';
                renderDebtItems();
            });

            document.getElementById('show-done').addEventListener('click', () => {
                showDone = !showDone;
                document.getElementById('show-done').textContent = showDone ? 'Hide Done' : 'Show Done';
                renderDebtItems();
            });

            document.getElementById('show-hidden').addEventListener('click', () => {
                showHidden = !showHidden;
                document.getElementById('show-hidden').textContent = showHidden ? 'Hide Hidden' : 'Show Hidden';
                renderDebtItems();
            });
        }

        // Render charts
        function renderCharts() {
            // Severity chart
            const severityChart = document.getElementById('severity-chart');
            severityChart.innerHTML = ''; // Clear existing chart items
            const severityOrder = ['Critical', 'High', 'Medium', 'Low'];
            const maxSeverityValue = Math.max(...Object.values(severityStats));
            
            severityOrder.forEach(severity => {
                const count = severityStats[severity] || 0;
                if (count > 0) {
                    const percentage = (count / maxSeverityValue) * 100;
                    const barItem = createBarItem(severity, count, percentage, `severity-${severity.toLowerCase()}`);
                    severityChart.appendChild(barItem);
                }
            });

            // Tag chart
            const tagChart = document.getElementById('tag-chart');
            tagChart.innerHTML = ''; // Clear existing chart items
            const maxTagValue = Math.max(...Object.values(tagStats));
            
            Object.entries(tagStats).forEach(([tag, count]) => {
                const percentage = (count / maxTagValue) * 100;
                const barItem = createBarItem(tag, count, percentage);
                tagChart.appendChild(barItem);
            });
        }

        // Create bar item
        function createBarItem(label, value, percentage, colorClass = '') {
            const barItem = document.createElement('div');
            barItem.className = 'bar-item';
            
            const barLabel = document.createElement('div');
            barLabel.className = 'bar-label';
            barLabel.textContent = label;
            
            const barContainer = document.createElement('div');
            barContainer.className = 'bar-container';
            
            const barFill = document.createElement('div');
            barFill.className = 'bar-fill';
            if (colorClass) {
                const colors = {
                    'severity-critical': '#dc2626',
                    'severity-high': '#ea580c',
                    'severity-medium': '#d97706',
                    'severity-low': '#059669'
                };
                barFill.style.background = colors[colorClass] || '#667eea';
            }
            barFill.style.width = `${percentage}%`;
            
            const barValue = document.createElement('div');
            barValue.className = 'bar-value';
            barValue.textContent = value;
            
            barContainer.appendChild(barFill);
            barContainer.appendChild(barValue);
            
            barItem.appendChild(barLabel);
            barItem.appendChild(barContainer);
            
            return barItem;
        }

        // Render debt items
        function renderDebtItems() {
            const container = document.getElementById('debt-items');
            container.innerHTML = '';
            
            let visibleItemsCount = 0;
            
            Object.entries(debtData).forEach(([filePath, items]) => {
                // Filter items
                const filteredItems = items.filter(item => {
                    const itemId = `${filePath}-${item.id}`;
                    const isHidden = hiddenItems[itemId];
                    const isDone = doneItems[itemId];
                    
                    if (!showHidden && isHidden) return false;
                    if (!showDone && isDone) return false;
                    
                    if (currentFilters.search) {
                        const searchTerm = currentFilters.search;
                        if (!item.id.toLowerCase().includes(searchTerm) &&
                            !item.summary.toLowerCase().includes(searchTerm) &&
                            !filePath.toLowerCase().includes(searchTerm)) {
                            return false;
                        }
                    }
                    
                    if (currentFilters.filename) {
                        const filenameTerm = currentFilters.filename;
                        if (!filePath.toLowerCase().includes(filenameTerm)) {
                            return false;
                        }
                    }
                    
                    if (currentFilters.severity && item.severity !== currentFilters.severity) {
                        return false;
                    }
                    
                    if (currentFilters.tag && !item.tags.includes(currentFilters.tag)) {
                        return false;
                    }
                    
                    return true;
                });
                
                if (filteredItems.length === 0) return;
                
                visibleItemsCount += filteredItems.length;
                
                // Create file group
                const fileGroup = document.createElement('div');
                fileGroup.className = 'file-group';
                
                // File header
                const fileHeader = document.createElement('div');
                fileHeader.className = 'file-header';
                
                const filePathElement = document.createElement('span');
                filePathElement.className = 'file-path';
                filePathElement.textContent = filePath;
                
                const fileCount = document.createElement('span');
                fileCount.className = 'file-count';
                fileCount.textContent = filteredItems.length;
                
                fileHeader.appendChild(filePathElement);
                fileHeader.appendChild(fileCount);
                
                // Add click handler to toggle collapse
                fileHeader.addEventListener('click', () => {
                    fileGroup.classList.toggle('collapsed');
                });
                
                fileGroup.appendChild(fileHeader);
                
                // Debt items
                const itemsContainer = document.createElement('div');
                itemsContainer.className = 'items-container';
                
                filteredItems.forEach(item => {
                    const debtItem = createDebtItem(filePath, item);
                    itemsContainer.appendChild(debtItem);
                });
                
                fileGroup.appendChild(itemsContainer);
                container.appendChild(fileGroup);
            });
            
            // Show empty state if no items
            if (visibleItemsCount === 0) {
                const emptyState = document.createElement('div');
                emptyState.className = 'empty-state';
                emptyState.innerHTML = `
                    <h3>No debt items found</h3>
                    <p>Try adjusting your filters or showing hidden items.</p>
                `;
                container.appendChild(emptyState);
            }
        }

        // Create debt item element
        function createDebtItem(filePath, item) {
            const itemId = `${filePath}-${item.id}`;
            const isHidden = hiddenItems[itemId];
            const isDone = doneItems[itemId];
            
            const debtItem = document.createElement('div');
            debtItem.className = `debt-item ${isHidden ? 'hidden' : ''} ${isDone ? 'done' : ''}`;
            
            const debtHeader = document.createElement('div');
            debtHeader.className = 'debt-header';
            
            const debtInfo = document.createElement('div');
            debtInfo.className = 'debt-info';
            
            const debtId = document.createElement('div');
            debtId.className = 'debt-id';
            debtId.textContent = item.id;
            
            const debtSummary = document.createElement('div');
            debtSummary.className = 'debt-summary';
            debtSummary.textContent = item.summary;
            
            const debtMeta = document.createElement('div');
            debtMeta.className = 'debt-meta';
            
            const severityBadge = document.createElement('span');
            severityBadge.className = `severity-badge severity-${item.severity.toLowerCase()}`;
            severityBadge.textContent = item.severity;
            debtMeta.appendChild(severityBadge);
            
            item.tags.forEach(tag => {
                const tagBadge = document.createElement('span');
                tagBadge.className = 'tag';
                tagBadge.textContent = tag;
                debtMeta.appendChild(tagBadge);
            });
            
            debtInfo.appendChild(debtId);
            debtInfo.appendChild(debtSummary);
            debtInfo.appendChild(debtMeta);
            
            const debtActions = document.createElement('div');
            debtActions.className = 'debt-actions';
            
            const expandBtn = document.createElement('button');
            expandBtn.className = 'btn-expand';
            expandBtn.textContent = 'View Details';
            expandBtn.onclick = () => toggleExpand(debtItem, expandBtn);
            debtActions.appendChild(expandBtn);
            
            const doneBtn = document.createElement('button');
            doneBtn.className = isDone ? 'btn-done btn-undone' : 'btn-done';
            doneBtn.textContent = isDone ? 'Mark as Todo' : 'Mark as Done';
            doneBtn.onclick = () => toggleDone(itemId);
            debtActions.appendChild(doneBtn);
            
            const hideBtn = document.createElement('button');
            hideBtn.className = isHidden ? 'btn-hide btn-unhide' : 'btn-hide';
            hideBtn.textContent = isHidden ? 'Unhide' : 'Mark as Irrelevant';
            hideBtn.onclick = () => toggleHidden(itemId);
            debtActions.appendChild(hideBtn);
            
            debtHeader.appendChild(debtInfo);
            debtHeader.appendChild(debtActions);
            
            debtItem.appendChild(debtHeader);
            
            // Add markdown content container
            const markdownStorage = document.getElementById('markdown-storage');
            const markdownElement = markdownStorage.querySelector(`[data-item-id=""${itemId}""]`);
            if (markdownElement) {
                const markdownContent = markdownElement.textContent;
                if (markdownContent) {
                    const contentDiv = document.createElement('div');
                    contentDiv.className = 'debt-content';
                    const markdownDiv = document.createElement('div');
                    markdownDiv.className = 'markdown-content';
                    // Decode HTML entities and parse markdown
                    const textarea = document.createElement('textarea');
                    textarea.innerHTML = markdownContent;
                    markdownDiv.innerHTML = marked.parse(textarea.value);
                    contentDiv.appendChild(markdownDiv);
                    debtItem.appendChild(contentDiv);
                }
            }
            
            return debtItem;
        }

        // Toggle expand state
        function toggleExpand(debtItem, expandBtn) {
            const contentDiv = debtItem.querySelector('.debt-content');
            if (contentDiv) {
                const isExpanded = contentDiv.classList.contains('expanded');
                contentDiv.classList.toggle('expanded');
                expandBtn.classList.toggle('expanded');
                expandBtn.textContent = isExpanded ? 'View Details' : 'Hide Details';
            }
        }

        // Toggle done state
        function toggleDone(itemId) {
            if (doneItems[itemId]) {
                delete doneItems[itemId];
            } else {
                doneItems[itemId] = true;
            }
            
            saveState({hiddenItems, doneItems});
            renderDebtItems();
            updateDoneCount();
        }

        // Toggle hidden state
        function toggleHidden(itemId) {
            if (hiddenItems[itemId]) {
                delete hiddenItems[itemId];
            } else {
                hiddenItems[itemId] = true;
            }
            
            saveState({hiddenItems, doneItems});
            renderDebtItems();
            updateHiddenCount();
        }

        // Update done count
        function updateDoneCount() {
            const count = Object.keys(doneItems).length;
            document.getElementById('done-count').textContent = count;
        }

        // Update hidden count
        function updateHiddenCount() {
            const count = Object.keys(hiddenItems).length;
            document.getElementById('hidden-count').textContent = count;
        }
        ";
    }
}
