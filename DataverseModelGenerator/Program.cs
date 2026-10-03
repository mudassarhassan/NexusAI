using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using DataverseModelGenerator;

const string DefaultDataverseUrl = "https://nexusai.crm12.dynamics.com";

// Microsoft's published sample/test application ID + redirect URI, used for
// interactive sign-in when no app registration (client id/secret) is
// supplied. This is the same convenience the Python version got from
// shelling out to `az login` -- here it's native to the SDK instead.
const string InteractiveSampleAppId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
const string InteractiveSampleRedirectUri = "app://58145b91-0c36-4500-8554-080855f8306b";

var options = ParseArgs(args);

try
{
    return Run(options);
}
catch (Exception ex)
{
    Log.Error($"Unhandled error: {ex.Message}");
    return 1;
}

int Run(Options opts)
{
    var orgUrl = (opts.OrgUrl ?? ResolveOrgUrlFromEnv()).TrimEnd('/');
    if (string.IsNullOrWhiteSpace(orgUrl))
    {
        Log.Error("Unable to determine Dataverse org URL. Set --org-url or the DATAVERSE_URL environment variable.");
        return 2;
    }

    var connectionString = BuildConnectionString(orgUrl, opts);
    Log.Info($"Target Dataverse org: {orgUrl}");

    using var svc = new ServiceClient(connectionString);
    if (!svc.IsReady)
    {
        Log.Error($"Failed to connect to Dataverse: {svc.LastError}");
        if (svc.LastException is not null)
        {
            Log.Error(svc.LastException.ToString());
        }
        return 2;
    }

    var whoAmI = (WhoAmIResponse)svc.Execute(new WhoAmIRequest());
    Log.Info($"Verified Dataverse identity; UserId: {whoAmI.UserId}");

    // --- Decide what to do: CLI flag if given, otherwise ask interactively ---
    var action = opts.ProgramAction ?? PromptForAction();

    if (action is ProgramAction.CreateMetadata or ProgramAction.Both)
    {
        RunCreateMetadata(svc, opts);
    }

    if (action is ProgramAction.GenerateSampleData or ProgramAction.Both)
    {
        var selectedSteps = opts.OnlySteps ?? PromptForSampleDataSteps();
        var recordCounts = SampleDataPools.DefaultRecordCounts;
        if (selectedSteps.Count < SampleDataPools.StepKeys.Length)
        {
            Log.Info($"Generating sample data for: {string.Join(", ", selectedSteps)} (other tables reused, not duplicated).");
        }
        SampleDataGenerator.Run(svc, selectedSteps, recordCounts);
    }

    Log.Info("Done.");
    return 0;
}

void RunCreateMetadata(IOrganizationService svc, Options opts)
{
    var solutionName = opts.SolutionName ?? DataModel.RequiredSolutionName;
    var solutionUniqueName = DataverseMetadataService.GetSolutionUniqueName(svc, DataModel.RequiredSolutionName);
    Log.Info($"Solution unique name resolved to: {solutionUniqueName}");
    if (solutionName != DataModel.RequiredSolutionName)
    {
        Log.Info($"Using requested solution name override: {solutionName}");
        solutionUniqueName = solutionName;
    }

    var customTableLogicalNames = new HashSet<string>(DataModel.CustomTables.Select(t => t.LogicalName));

    var existing = new Dictionary<string, bool>();
    foreach (var logicalName in DataModel.StandaloneLookupTargets.Append("activitypointer").Concat(customTableLogicalNames))
    {
        existing[logicalName] = DataverseMetadataService.TryGetEntityMetadata(svc, logicalName, out _);
    }

    var availableLookupTargets = DataModel.StandaloneLookupTargets
        .Where(name => existing.GetValueOrDefault(name))
        .ToHashSet();
    var missingTargets = DataModel.StandaloneLookupTargets.Where(name => !availableLookupTargets.Contains(name)).ToList();
    if (missingTargets.Count > 0)
    {
        Log.Info($"Standard lookup targets not present in this environment, their lookup fields will be skipped: [{string.Join(", ", missingTargets)}]");
    }

    foreach (var table in DataModel.CustomTables)
    {
        if (existing.GetValueOrDefault(table.LogicalName))
        {
            Log.Info($"Custom table already exists; skipping creation: {table.LogicalName}");
        }
        else
        {
            DataverseMetadataService.EnsureTableExists(svc, table);
        }
        DataverseMetadataService.EnsureFields(svc, table, availableLookupTargets, customTableLogicalNames);
    }

    if (!opts.SkipSolutionAssociation)
    {
        foreach (var table in DataModel.CustomTables)
        {
            DataverseMetadataService.AddTableToSolution(svc, solutionUniqueName, table.LogicalName);
        }
        DataverseMetadataService.PublishAll(svc);
    }

    Log.Info("Dataverse metadata sync complete.");
}

ProgramAction PromptForAction()
{
    Console.WriteLine();
    Console.WriteLine("What would you like to do?");
    Console.WriteLine("  1) Create/update Dataverse metadata (tables, fields, solution)");
    Console.WriteLine("  2) Generate sample data");
    Console.WriteLine("  3) Both");
    Console.Write("Enter choice (1-3): ");
    var input = Console.ReadLine()?.Trim();
    return input switch
    {
        "1" => ProgramAction.CreateMetadata,
        "2" => ProgramAction.GenerateSampleData,
        "3" => ProgramAction.Both,
        _ => throw new InvalidOperationException($"Unrecognized choice: '{input}'. Expected 1, 2, or 3."),
    };
}

HashSet<string> PromptForSampleDataSteps()
{
    Console.WriteLine();
    Console.WriteLine("Which sample data steps do you want to generate?");
    for (var i = 0; i < SampleDataPools.StepKeys.Length; i++)
    {
        Console.WriteLine($"  {i + 1}) {SampleDataPools.StepKeys[i]}");
    }
    Console.Write("Enter comma-separated numbers, or press Enter for all: ");
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input))
    {
        return [.. SampleDataPools.StepKeys];
    }

    var selected = new HashSet<string>();
    foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (int.TryParse(part, out var num) && num >= 1 && num <= SampleDataPools.StepKeys.Length)
        {
            selected.Add(SampleDataPools.StepKeys[num - 1]);
        }
        else
        {
            throw new InvalidOperationException($"Unrecognized step selection: '{part}'.");
        }
    }
    return selected;
}

string BuildConnectionString(string orgUrl, Options opts)
{
    Log.Info("Using interactive sign-in (a browser window will open).");
    return $"AuthType=OAuth;Url={orgUrl};AppId={InteractiveSampleAppId};RedirectUri={InteractiveSampleRedirectUri};LoginPrompt=Auto;RequireNewInstance=true";
}

string ResolveOrgUrlFromEnv()
{
    return Environment.GetEnvironmentVariable("DATAVERSE_URL")
        ?? Environment.GetEnvironmentVariable("DYNAMICS_URL")
        ?? Environment.GetEnvironmentVariable("ORG_URL")
        ?? DefaultDataverseUrl;
}

Options ParseArgs(string[] rawArgs)
{
    var opts = new Options();
    for (var i = 0; i < rawArgs.Length; i++)
    {
        switch (rawArgs[i])
        {
            case "--org-url":
                opts.OrgUrl = rawArgs[++i];
                break;
            case "--solution-name":
                opts.SolutionName = rawArgs[++i];
                break;
            case "--skip-solution-association":
                opts.SkipSolutionAssociation = true;
                break;
            case "--action":
                opts.ProgramAction = rawArgs[++i] switch
                {
                    "create-metadata" => ProgramAction.CreateMetadata,
                    "generate-sample-data" => ProgramAction.GenerateSampleData,
                    "both" => ProgramAction.Both,
                    var other => throw new ArgumentException($"Unrecognized --action value: '{other}'. Expected create-metadata, generate-sample-data, or both."),
                };
                break;
            case "--only":
                opts.OnlySteps = rawArgs[++i]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet();
                break;
            default:
                Console.Error.WriteLine($"Unrecognized argument: {rawArgs[i]}");
                break;
        }
    }
    return opts;
}

enum ProgramAction
{
    CreateMetadata,
    GenerateSampleData,
    Both,
}

sealed class Options
{
    public string? OrgUrl { get; set; }
    public string? SolutionName { get; set; }
    public bool SkipSolutionAssociation { get; set; }
    public ProgramAction? ProgramAction { get; set; }
    public HashSet<string>? OnlySteps { get; set; }
}