using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

// Populate values from your OpenAI deployment
var endpoint = "https://ai-proxy.lab.epam.com/";
var apiKey = "{Your API Key}";
var deploymentName = "gpt-35-turbo";
var apiVersion = "2023-08-01-preview";

var handler = new HttpClientHandler();
handler.CheckCertificateRevocationList = false;
var httpClient = new HttpClient(handler);

// Create a kernel with Azure OpenAI chat completion
var builder = Kernel
    .CreateBuilder()
    .AddAzureOpenAIChatCompletion(
        deploymentName,
        endpoint,
        apiKey,
        null,
        deploymentName,
        httpClient
    );

// Add enterprise components
builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));

// Build the kernel
Kernel kernel = builder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

// Enable planning
OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new() { Temperature = 0.4 };

var history = new ChatHistory();

string? userInput = string.Empty;
while (userInput != "exit")
{
    // Collect user input
    Console.Write("User > ");

    userInput = Console.ReadLine();
    if (userInput == "exit")
        continue;

    history.AddSystemMessage(
        "You are a useful gym chatbot. You will limit your answers to purely gym-related topics. Any other topics, "
            + "or you don't know an answer, say 'I'm sorry, I can't help with that!'"
    );

    // Add user input
    history.AddUserMessage(userInput);
    Console.WriteLine(userInput);

    // Get the response from the AI
    var result = await chatCompletionService.GetChatMessageContentAsync(
        history,
        openAIPromptExecutionSettings,
        kernel: kernel
    );

    // Print the results
    Console.WriteLine("Assistant > " + result);

    // Add the message from the agent to the chat history
    history.AddMessage(result.Role, result.Content ?? string.Empty);
}
;
