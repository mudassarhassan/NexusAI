using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseModelGenerator;

/// <summary>
/// Generates synthetic sample data across all NexusAI tables, in dependency
/// order. Not idempotent by default -- running with all steps selected
/// creates a fresh batch every time. Pass a subset of steps to add data to
/// just those tables while reusing (not duplicating) existing records in the
/// tables you leave out -- mirrors generate_sample_data.py's --only flag.
/// </summary>
public static class SampleDataGenerator
{
    private static readonly Random Rng = new(42); // fixed seed: reproducible-but-varied, matches the Python script

    public static void Run(IOrganizationService svc, HashSet<string> selectedSteps, Dictionary<string, int> recordCounts)
    {
        var usedCompanyNames = new HashSet<string>();
        var usedPersonNames = new HashSet<(string, string)>();

        // ---- 1. Accounts ----
        List<Guid> accountIds;
        if (selectedSteps.Contains("accounts"))
        {
            accountIds = CreateAccounts(svc, recordCounts["accounts"], usedCompanyNames);
        }
        else
        {
            accountIds = GetExistingIds(svc, "account");
            Log.Info($"Reusing {accountIds.Count} existing accounts (skipped creating new ones).");
        }

        // ---- 2. Contacts ----
        List<Guid> contactIds;
        if (selectedSteps.Contains("contacts"))
        {
            contactIds = CreateContacts(svc, recordCounts["contacts"], accountIds, usedPersonNames);
        }
        else
        {
            contactIds = GetExistingIds(svc, "contact");
            Log.Info($"Reusing {contactIds.Count} existing contacts (skipped creating new ones).");
        }

        // ---- 3. Products ----
        List<Guid> productIds;
        if (selectedSteps.Contains("products"))
        {
            productIds = CreateProducts(svc, recordCounts["products"]);
        }
        else
        {
            productIds = GetExistingIds(svc, "nxa_product");
            Log.Info($"Reusing {productIds.Count} existing products (skipped creating new ones).");
        }

        // ---- 3b. Product Holdings ----
        if (selectedSteps.Contains("product_holdings"))
        {
            CreateProductHoldings(svc, recordCounts["product_holdings"], accountIds, contactIds, productIds);
        }
        else
        {
            Log.Info("Skipping nxa_productholding (not selected).");
        }

        // ---- 4. Leads ----
        List<Guid> leadIds;
        if (selectedSteps.Contains("leads"))
        {
            leadIds = CreateLeads(svc, recordCounts["leads"], accountIds, contactIds, usedCompanyNames);
        }
        else if (selectedSteps.Contains("opportunities") || selectedSteps.Contains("recommendations") || selectedSteps.Contains("action_logs"))
        {
            leadIds = GetExistingIds(svc, "nxa_lead");
            Log.Info($"Reusing {leadIds.Count} existing leads (skipped creating new ones).");
        }
        else
        {
            leadIds = [];
        }

        // ---- 5. Opportunities ----
        List<Guid> opportunityIds;
        if (selectedSteps.Contains("opportunities"))
        {
            opportunityIds = CreateOpportunities(svc, recordCounts["opportunities"], accountIds, contactIds, leadIds, usedCompanyNames);
        }
        else if (selectedSteps.Contains("recommendations") || selectedSteps.Contains("action_logs"))
        {
            opportunityIds = GetExistingIds(svc, "nxa_opportunity");
            Log.Info($"Reusing {opportunityIds.Count} existing opportunities (skipped creating new ones).");
        }
        else
        {
            opportunityIds = [];
        }

        // ---- 6. AI Recommendations ----
        if (selectedSteps.Contains("recommendations"))
        {
            CreateRecommendations(svc, recordCounts["recommendations"], accountIds, contactIds, leadIds, opportunityIds, productIds);
        }
        else
        {
            Log.Info("Skipping nxa_airecommendation (not selected).");
        }

        // ---- 7. AI Action Log ----
        if (selectedSteps.Contains("action_logs"))
        {
            CreateActionLogs(svc, recordCounts["action_logs"], accountIds, contactIds, leadIds, opportunityIds);
        }
        else
        {
            Log.Info("Skipping nxa_aiactionlog (not selected).");
        }

        Log.Info($"Sample data generation complete. Created new records for: {string.Join(", ", selectedSteps.OrderBy(s => Array.IndexOf(SampleDataPools.StepKeys, s)))}.");
    }

    private static List<Guid> GetExistingIds(IOrganizationService svc, string logicalName, int top = 5000)
    {
        var query = new QueryExpression(logicalName) { ColumnSet = new ColumnSet(false), TopCount = top };
        var result = svc.RetrieveMultiple(query);
        var ids = result.Entities.Select(e => e.Id).ToList();
        if (ids.Count == 0)
        {
            throw new InvalidOperationException(
                $"No existing records found in '{logicalName}'. A later step depends on these existing -- " +
                $"run that step at least once first (include it in your selection).");
        }
        return ids;
    }

    private static string RandomCompanyName(HashSet<string> used)
    {
        while (true)
        {
            var name = $"{Pick(SampleDataPools.CompanyCore)} {Pick(SampleDataPools.CompanySuffix)}";
            if (used.Add(name))
            {
                return name;
            }
        }
    }

    private static (string First, string Last) RandomPerson(HashSet<(string, string)> used)
    {
        while (true)
        {
            var first = Pick(SampleDataPools.FirstNames);
            var last = Pick(SampleDataPools.LastNames);
            if (used.Add((first, last)))
            {
                return (first, last);
            }
        }
    }

    private static T Pick<T>(IReadOnlyList<T> items) => items[Rng.Next(items.Count)];

    private static DateTime DaysAgo(int days) => DateTime.UtcNow.AddDays(-days);
    private static DateTime DaysAhead(int days) => DateTime.UtcNow.AddDays(days);

    private static void Progress(string label, int i, int total)
    {
        var step = Math.Max(total / 10, 1);
        if (i == 0 || (i + 1) % step == 0 || i + 1 == total)
        {
            Log.Info($"{label}: {i + 1}/{total}");
        }
    }

    // ---------------------------------------------------------------
    // 1. Accounts
    // ---------------------------------------------------------------
    private static List<Guid> CreateAccounts(IOrganizationService svc, int n, HashSet<string> usedCompanyNames)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            var name = RandomCompanyName(usedCompanyNames);
            var entity = new Entity("account")
            {
                ["name"] = name,
                ["accountnumber"] = $"ACC-{1000 + i}",
                ["emailaddress1"] = $"contact@{name.Split(' ')[0].ToLowerInvariant().Replace("'", "")}.example.com",
                ["telephone1"] = $"07 3{100 + i % 900:D3} {2000 + i:D4}",
            };
            ids.Add(svc.Create(entity));
            Progress("Accounts created", i, n);
        }
        return ids;
    }

    // ---------------------------------------------------------------
    // 2. Contacts
    // ---------------------------------------------------------------
    private static List<Guid> CreateContacts(IOrganizationService svc, int n, List<Guid> accountIds, HashSet<(string, string)> usedPersonNames)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            var (first, last) = RandomPerson(usedPersonNames);
            var accountId = Pick(accountIds);
            var entity = new Entity("contact")
            {
                ["firstname"] = first,
                ["lastname"] = last,
                ["jobtitle"] = Pick(SampleDataPools.JobTitles),
                ["emailaddress1"] = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant().Replace("'", "")}@example.com",
                ["mobilephone"] = $"04{10 + i % 90:D2} {100 + i % 900:D3} {200 + i % 800:D3}",
                ["parentcustomerid"] = new EntityReference("account", accountId),
            };
            ids.Add(svc.Create(entity));
            Progress("Contacts created", i, n);
        }
        return ids;
    }

    // ---------------------------------------------------------------
    // 3. Products
    // ---------------------------------------------------------------
    private static List<Guid> CreateProducts(IOrganizationService svc, int requestedN)
    {
        var defs = SampleDataPools.ProductDefs;
        var n = Math.Min(requestedN, defs.Length);
        if (requestedN > defs.Length)
        {
            Log.Info($"Requested {requestedN} products but only {defs.Length} distinct product templates are defined; creating {defs.Length}.");
        }

        var ids = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            var (name, productType, cost, price) = defs[i];
            var entity = new Entity("nxa_product")
            {
                ["nxa_name"] = name,
                ["nxa_productnumber"] = $"PRD-{1000 + i}",
                ["nxa_producttype"] = new OptionSetValue(OptionValue("nxa_product", "nxa_producttype", productType)),
                ["nxa_currentcost"] = cost,
                ["nxa_price"] = price,
            };
            ids.Add(svc.Create(entity));
            Progress("Products created", i, n);
        }
        return ids;
    }

    // ---------------------------------------------------------------
    // 3b. Product Holdings
    // ---------------------------------------------------------------
    private static void CreateProductHoldings(IOrganizationService svc, int n, List<Guid> accountIds, List<Guid> contactIds, List<Guid> productIds)
    {
        var statuses = new[] { "Active", "Closed", "Matured", "Suspended" };
        for (var i = 0; i < n; i++)
        {
            var productId = Pick(productIds);
            var heldByAccount = Rng.NextDouble() < 0.6;
            var status = Pick(statuses);
            var isClosed = status is "Closed" or "Matured";

            var entity = new Entity("nxa_productholding")
            {
                ["nxa_name"] = $"Product Holding {i + 1:D4}",
                ["nxa_holdingnumber"] = $"HLD-{10000 + i}",
                ["nxa_startdate"] = DaysAgo(Rng.Next(30, 1500)),
                ["nxa_balance"] = (decimal)Math.Round(Rng.NextDouble() * (450000 - 500) + 500, 2),
                ["nxa_interestrate"] = (decimal)Math.Round(Rng.NextDouble() * (9.5 - 0.5) + 0.5, 2),
                ["nxa_status"] = new OptionSetValue(OptionValue("nxa_productholding", "nxa_status", status)),
                ["nxa_productid"] = new EntityReference("nxa_product", productId),
            };
            if (heldByAccount)
            {
                entity["nxa_accountid"] = new EntityReference("account", Pick(accountIds));
            }
            else
            {
                entity["nxa_contactid"] = new EntityReference("contact", Pick(contactIds));
            }
            if (isClosed)
            {
                entity["nxa_enddate"] = DaysAgo(Rng.Next(0, 29));
            }
            svc.Create(entity);
            Progress("Product holdings created", i, n);
        }
    }

    // ---------------------------------------------------------------
    // 4. Leads
    // ---------------------------------------------------------------
    private static List<Guid> CreateLeads(IOrganizationService svc, int n, List<Guid> accountIds, List<Guid> contactIds, HashSet<string> usedCompanyNames)
    {
        var sources = new[] { "Web", "Referral", "Advertisement", "Cold Call", "Partner", "Other" };
        var ratings = new[] { "Hot", "Warm", "Cold" };
        var statuses = new[] { "New", "Contacted", "Qualified", "Disqualified", "Converted" };

        var ids = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            var first = Pick(SampleDataPools.FirstNames);
            var last = Pick(SampleDataPools.LastNames);
            var company = RandomCompanyName(usedCompanyNames);
            var topic = $"{Pick(SampleDataPools.LeadTopicTemplates)} - {company}";

            var entity = new Entity("nxa_lead")
            {
                ["nxa_name"] = topic,
                ["nxa_firstname"] = first,
                ["nxa_lastname"] = last,
                ["nxa_companyname"] = company,
                ["nxa_emailaddress"] = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant().Replace("'", "")}@example.com",
                ["nxa_telephone"] = $"04{20 + i % 80:D2} {300 + i % 700:D3} {400 + i % 600:D3}",
                ["nxa_leadsource"] = new OptionSetValue(OptionValue("nxa_lead", "nxa_leadsource", Pick(sources))),
                ["nxa_rating"] = new OptionSetValue(OptionValue("nxa_lead", "nxa_rating", Pick(ratings))),
                ["nxa_status"] = new OptionSetValue(OptionValue("nxa_lead", "nxa_status", Pick(statuses))),
            };
            if (Rng.NextDouble() < 0.5)
            {
                entity["nxa_accountid"] = new EntityReference("account", Pick(accountIds));
                entity["nxa_contactid"] = new EntityReference("contact", Pick(contactIds));
            }
            ids.Add(svc.Create(entity));
            Progress("Leads created", i, n);
        }
        return ids;
    }

    // ---------------------------------------------------------------
    // 5. Opportunities
    // ---------------------------------------------------------------
    private static List<Guid> CreateOpportunities(IOrganizationService svc, int n, List<Guid> accountIds, List<Guid> contactIds, List<Guid> leadIds, HashSet<string> usedCompanyNames)
    {
        var stages = new[] { "Qualify", "Develop", "Propose", "Close" };
        var ratings = new[] { "Hot", "Warm", "Cold" };
        var statuses = new[] { "Open", "Won", "Lost" };

        var ids = new List<Guid>();
        for (var i = 0; i < n; i++)
        {
            var company = RandomCompanyName(usedCompanyNames);
            var name = $"{Pick(SampleDataPools.OpportunityTemplates)} - {company}";
            var estValue = (decimal)Math.Round(Rng.NextDouble() * (500000 - 5000) + 5000, 2);

            var entity = new Entity("nxa_opportunity")
            {
                ["nxa_name"] = name,
                ["nxa_estimatedvalue"] = estValue,
                ["nxa_totalamount"] = estValue,
                ["nxa_closeprobability"] = Rng.Next(10, 96),
                ["nxa_estimatedclosedate"] = DaysAhead(Rng.Next(10, 180)),
                ["nxa_salesstage"] = new OptionSetValue(OptionValue("nxa_opportunity", "nxa_salesstage", Pick(stages))),
                ["nxa_rating"] = new OptionSetValue(OptionValue("nxa_opportunity", "nxa_rating", Pick(ratings))),
                ["nxa_status"] = new OptionSetValue(OptionValue("nxa_opportunity", "nxa_status", Pick(statuses))),
                ["nxa_accountid"] = new EntityReference("account", Pick(accountIds)),
                ["nxa_contactid"] = new EntityReference("contact", Pick(contactIds)),
                ["nxa_leadid"] = new EntityReference("nxa_lead", Pick(leadIds)),
            };
            ids.Add(svc.Create(entity));
            Progress("Opportunities created", i, n);
        }
        return ids;
    }

    // ---------------------------------------------------------------
    // 6. AI Recommendations
    // ---------------------------------------------------------------
    private static void CreateRecommendations(IOrganizationService svc, int n, List<Guid> accountIds, List<Guid> contactIds, List<Guid> leadIds, List<Guid> opportunityIds, List<Guid> productIds)
    {
        var statuses = new[] { "Draft", "Pending Approval", "Approved", "Rejected", "Completed", "Cancelled" };

        for (var i = 0; i < n; i++)
        {
            var (title, reason) = Pick(SampleDataPools.RecommendationTemplates);
            var status = Pick(statuses);
            var approved = status is "Approved" or "Completed";

            var entity = new Entity("nxa_airecommendation")
            {
                ["nxa_name"] = title,
                ["nxa_recommendation"] = title,
                ["nxa_reason"] = reason,
                ["nxa_supportingevidence"] = $"Derived from account activity and product usage patterns as of {DateTime.UtcNow:yyyy-MM-dd}.",
                ["nxa_confidence"] = (decimal)Math.Round(Rng.NextDouble() * (98 - 50) + 50, 2),
                ["nxa_status"] = new OptionSetValue(OptionValue("nxa_airecommendation", "nxa_status", status)),
                ["nxa_generatedby"] = "NexusAI Recommendation Agent v1",
                ["nxa_generatedon"] = DaysAgo(Rng.Next(0, 30)),
                ["nxa_humanapproved"] = approved,
                ["nxa_accountid"] = new EntityReference("account", Pick(accountIds)),
                ["nxa_contactid"] = new EntityReference("contact", Pick(contactIds)),
                ["nxa_leadid"] = new EntityReference("nxa_lead", Pick(leadIds)),
                ["nxa_opportunityid"] = new EntityReference("nxa_opportunity", Pick(opportunityIds)),
                ["nxa_productid"] = new EntityReference("nxa_product", Pick(productIds)),
            };
            if (approved)
            {
                entity["nxa_approvaldate"] = DaysAgo(Rng.Next(0, 10));
            }
            svc.Create(entity);
            Progress("AI recommendations created", i, n);
        }
    }

    // ---------------------------------------------------------------
    // 7. AI Action Log
    // ---------------------------------------------------------------
    private static void CreateActionLogs(IOrganizationService svc, int n, List<Guid> accountIds, List<Guid> contactIds, List<Guid> leadIds, List<Guid> opportunityIds)
    {
        var statuses = new[] { "Proposed", "Pending Approval", "Approved", "Rejected", "Executing", "Completed", "Failed", "Cancelled" };

        for (var i = 0; i < n; i++)
        {
            var (name, actionType, description) = Pick(SampleDataPools.ActionTemplates);
            var status = Pick(statuses);
            var requiresApproval = Rng.NextDouble() < 0.5;

            var entity = new Entity("nxa_aiactionlog")
            {
                ["nxa_name"] = name,
                ["nxa_actiontype"] = new OptionSetValue(OptionValue("nxa_aiactionlog", "nxa_actiontype", actionType)),
                ["nxa_description"] = description,
                ["nxa_agent"] = "NexusAI Action Agent v1",
                ["nxa_status"] = new OptionSetValue(OptionValue("nxa_aiactionlog", "nxa_status", status)),
                ["nxa_requiresapproval"] = requiresApproval,
                ["nxa_accountid"] = new EntityReference("account", Pick(accountIds)),
                ["nxa_contactid"] = new EntityReference("contact", Pick(contactIds)),
                ["nxa_leadid"] = new EntityReference("nxa_lead", Pick(leadIds)),
                ["nxa_opportunityid"] = new EntityReference("nxa_opportunity", Pick(opportunityIds)),
            };
            if (requiresApproval && status == "Completed")
            {
                entity["nxa_approvedby"] = "Relationship Manager";
                entity["nxa_approvedon"] = DaysAgo(Rng.Next(1, 10));
            }
            if (status == "Completed")
            {
                entity["nxa_executedon"] = DaysAgo(Rng.Next(0, 5));
                entity["nxa_result"] = "Action completed successfully.";
            }
            svc.Create(entity);
            Progress("AI action logs created", i, n);
        }
    }

    /// <summary>
    /// Derives a picklist option's integer Value offline from TableDefinitions.cs
    /// (the same source of truth create_dataverse_model used to create the
    /// field), rather than querying the live environment -- Dataverse assigns
    /// values sequentially (100000000, 200000000, ...) in option-list order,
    /// which this mirrors exactly.
    /// </summary>
    private static int OptionValue(string tableLogicalName, string fieldSchemaName, string label)
    {
        var table = DataModel.CustomTables.First(t => t.LogicalName == tableLogicalName);
        var field = table.Fields.First(f => f.SchemaName == fieldSchemaName);
        var options = field.OptionSet ?? throw new InvalidOperationException($"{tableLogicalName}.{fieldSchemaName} has no OptionSet defined.");
        var index = Array.IndexOf(options, label);
        if (index < 0)
        {
            throw new InvalidOperationException($"Label '{label}' not found in {tableLogicalName}.{fieldSchemaName} option set.");
        }
        return (index + 1) * 100000000;
    }
}
