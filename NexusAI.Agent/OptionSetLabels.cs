namespace NexusAI.Agent;

/// <summary>
/// Decodes a custom picklist's numeric OptionSetValue back to its label.
/// Deliberately duplicated (not project-referenced) from
/// DataverseModelGenerator's TableDefinitions.cs: the agent and the
/// metadata-admin tool are separate concerns and shouldn't be coupled just to
/// share these small arrays. Order here MUST stay in sync with the
/// OptionSet lists in DataverseModelGenerator/TableDefinitions.cs, since
/// Dataverse assigns values sequentially (100000000, 200000000, ...) in
/// that exact order.
/// </summary>
public static class OptionSetLabels
{
    private static readonly string[] LeadSource = ["Web", "Referral", "Advertisement", "Cold Call", "Partner", "Other"];
    private static readonly string[] Rating = ["Hot", "Warm", "Cold"];
    private static readonly string[] LeadStatus = ["New", "Contacted", "Qualified", "Disqualified", "Converted"];
    private static readonly string[] SalesStage = ["Qualify", "Develop", "Propose", "Close"];
    private static readonly string[] OpportunityStatus = ["Open", "Won", "Lost"];
    private static readonly string[] ProductType = ["Inventory", "Service", "Bundle", "Other"];
    private static readonly string[] HoldingStatus = ["Active", "Closed", "Matured", "Suspended"];

    public static string? LeadSourceLabel(int? value) => Lookup(LeadSource, value);
    public static string? RatingLabel(int? value) => Lookup(Rating, value);
    public static string? LeadStatusLabel(int? value) => Lookup(LeadStatus, value);
    public static string? SalesStageLabel(int? value) => Lookup(SalesStage, value);
    public static string? OpportunityStatusLabel(int? value) => Lookup(OpportunityStatus, value);
    public static string? ProductTypeLabel(int? value) => Lookup(ProductType, value);
    public static string? HoldingStatusLabel(int? value) => Lookup(HoldingStatus, value);

    private static string? Lookup(string[] options, int? value)
    {
        if (value is null)
        {
            return null;
        }
        var index = (value.Value / 100000000) - 1;
        return index >= 0 && index < options.Length ? options[index] : null;
    }
}
