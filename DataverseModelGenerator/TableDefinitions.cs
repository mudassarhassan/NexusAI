namespace DataverseModelGenerator;

public enum FieldType
{
    String,
    Memo,
    DateTime,
    Boolean,
    Decimal,
    Integer,
    Picklist,
    Lookup,
}

/// <summary>
/// Mirrors one field entry from the Python script's CUSTOM_TABLES field dicts.
/// Not every property applies to every FieldType -- see FieldBuilder for which
/// ones each type actually reads.
/// </summary>
public sealed record FieldDefinition
{
    public required string SchemaName { get; init; }
    public required string DisplayName { get; init; }
    public required FieldType Type { get; init; }
    public bool Required { get; init; }
    public bool IsPrimaryName { get; init; }

    // StringType
    public int? MaxLength { get; init; }
    public string? Format { get; init; } // "Text" (default), "Email", "Phone"

    // MemoType
    // (reuses MaxLength, default 2000 if not set)

    // DecimalType
    public int? Precision { get; init; }
    public double? MinValue { get; init; }
    public double? MaxValue { get; init; }

    // IntegerType
    public int? MinValueInt { get; init; }
    public int? MaxValueInt { get; init; }

    // PicklistType -- option labels in display order; Dataverse assigns
    // values sequentially (100000000, 200000000, ...) in this exact order.
    public string[]? OptionSet { get; init; }

    // LookupType
    public string? TargetEntityLogicalName { get; init; }
}

public sealed record TableDefinition
{
    public required string LogicalName { get; init; }
    public required string SchemaName { get; init; }
    public required string DisplayName { get; init; }
    public required string CollectionName { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }

    public FieldDefinition PrimaryField => Fields.First(f => f.IsPrimaryName);
}

/// <summary>
/// Direct port of the Python script's CUSTOM_TABLES dict. Order matters:
/// nxa_lead, nxa_opportunity and nxa_product must come before
/// nxa_productholding, which must come before nxa_airecommendation /
/// nxa_aiactionlog, since later tables have lookups pointing at earlier ones.
/// </summary>
public static class DataModel
{
    public const string RequiredSolutionName = "NexusAICore";

    // Standard, out-of-the-box tables this tool never creates: it only
    // checks whether they exist in the target environment and uses them as
    // lookup targets when they do.
    public static readonly string[] StandaloneLookupTargets = ["account", "contact"];

    public static readonly IReadOnlyList<TableDefinition> CustomTables = new List<TableDefinition>
    {
        new TableDefinition
        {
            LogicalName = "nxa_lead",
            SchemaName = "nxa_Lead",
            DisplayName = "NexusAI Lead",
            CollectionName = "NexusAI Leads",
            Description = "Custom lead table used by NexusAI; standard Lead is not available in this environment.",
            Fields = new List<FieldDefinition>
            {
                new() { SchemaName = "nxa_name", DisplayName = "Topic", Type = FieldType.String, Required = true, MaxLength = 200, IsPrimaryName = true },
                new() { SchemaName = "nxa_firstname", DisplayName = "First Name", Type = FieldType.String, Required = false, MaxLength = 100 },
                new() { SchemaName = "nxa_lastname", DisplayName = "Last Name", Type = FieldType.String, Required = false, MaxLength = 100 },
                new() { SchemaName = "nxa_companyname", DisplayName = "Company Name", Type = FieldType.String, Required = false, MaxLength = 200 },
                new() { SchemaName = "nxa_emailaddress", DisplayName = "Email", Type = FieldType.String, Required = false, MaxLength = 200, Format = "Email" },
                new() { SchemaName = "nxa_telephone", DisplayName = "Phone", Type = FieldType.String, Required = false, MaxLength = 50, Format = "Phone" },
                new() { SchemaName = "nxa_accountid", DisplayName = "Account", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "account" },
                new() { SchemaName = "nxa_contactid", DisplayName = "Contact", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "contact" },
                new() { SchemaName = "nxa_leadsource", DisplayName = "Lead Source", Type = FieldType.Picklist, Required = false, OptionSet = ["Web", "Referral", "Advertisement", "Cold Call", "Partner", "Other"] },
                new() { SchemaName = "nxa_rating", DisplayName = "Rating", Type = FieldType.Picklist, Required = false, OptionSet = ["Hot", "Warm", "Cold"] },
                new() { SchemaName = "nxa_status", DisplayName = "Status", Type = FieldType.Picklist, Required = true, OptionSet = ["New", "Contacted", "Qualified", "Disqualified", "Converted"] },
            },
        },
        new TableDefinition
        {
            LogicalName = "nxa_opportunity",
            SchemaName = "nxa_Opportunity",
            DisplayName = "NexusAI Opportunity",
            CollectionName = "NexusAI Opportunities",
            Description = "Custom opportunity table used by NexusAI; standard Opportunity is not available in this environment.",
            Fields = new List<FieldDefinition>
            {
                new() { SchemaName = "nxa_name", DisplayName = "Opportunity Name", Type = FieldType.String, Required = true, MaxLength = 200, IsPrimaryName = true },
                new() { SchemaName = "nxa_accountid", DisplayName = "Account", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "account" },
                new() { SchemaName = "nxa_contactid", DisplayName = "Contact", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "contact" },
                new() { SchemaName = "nxa_leadid", DisplayName = "Originating Lead", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "nxa_lead" },
                new() { SchemaName = "nxa_estimatedvalue", DisplayName = "Estimated Value", Type = FieldType.Decimal, Required = false, Precision = 2, MinValue = 0.0, MaxValue = 100000000.0 },
                new() { SchemaName = "nxa_totalamount", DisplayName = "Total Amount", Type = FieldType.Decimal, Required = false, Precision = 2, MinValue = 0.0, MaxValue = 100000000.0 },
                new() { SchemaName = "nxa_closeprobability", DisplayName = "Close Probability", Type = FieldType.Integer, Required = false, MinValueInt = 0, MaxValueInt = 100 },
                new() { SchemaName = "nxa_estimatedclosedate", DisplayName = "Estimated Close Date", Type = FieldType.DateTime, Required = false },
                new() { SchemaName = "nxa_salesstage", DisplayName = "Sales Stage", Type = FieldType.Picklist, Required = false, OptionSet = ["Qualify", "Develop", "Propose", "Close"] },
                new() { SchemaName = "nxa_rating", DisplayName = "Rating", Type = FieldType.Picklist, Required = false, OptionSet = ["Hot", "Warm", "Cold"] },
                new() { SchemaName = "nxa_status", DisplayName = "Status", Type = FieldType.Picklist, Required = true, OptionSet = ["Open", "Won", "Lost"] },
            },
        },
        new TableDefinition
        {
            LogicalName = "nxa_product",
            SchemaName = "nxa_Product",
            DisplayName = "NexusAI Product",
            CollectionName = "NexusAI Products",
            Description = "Custom product table used by NexusAI; standard Product is not available in this environment.",
            Fields = new List<FieldDefinition>
            {
                new() { SchemaName = "nxa_name", DisplayName = "Product Name", Type = FieldType.String, Required = true, MaxLength = 200, IsPrimaryName = true },
                new() { SchemaName = "nxa_productnumber", DisplayName = "Product Number", Type = FieldType.String, Required = false, MaxLength = 100 },
                new() { SchemaName = "nxa_producttype", DisplayName = "Product Type", Type = FieldType.Picklist, Required = false, OptionSet = ["Inventory", "Service", "Bundle", "Other"] },
                new() { SchemaName = "nxa_currentcost", DisplayName = "Current Cost", Type = FieldType.Decimal, Required = false, Precision = 2, MinValue = 0.0, MaxValue = 100000000.0 },
                new() { SchemaName = "nxa_price", DisplayName = "List Price", Type = FieldType.Decimal, Required = false, Precision = 2, MinValue = 0.0, MaxValue = 100000000.0 },
                new() { SchemaName = "nxa_parentproductid", DisplayName = "Parent Product", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "nxa_product" },
            },
        },
        new TableDefinition
        {
            LogicalName = "nxa_productholding",
            SchemaName = "nxa_ProductHolding",
            DisplayName = "NexusAI Product Holding",
            CollectionName = "NexusAI Product Holdings",
            Description = "Represents a product currently or previously held by an account or contact.",
            Fields = new List<FieldDefinition>
            {
                new() { SchemaName = "nxa_name", DisplayName = "Holding Name", Type = FieldType.String, Required = true, MaxLength = 200, IsPrimaryName = true },
                new() { SchemaName = "nxa_accountid", DisplayName = "Account", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "account" },
                new() { SchemaName = "nxa_contactid", DisplayName = "Contact", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "contact" },
                new() { SchemaName = "nxa_productid", DisplayName = "Product", Type = FieldType.Lookup, Required = true, TargetEntityLogicalName = "nxa_product" },
                new() { SchemaName = "nxa_holdingnumber", DisplayName = "Holding Number", Type = FieldType.String, Required = false, MaxLength = 100 },
                new() { SchemaName = "nxa_startdate", DisplayName = "Start Date", Type = FieldType.DateTime, Required = false },
                new() { SchemaName = "nxa_enddate", DisplayName = "End Date", Type = FieldType.DateTime, Required = false },
                new() { SchemaName = "nxa_balance", DisplayName = "Balance", Type = FieldType.Decimal, Required = false, Precision = 2, MinValue = -100000000.0, MaxValue = 100000000.0 },
                new() { SchemaName = "nxa_interestrate", DisplayName = "Interest Rate", Type = FieldType.Decimal, Required = false, Precision = 2, MinValue = 0.0, MaxValue = 100.0 },
                new() { SchemaName = "nxa_status", DisplayName = "Status", Type = FieldType.Picklist, Required = true, OptionSet = ["Active", "Closed", "Matured", "Suspended"] },
            },
        },
        new TableDefinition
        {
            LogicalName = "nxa_airecommendation",
            SchemaName = "nxa_AIRecommendation",
            DisplayName = "AI Recommendation",
            CollectionName = "AI Recommendations",
            Description = "Stores recommendations generated by the NexusAI AI agent.",
            Fields = new List<FieldDefinition>
            {
                new() { SchemaName = "nxa_name", DisplayName = "Recommendation Name", Type = FieldType.String, Required = true, MaxLength = 200, IsPrimaryName = true },
                new() { SchemaName = "nxa_accountid", DisplayName = "Account", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "account" },
                new() { SchemaName = "nxa_contactid", DisplayName = "Contact", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "contact" },
                new() { SchemaName = "nxa_leadid", DisplayName = "Lead", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "nxa_lead" },
                new() { SchemaName = "nxa_opportunityid", DisplayName = "Opportunity", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "nxa_opportunity" },
                new() { SchemaName = "nxa_productid", DisplayName = "Product", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "nxa_product" },
                new() { SchemaName = "nxa_recommendation", DisplayName = "Recommendation", Type = FieldType.Memo, Required = true },
                new() { SchemaName = "nxa_reason", DisplayName = "Reason", Type = FieldType.Memo, Required = true },
                new() { SchemaName = "nxa_supportingevidence", DisplayName = "Supporting Evidence", Type = FieldType.Memo, Required = false },
                new() { SchemaName = "nxa_confidence", DisplayName = "Confidence", Type = FieldType.Decimal, Required = false, Precision = 2 },
                new() { SchemaName = "nxa_status", DisplayName = "Status", Type = FieldType.Picklist, Required = true, OptionSet = ["Draft", "Pending Approval", "Approved", "Rejected", "Completed", "Cancelled"] },
                new() { SchemaName = "nxa_generatedby", DisplayName = "Generated By", Type = FieldType.String, Required = false, MaxLength = 200 },
                new() { SchemaName = "nxa_generatedon", DisplayName = "Generated On", Type = FieldType.DateTime, Required = true },
                new() { SchemaName = "nxa_humanapproved", DisplayName = "Human Approved", Type = FieldType.Boolean, Required = true },
                new() { SchemaName = "nxa_approvaldate", DisplayName = "Approval Date", Type = FieldType.DateTime, Required = false },
            },
        },
        new TableDefinition
        {
            LogicalName = "nxa_aiactionlog",
            SchemaName = "nxa_AIActionLog",
            DisplayName = "AI Action Log",
            CollectionName = "AI Action Logs",
            Description = "Record AI-proposed and AI-executed actions for auditability.",
            Fields = new List<FieldDefinition>
            {
                new() { SchemaName = "nxa_name", DisplayName = "Action Name", Type = FieldType.String, Required = true, MaxLength = 200, IsPrimaryName = true },
                new() { SchemaName = "nxa_accountid", DisplayName = "Account", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "account" },
                new() { SchemaName = "nxa_contactid", DisplayName = "Contact", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "contact" },
                new() { SchemaName = "nxa_leadid", DisplayName = "Lead", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "nxa_lead" },
                new() { SchemaName = "nxa_opportunityid", DisplayName = "Opportunity", Type = FieldType.Lookup, Required = false, TargetEntityLogicalName = "nxa_opportunity" },
                new() { SchemaName = "nxa_actiontype", DisplayName = "Action Type", Type = FieldType.Picklist, Required = true, OptionSet = ["Create Record", "Update Record", "Send Communication", "Create Task", "Assign Lead", "Update Opportunity", "Generate Recommendation", "Other"] },
                new() { SchemaName = "nxa_description", DisplayName = "Description", Type = FieldType.Memo, Required = true },
                new() { SchemaName = "nxa_agent", DisplayName = "Agent", Type = FieldType.String, Required = true, MaxLength = 200 },
                new() { SchemaName = "nxa_status", DisplayName = "Status", Type = FieldType.Picklist, Required = true, OptionSet = ["Proposed", "Pending Approval", "Approved", "Rejected", "Executing", "Completed", "Failed", "Cancelled"] },
                new() { SchemaName = "nxa_requiresapproval", DisplayName = "Requires Approval", Type = FieldType.Boolean, Required = true },
                new() { SchemaName = "nxa_approvedby", DisplayName = "Approved By", Type = FieldType.String, Required = false, MaxLength = 200 },
                new() { SchemaName = "nxa_approvedon", DisplayName = "Approved On", Type = FieldType.DateTime, Required = false },
                new() { SchemaName = "nxa_executedon", DisplayName = "Executed On", Type = FieldType.DateTime, Required = false },
                new() { SchemaName = "nxa_result", DisplayName = "Result", Type = FieldType.Memo, Required = false },
                new() { SchemaName = "nxa_errordetails", DisplayName = "Error Details", Type = FieldType.Memo, Required = false },
            },
        },
    };

    /// <summary>Dates that render as DateOnly rather than DateAndTime, matching the
    /// Python script's tag-based rule.</summary>
    public static readonly HashSet<string> DateOnlyDisplayNames = new(StringComparer.Ordinal)
    {
        "Generated On", "Approval Date", "Executed On", "Estimated Close Date", "Start Date", "End Date",
    };
}
