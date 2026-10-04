using System.Text.Json;
using NexusAI.Agent;
using OpenAI.Chat;

var orgUrl = DataverseConnectionFactory.ResolveOrgUrl(GetArg(args, "--org-url"));
Console.WriteLine($"Target Dataverse org: {orgUrl}");

using var svc = DataverseConnectionFactory.Connect(orgUrl);
Console.WriteLine("Connected.");

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

// --- Resolve an Account ID, either directly or via a name search ---
var accountIdArg = GetArg(args, "--account-id");
var accountNameArg = GetArg(args, "--account-name");
Guid accountId;

if (accountIdArg is not null)
{
    if (!Guid.TryParse(accountIdArg, out accountId))
    {
        Console.Error.WriteLine($"'{accountIdArg}' is not a valid GUID.");
        return 2;
    }
}
else if (accountNameArg is not null)
{
    if (!TryResolveAccountByName(svc, accountNameArg, out accountId))
    {
        return 2;
    }
}
else
{
    Console.Write("Enter Account ID (GUID) or part of an account name: ");
    var input = Console.ReadLine()?.Trim() ?? "";

    if (Guid.TryParse(input, out accountId))
    {
        // looked like a GUID, use it directly
    }
    else if (!TryResolveAccountByName(svc, input, out accountId))
    {
        return 2;
    }
}

// --- Step 2 (already working): retrieve the real structured data ---
Customer360Result customer360;
try
{
    customer360 = DataverseTools.GetCustomer360(svc, accountId);
}
catch (AccountNotFoundException)
{
    Console.WriteLine(JsonSerializer.Serialize(new { error = "Account not found." }, jsonOptions));
    return 1;
}

// ===========================================================================
// STEP 3 -- GROUNDING. This is the actual new concept: instead of printing
// the raw JSON (what Program.cs did before), we hand that real data to the
// LLM as CONTEXT and ask it to reason over it. The model never invents
// customer facts -- everything it says has to trace back to this JSON,
// because that's literally the only information about this customer it has.
// ===========================================================================
var customer360Json = JsonSerializer.Serialize(customer360, jsonOptions);

var chatClient = LlmClientFactory.Connect();

var systemPrompt =
    "You are NexusAI, an assistant for a bank relationship manager. " +
    "You will be given structured JSON data about one customer (their account, " +
    "contacts, leads, opportunities, product holdings, and recent activities). " +
    "Write a short, practical briefing a relationship manager could read in 30 " +
    "seconds before a meeting with this customer. " +
    "Base every statement STRICTLY on the JSON provided -- if the data doesn't " +
    "mention something (e.g. no activities exist), say so plainly rather than " +
    "guessing or inventing details. Do not present this briefing as financial advice.";

var userPrompt =
    $"Here is the customer data:\n\n{customer360Json}\n\n" +
    "Please prepare the meeting briefing.";

var messages = new ChatMessage[]
{
    new SystemChatMessage(systemPrompt),
    new UserChatMessage(userPrompt),
};

Console.WriteLine();
Console.WriteLine("Generating briefing...");
ChatCompletion completion = chatClient.CompleteChat(messages);

Console.WriteLine();
Console.WriteLine("--- Customer Briefing ---");
Console.WriteLine(completion.Content[0].Text);

return 0;

// Searches by name; if exactly one match, resolves it automatically.
// If multiple match, lists them and asks the person to pick one (or
// re-run with --account-id once they know which GUID they want).
bool TryResolveAccountByName(Microsoft.Xrm.Sdk.IOrganizationService organizationService, string nameSearch, out Guid resolvedId)
{
    var matches = DataverseTools.SearchAccountsByName(organizationService, nameSearch);

    if (matches.Count == 0)
    {
        Console.Error.WriteLine($"No accounts found matching '{nameSearch}'.");
        resolvedId = Guid.Empty;
        return false;
    }

    if (matches.Count == 1)
    {
        Console.WriteLine($"Matched: {matches[0].Name} ({matches[0].AccountId})");
        resolvedId = matches[0].AccountId;
        return true;
    }

    Console.WriteLine($"{matches.Count} accounts matched '{nameSearch}':");
    for (var i = 0; i < matches.Count; i++)
    {
        Console.WriteLine($"  {i + 1}) {matches[i].Name}  ({matches[i].AccountId})");
    }
    Console.Write("Pick a number: ");
    var choice = Console.ReadLine()?.Trim();
    if (int.TryParse(choice, out var num) && num >= 1 && num <= matches.Count)
    {
        resolvedId = matches[num - 1].AccountId;
        return true;
    }

    Console.Error.WriteLine("Invalid selection.");
    resolvedId = Guid.Empty;
    return false;
}

static string? GetArg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == name)
        {
            return args[i + 1];
        }
    }
    return null;
}