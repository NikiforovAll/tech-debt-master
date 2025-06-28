namespace TechDebtMaster.Cli.Services;

/// <summary>
/// Service for managing walkthrough files, including ensuring default walkthrough is available
/// </summary>
public class WalkthroughService : IWalkthroughService
{
    private readonly string _walkthroughDirectory;

    public WalkthroughService()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var techDebtMasterDirectory = Path.Combine(homeDirectory, ".techdebtmaster");
        _walkthroughDirectory = Path.Combine(techDebtMasterDirectory, "walkthroughs");
    }

    public async Task EnsureDefaultWalkthroughAsync()
    {
        // Create walkthroughs directory if it doesn't exist
        if (!Directory.Exists(_walkthroughDirectory))
        {
            Directory.CreateDirectory(_walkthroughDirectory);
        }

        var defaultWalkthroughPath = Path.Combine(_walkthroughDirectory, "default-walkthrough.html");

        // Only create if it doesn't exist (don't overwrite user modifications)
        if (!File.Exists(defaultWalkthroughPath))
        {
            await CreateDefaultWalkthroughAsync(defaultWalkthroughPath);
        }
    }

    public string GetWalkthroughsDirectory()
    {
        return _walkthroughDirectory;
    }

    public async Task<string> GetDefaultWalkthroughPathAsync()
    {
        await EnsureDefaultWalkthroughAsync();
        return Path.Combine(_walkthroughDirectory, "default-walkthrough.html");
    }

    private static async Task CreateDefaultWalkthroughAsync(string filePath)
    {
        const string defaultHtml = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>TechDebtMaster - Product Walkthrough</title>
    <script src=""https://cdn.jsdelivr.net/npm/marked/marked.min.js""></script>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; padding: 0; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); min-height: 100vh; font-size: 18px; }
        .container { max-width: 1200px; margin: 0 auto; padding: 1rem; }
        .hero { text-align: center; color: white; padding: 2rem 0; }
        .hero h1 { font-size: 4rem; margin-bottom: 0.5rem; text-shadow: 2px 2px 4px rgba(0,0,0,0.3); }
        .hero .subtitle { font-size: 1.5rem; margin-bottom: 1rem; opacity: 0.9; }
        .hero p { font-size: 1.1rem; }
        .section { background: white; border-radius: 1rem; padding: 1.5rem; margin: 1rem 0; box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1); }
        .section h2 { font-size: 2.2rem; color: #6366f1; margin-bottom: 1rem; text-align: center; }
        .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1rem; }
        .card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 0.5rem; padding: 1rem; text-align: center; }
        .card h3 { font-size: 1.2rem; margin-bottom: 0.5rem; }
        .card p { font-size: 1rem; line-height: 1.4; }
        .card-icon { font-size: 2.5rem; margin-bottom: 0.5rem; }
        .code-block { background: #f8f9fa; color: #2d3748; padding: 1.5rem; border-radius: 0.5rem; font-family: 'Consolas', 'Monaco', 'Courier New', monospace; margin: 1rem 0; font-size: 0.9rem; line-height: 1.6; border-left: 4px solid #6366f1; overflow-x: auto; }
        .markdown-content { font-size: 16px; line-height: 1.6; color: #374151; }
        .markdown-content h1, .markdown-content h2, .markdown-content h3, .markdown-content h4, .markdown-content h5, .markdown-content h6 { margin-top: 1.5em; margin-bottom: 0.5em; font-weight: 600; color: #1f2937; }
        .markdown-content h1 { font-size: 1.8em; color: #6366f1; }
        .markdown-content h2 { font-size: 1.5em; color: #059669; }
        .markdown-content h3 { font-size: 1.3em; color: #dc2626; }
        .markdown-content p { margin-bottom: 1em; }
        .markdown-content ul, .markdown-content ol { margin-bottom: 1em; padding-left: 2em; }
        .markdown-content code { background: #e5e7eb; padding: 2px 4px; border-radius: 3px; font-family: 'Consolas', 'Monaco', monospace; font-size: 0.9em; }
        .markdown-content pre { background: #1f2937; color: #f3f4f6; padding: 15px; border-radius: 6px; overflow-x: auto; margin-bottom: 1em; }
        .markdown-content pre code { background: transparent; padding: 0; color: inherit; }
        .markdown-content blockquote { border-left: 4px solid #6366f1; padding-left: 1em; margin-left: 0; margin-bottom: 1em; color: #6b7280; }
        .btn { display: inline-block; background: linear-gradient(135deg, #6366f1, #8b5cf6); color: white; padding: 0.7rem 1.2rem; border: none; border-radius: 0.5rem; text-decoration: none; margin: 0.3rem; font-size: 1rem; }
        .carousel-container { position: relative; overflow: hidden; border-radius: 1rem; }
        .carousel-track { display: flex; transition: transform 0.3s ease; }
        .carousel-slide { min-width: 100%; box-sizing: border-box; padding: 0 1rem; }
        .carousel-nav { display: flex; justify-content: center; gap: 0.5rem; margin-top: 1rem; }
        .carousel-dot { width: 12px; height: 12px; border-radius: 50%; background: #d1d5db; cursor: pointer; transition: background 0.3s ease; }
        .carousel-dot.active { background: #6366f1; }
        .carousel-arrows { position: absolute; top: 50%; transform: translateY(-50%); background: rgba(255,255,255,0.9); border: none; border-radius: 50%; width: 40px; height: 40px; cursor: pointer; font-size: 18px; color: #6366f1; transition: all 0.3s ease; }
        .carousel-arrows:hover { background: white; box-shadow: 0 4px 12px rgba(0,0,0,0.15); }
        .carousel-prev { left: 10px; }
        .carousel-next { right: 10px; }
        .feature-highlight { background: linear-gradient(135deg, #f8fafc 0%, #e2e8f0 100%); border: 2px solid #6366f1; border-radius: 1rem; padding: 2rem; text-align: center; margin: 1rem 0; }
        .feature-highlight .icon { font-size: 3rem; margin-bottom: 1rem; }
        .feature-highlight h3 { font-size: 1.5rem; color: #6366f1; margin-bottom: 1rem; }
        .feature-highlight p { font-size: 1.1rem; line-height: 1.6; color: #4b5563; }
        @media (max-width: 768px) { .hero h1 { font-size: 2.2rem; } .hero .subtitle { font-size: 1.1rem; } .hero p { font-size: 1rem; } .section h2 { font-size: 1.3rem; } .container { padding: 0.5rem; } .section { padding: 0.7rem; } body { font-size: 15px; } .carousel-arrows { display: none; } }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""hero"">
            <h1>TechDebtMaster</h1>
            <p class=""subtitle"">AI-Powered Technical Debt Management</p>
            <p>Transform your codebase into a maintainable, high-quality asset with intelligent debt detection and human-centered solutions</p>
        </div>
        
        <div class=""section"">
            <h2>The Technical Debt Crisis</h2>
            <div class=""grid"">
                <div class=""card""><div class=""card-icon"">💰</div><h3>$85B Annual Cost</h3><p>The software industry spends billions annually on technical debt maintenance and remediation.</p></div>
                <div class=""card""><div class=""card-icon"">⏰</div><h3>42% Developer Time</h3><p>Nearly half of development time is spent dealing with technical debt instead of building features.</p></div>
                <div class=""card""><div class=""card-icon"">🚫</div><h3>67% Project Delays</h3><p>Most projects experience delays due to accumulated technical debt and maintainability issues.</p></div>
            </div>
        </div>
        
        <div class=""section"">
            <h2>Key Features</h2>
            <div class=""carousel-container"">
                <div class=""carousel-track"" id=""featuresCarousel"">
                    <div class=""carousel-slide"">
                        <div class=""feature-highlight"">
                            <div class=""icon"">🎯</div>
                            <h3>For Developers</h3>
                            <p>Focus on high-impact improvements with automated refactoring suggestions, faster code reviews, and clear progress tracking. Get actionable insights that help you write better code and <strong>reduce technical debt systematically.</strong></p>
                        </div>
                    </div>
                    <div class=""carousel-slide"">
                        <div class=""feature-highlight"">
                            <div class=""icon"">🏢</div>
                            <h3>For Organizations</h3>
                            <p>Achieve reduced maintenance costs, faster feature delivery, and improved software quality metrics. Make strategic technical investments with data-driven insights and comprehensive reporting.</p>
                        </div>
                    </div>
                    <div class=""carousel-slide"">
                        <div class=""feature-highlight"">
                            <div class=""icon"">🤖</div>
                            <h3>AI-Powered Intelligence</h3>
                            <p>Leverage advanced AI capabilities with LLM integrations, custom prompting support, and intelligent code analysis. Get contextual recommendations tailored to your technology stack.</p>
                        </div>
                    </div>
                </div>
                <button class=""carousel-arrows carousel-prev"" onclick=""prevSlide()"">‹</button>
                <button class=""carousel-arrows carousel-next"" onclick=""nextSlide()"">›</button>
                <div class=""carousel-nav"" id=""carouselDots""></div>
            </div>
        </div>
        
        <div class=""section"">
            <h2>AI-Powered Solution</h2>
            <div class=""grid"">
                <div class=""card""><div class=""card-icon"">🔌</div><h3>LLM Integrations</h3><p>Direct integration with EPAM DIAL and OpenAI for code analysis. Connect to enterprise AI platforms or cloud-based models for intelligent technical debt detection and remediation strategies.</p></div>
                <div class=""card""><div class=""card-icon"">✨</div><h3>Actionable Insights</h3><p>Get specific, contextual recommendations with code examples and refactoring strategies tailored to your technology stack.</p></div>
                <div class=""card"">
                    <div class=""card-icon"">📝</div>
                    <h3>Custom Prompting Support</h3>
                    <p>TechDebtMaster supports custom prompting, enabling you to define and execute any task or workflow using natural language instructions. Leverage the platform for code analysis, documentation, refactoring, or any custom process tailored to your team's needs.</p>
                </div>
            </div>
        </div>
        
        <div class=""section"">
            <h2>Backlog Management</h2>
            <div class=""grid"">
                <div class=""card""><div class=""card-icon"">💭</div><h3>Triage</h3><p>Interactive preview and filtering tools help prioritize and manage technical debt items during team triage sessions.</p></div>
                <div class=""card""><div class=""card-icon"">📊</div><h3>Interactive HTML Reports</h3><p>Generate comprehensive HTML reports with export/import capabilities for seamless collaboration and tracking progress.</p></div>
            </div>
        </div>
        
        <div class=""section"">
            <h2>AI Tooling Friendly</h2>
            <div class=""grid"">
                <div class=""card""><div class=""card-icon"">🔗</div><h3>Model Context Protocol</h3><p>Model Context Protocol support enables seamless integration with AI assistants, IDEs, and development workflows.</p></div>
                <div class=""card""><div class=""card-icon"">🤖</div><h3>AI Assistants Integration</h3><p>Native integration with popular AI tools including VSCode extensions, Gemini CLI, and Claude Code for enhanced development workflows.</p></div>
            </div>
        </div>
        
        <div class=""section"">
            <h2>Quick Start Guide</h2>
            <div id=""quick-start-content"" class=""markdown-content"">
                <!-- Content will be populated by JavaScript -->
            </div>
        </div>
        
        <div class=""section"">
            <h2>Future Features</h2>
            <div class=""grid"">
                <div class=""card""><div class=""card-icon"">🧠</div><h3>Incremental Analysis</h3><p>Smart analysis that remembers user choices to forget irrelevant items and mark them appropriately, reducing noise in future scans.</p></div>
                <div class=""card""><div class=""card-icon"">🤝</div><h3>Team Collaboration</h3><p>Improved tech debt backlog management with better team collaboration features and shared prioritization workflows.</p></div>
                <div class=""card""><div class=""card-icon"">🌐</div><h3>Integration with CodeMie</h3><p>Integration with CodeMie through remote Model Context Protocol for advanced workflows and automated technical debt remediation.</p></div>
                <div class=""card""><div class=""card-icon"">🔄</div><h3>Automated CI/CD Workflows</h3><p>CodeMie workflow integration to create separate merge requests for each tech debt item as part of nightly CI/CD runs, enabling incremental automated fixes.</p></div>
                <div class=""card""><div class=""card-icon"">📈</div><h3>Advanced Analytics</h3><p>Enhanced analytics dashboard with detailed metrics, trends, and insights into technical debt evolution over time.</p></div>
                <div class=""card""><div class=""card-icon"">🤖</div><h3>AI Prioritization</h3><p>AI-driven prioritization of technical debt items based on impact, risk, and team capacity to optimize remediation efforts.</p></div>
            </div>
        </div>
    </div>
    <script>
        let currentSlide = 0;
        const slides = document.querySelectorAll('.carousel-slide');
        const totalSlides = slides.length;
        
        function initCarousel() {
            const dotsContainer = document.getElementById('carouselDots');
            for (let i = 0; i < totalSlides; i++) {
                const dot = document.createElement('div');
                dot.className = 'carousel-dot';
                if (i === 0) dot.classList.add('active');
                dot.onclick = () => goToSlide(i);
                dotsContainer.appendChild(dot);
            }
        }
        
        function updateCarousel() {
            const track = document.getElementById('featuresCarousel');
            const dots = document.querySelectorAll('.carousel-dot');
            
            track.style.transform = `translateX(-${currentSlide * 100}%)`;
            
            dots.forEach((dot, index) => {
                dot.classList.toggle('active', index === currentSlide);
            });
        }
        
        function nextSlide() {
            currentSlide = (currentSlide + 1) % totalSlides;
            updateCarousel();
        }
        
        // Quick Start Guide markdown content
        const quickStartMarkdown = `
### ⚙️ Step 1: Setup & Configuration
Configure your AI API key and default settings:

\`\`\`bash
# Set AI API key (required for analysis)
tdm config set ai.key <your-api-key>

# List Models (Optional)
tdm dial models list

# Set Model (Optional)
tdm dial models set-default

# List & Edit Prompts (Optional)
tdm prompts edit

# Set Default Prompt (Optional)
tdm prompts set-default

# View current configuration
tdm config show
\`\`\`

### 📁 Step 2: Repository Management
Index your repository for analysis:

\`\`\`bash
# Index repository for analysis
tdm repo index

# Check repository status
tdm repo status
\`\`\`

### 🔍 Step 3: Debt Analysis
Analyze your code for technical debt:

\`\`\`bash
# Analyze for technical debt
tdm debt analyze
\`\`\`

### 📊 Step 4.1: Results & Reporting
View and export your technical debt analysis:

\`\`\`bash
# Show debt statistics in tree structure
tdm debt show

# View detailed debt items (interactive)
tdm debt view

# Filter debt items by severity
tdm debt show --severity Critical

# Export specific debt item as XML
tdm debt view --tags Security --xml
\`\`\`

### 📝 Step 4.2: HTML Report Generation
Generate and manage interactive HTML reports:

\`\`\`bash
# Generate HTML report
tdm debt report

# Generate and open report in browser
tdm debt report --output my-report.html --open

# Import modified report (preview changes)
tdm debt import modified-report.html

# Apply changes from modified report
tdm debt import modified-report.html --apply
\`\`\`

### 🤖 Step 5: AI Integration with MCP
Connect with AI assistants using Model Context Protocol:

\`\`\`bash
# Start MCP server for external tool integration
tdm mcp
\`\`\`

### 🔗 AI Tooling Integration Examples
VSCode with MCP integration

\`\`\`bash
# Initialize for VS Code
tdm init --profile vscode
\`\`\`
`;

        // Render markdown content
        function renderQuickStart() {
            const contentDiv = document.getElementById('quick-start-content');
            if (contentDiv && marked) {
                contentDiv.innerHTML = marked.parse(quickStartMarkdown);
            }
        }
        
        function prevSlide() {
            currentSlide = (currentSlide - 1 + totalSlides) % totalSlides;
            updateCarousel();
        }
        
        function goToSlide(index) {
            currentSlide = index;
            updateCarousel();
        }
        
        // Initialize carousel when page loads
        document.addEventListener('DOMContentLoaded', () => {
            initCarousel();
            renderQuickStart();
        });
    </script>
</body>
</html>";

        await File.WriteAllTextAsync(filePath, defaultHtml);
    }
}
