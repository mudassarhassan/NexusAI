using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace NexusAI.Agent;

/// <summary>
/// Builds a connected ChatClient against Azure AI Foundry / Azure OpenAI.
/// Extracted from the earlier Customer360 learning exercise so the same
/// connection logic can be reused by Program.cs once it starts combining
/// GetCustomer360's data with an actual LLM call (Phase 3 onward).
/// </summary>
public static class LlmClientFactory
{
    public static ChatClient Connect()
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deploymentName))
        {
            throw new InvalidOperationException(
                "Set AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_KEY, and AZURE_OPENAI_DEPLOYMENT environment variables first.");
        }

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        return azureClient.GetChatClient(deploymentName);
    }
}
