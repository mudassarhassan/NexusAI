using Microsoft.PowerPlatform.Dataverse.Client;

namespace NexusAI.Agent;

/// <summary>
/// Builds an authenticated Dataverse connection. Auth is always interactive
/// (browser sign-in) using Microsoft's published sample application ID --
/// same pattern as DataverseModelGenerator, no app registration required.
/// </summary>
public static class DataverseConnectionFactory
{
    private const string DefaultDataverseUrl = "https://nexusai.crm12.dynamics.com";
    private const string InteractiveSampleAppId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
    // MSAL (which ServiceClient uses) only supports loopback redirect URIs for
    // interactive sign-in, not the old ADAL-era custom "app://..." scheme --
    // see Microsoft's current OAuth docs, which use exactly this value.
    private const string InteractiveSampleRedirectUri = "http://localhost";

    public static string ResolveOrgUrl(string? explicitUrl)
    {
        var url = explicitUrl
            ?? Environment.GetEnvironmentVariable("DATAVERSE_URL")
            ?? Environment.GetEnvironmentVariable("DYNAMICS_URL")
            ?? Environment.GetEnvironmentVariable("ORG_URL")
            ?? DefaultDataverseUrl;
        return url.TrimEnd('/');
    }

    public static ServiceClient Connect(string orgUrl)
    {
        var connectionString =
            $"AuthType=OAuth;Url={orgUrl};AppId={InteractiveSampleAppId};" +
            $"RedirectUri={InteractiveSampleRedirectUri};LoginPrompt=Auto;RequireNewInstance=true";

        var svc = new ServiceClient(connectionString);
        if (!svc.IsReady)
        {
            var detail = svc.LastException?.ToString() ?? svc.LastError;
            throw new InvalidOperationException($"Failed to connect to Dataverse: {detail}");
        }
        return svc;
    }
}