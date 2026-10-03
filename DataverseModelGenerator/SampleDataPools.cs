namespace DataverseModelGenerator;

/// <summary>
/// Static content pools for synthetic data generation. Exact line-by-line
/// port of the name/template lists in generate_sample_data.py, including
/// its intentional-or-not duplicate entries (e.g. "Samuel" appears twice in
/// FirstNames, "Pty Ltd" appears twice in CompanySuffix) -- preserved here
/// so relative random-selection weighting matches the Python version exactly.
/// </summary>
public static class SampleDataPools
{
    // Dependency order: a step can only link to records from a step listed
    // before it (e.g. ProductHoldings needs Accounts/Contacts/Products).
    public static readonly string[] StepKeys =
    [
        "accounts", "contacts", "products", "product_holdings",
        "leads", "opportunities", "recommendations", "action_logs",
    ];

    public static readonly Dictionary<string, int> DefaultRecordCounts = new()
    {
        ["accounts"] = 60,
        ["contacts"] = 80,
        ["products"] = 15,
        ["product_holdings"] = 90,
        ["leads"] = 80,
        ["opportunities"] = 70,
        ["recommendations"] = 90,
        ["action_logs"] = 70,
    };

    public static readonly string[] FirstNames =
    [
        "Olivia", "Liam", "Ava", "Noah", "Isla", "Jack", "Mia", "Lucas", "Harrison",
        "Grace", "Ethan", "Chloe", "Henry", "Amelia", "Jacob", "Zoe", "Samuel", "Ruby",
        "Charlotte", "William", "Sophie", "James", "Emily", "Oliver", "Ella", "Thomas",
        "Lily", "Benjamin", "Hannah", "Daniel", "Matilda", "Alexander", "Scarlett",
        "Michael", "Audrey", "Joshua", "Georgia", "Nathan", "Harper", "Samuel",
        "Freya", "Cooper", "Willow", "Riley", "Poppy", "Mason", "Evie", "Hugo",
    ];

    public static readonly string[] LastNames =
    [
        "Lee", "O'Connor", "Mitchell", "Baxter", "Walsh", "Foster", "Reid", "Sullivan",
        "Doyle", "Nash", "Nguyen", "Patterson", "Singh", "Williams", "Thompson",
        "Robertson", "Campbell", "Brennan", "Carter", "Bennett", "Hughes", "Murphy",
        "Kelly", "Ryan", "Byrne", "Walker", "Hayes", "Fox", "Dunn", "Shaw", "Barrett",
        "Gallagher", "Quinn", "Price", "Marsh", "Lambert", "Pearce", "Chapman",
    ];

    public static readonly string[] CompanyCore =
    [
        "Meadowbrook", "Sunrise", "Harborview", "Blue Gum", "Fernridge", "Lee Family",
        "O'Connor", "Mitchell Household", "Baxter Retail", "Walsh Super Fund",
        "Foster", "Reid Earthmoving", "Sullivan Creative", "Doyle Household",
        "Nash Cafe", "Ironbark", "Silverleaf", "Red Gum", "Northbank", "Southport",
        "Riverside", "Hillcrest", "Coastal", "Highland", "Stonegate", "Wattlewood",
        "Goldfield", "Brightwater", "Eastgate", "Westmead", "Clearview", "Oakridge",
        "Pinehurst", "Marlowe", "Cedarbrook", "Thornbury", "Greystone", "Ashwood",
        "Lakeside", "Summerhill",
    ];

    public static readonly string[] CompanySuffix =
    [
        "Pty Ltd", "Group", "Holdings Pty Ltd", "Trading Co", "Logistics Pty Ltd",
        "Construction Pty Ltd", "Hospitality Group", "Consulting", "Retail Group",
        "Studio", "Pty Ltd", "& Co", "Super Fund", "Household", "Enterprises",
    ];

    public static readonly string[] JobTitles =
    [
        "Finance Manager", "Operations Director", "Owner", "CFO", "Managing Director",
        "Procurement Lead", "General Manager", "Finance Director", "Office Manager",
        "Business Owner", "Head of Operations", "Company Secretary",
    ];

    public static readonly string[] LeadTopicTemplates =
    [
        "New business banking enquiry", "Referral from existing customer",
        "Home loan refinance interest", "Business overdraft enquiry",
        "Term deposit rollover enquiry", "Credit card upgrade enquiry",
        "Equipment finance enquiry", "Savings account for new business",
        "Personal loan enquiry", "Merchant facility enquiry",
        "Foreign currency account enquiry", "Trade finance enquiry",
        "Business insurance bundling enquiry", "Asset finance enquiry",
        "Cash flow lending enquiry",
    ];

    public static readonly string[] OpportunityTemplates =
    [
        "Home loan refinance", "Business overdraft", "Term deposit rollover",
        "Equipment finance", "Merchant facility", "Credit card upgrade",
        "Personal loan", "Business banking package", "Trade finance facility",
        "Asset finance arrangement",
    ];

    public static readonly (string Title, string Reason)[] RecommendationTemplates =
    [
        ("Recommend home loan refinance package", "Customer is likely to benefit from a lower variable rate based on current market conditions."),
        ("Recommend overdraft limit increase", "Transaction history shows consistent cash flow that supports a higher facility limit."),
        ("Recommend term deposit renewal at current rate", "Existing term deposit matures soon; current rate remains competitive."),
        ("Recommend equipment finance pre-approval", "Opportunity stage and credit profile suggest high likelihood of approval."),
        ("Recommend merchant facility onboarding", "Business profile matches ideal customer segment for merchant services."),
        ("Recommend credit card product upgrade", "Spending pattern indicates customer would benefit from a higher rewards tier."),
        ("Recommend personal loan consolidation", "Customer holds multiple smaller facilities that could be consolidated for a lower rate."),
        ("Recommend business banking package", "New business account activity suggests a bundled package would reduce fees."),
        ("Recommend proactive rate review outreach", "Customer's current product rate is above the available market rate."),
        ("Recommend savings account cross-sell", "Customer holds a transaction account only; a linked savings account suits their profile."),
        ("Recommend trade finance facility", "Import/export transaction volume suggests a trade finance facility would reduce costs."),
        ("Recommend cash flow lending product", "Seasonal revenue pattern suggests a cash flow-based lending product would help smooth working capital."),
    ];

    public static readonly (string Name, string ActionType, string Description)[] ActionTemplates =
    [
        ("Send refinance offer email", "Send Communication", "Drafted and sent refinance offer summary to customer contact."),
        ("Increase overdraft limit", "Update Record", "Updated overdraft facility limit per approved recommendation."),
        ("Renew term deposit", "Update Record", "Processed term deposit renewal at current advertised rate."),
        ("Create equipment finance task", "Create Task", "Created follow-up task for relationship manager to finalize equipment finance."),
        ("Onboard merchant facility", "Create Record", "Created merchant facility record and scheduled terminal installation."),
        ("Assign lead to relationship manager", "Assign Lead", "Assigned qualified lead to regional relationship manager for follow-up."),
        ("Generate consolidation proposal", "Generate Recommendation", "Generated loan consolidation proposal for review."),
        ("Flag rate review for manual check", "Other", "Flagged account for manual rate review due to policy threshold."),
        ("Update opportunity stage", "Update Opportunity", "Advanced opportunity to the next sales stage after customer confirmation."),
        ("Send business package summary", "Send Communication", "Sent summary of recommended business banking package to contact."),
    ];

    public static readonly (string Name, string ProductType, decimal Cost, decimal Price)[] ProductDefs =
    [
        ("Everyday Savings Account", "Service", 0.0m, 0.0m),
        ("12-Month Term Deposit", "Service", 0.0m, 0.0m),
        ("24-Month Term Deposit", "Service", 0.0m, 0.0m),
        ("Personal Loan", "Service", 0.0m, 0.0m),
        ("Home Loan - Variable Rate", "Service", 0.0m, 0.0m),
        ("Home Loan - Fixed Rate", "Service", 0.0m, 0.0m),
        ("Platinum Business Credit Card", "Service", 0.0m, 99.0m),
        ("Rewards Business Credit Card", "Service", 0.0m, 49.0m),
        ("Business Overdraft Facility", "Service", 0.0m, 0.0m),
        ("Merchant Payment Terminal", "Inventory", 250.0m, 0.0m),
        ("Trade Finance Facility", "Service", 0.0m, 0.0m),
        ("Asset Finance Package", "Bundle", 0.0m, 0.0m),
        ("Foreign Currency Account", "Service", 0.0m, 0.0m),
        ("Cash Flow Lending Product", "Service", 0.0m, 0.0m),
        ("Business Insurance Bundle", "Bundle", 0.0m, 0.0m),
    ];
}