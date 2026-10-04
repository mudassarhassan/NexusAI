using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace NexusAI.Agent;

/// <summary>
/// Read-only Dataverse data-access layer. Named after the tool functions
/// listed in the project README's Phase 2 ("Dataverse Integration"):
/// get_account, get_contact, get_account_opportunities, get_contact_activities,
/// get_customer_products (-> product holdings here), get_product, plus the
/// composed get_customer_360.
///
/// Strictly read-only: every method here only issues Retrieve/RetrieveMultiple
/// calls. No LLM reasoning happens in this layer -- see the project README,
/// Section 6 (Architecture), for why that separation matters.
///
/// Field-name notes (carried over from the earlier Python port's findings):
/// nxa_lead has nxa_emailaddress / nxa_telephone (not nxa_email / nxa_phone),
/// and no description field. nxa_opportunity has nxa_closeprobability /
/// nxa_estimatedclosedate (not nxa_probability / nxa_expectedclosedate), and
/// no description field either.
/// </summary>
public static class DataverseTools
{
    // ---------------------------------------------------------------
    // get_account
    // ---------------------------------------------------------------
    public static AccountInfo GetAccount(IOrganizationService svc, Guid accountId)
    {
        Entity entity;
        try
        {
            entity = svc.Retrieve("account", accountId, new ColumnSet(
                "accountid", "name", "accountnumber", "emailaddress1", "telephone1",
                "address1_city", "industrycode", "statecode", "statuscode", "ownerid"));
        }
        catch (Exception)
        {
            throw new AccountNotFoundException(accountId);
        }

        return new AccountInfo(
            entity.Id,
            entity.GetAttributeValue<string>("name"),
            entity.GetAttributeValue<string>("accountnumber"),
            entity.GetAttributeValue<string>("emailaddress1"),
            entity.GetAttributeValue<string>("telephone1"),
            entity.GetAttributeValue<string>("address1_city"),
            entity.GetAttributeValue<OptionSetValue>("industrycode")?.Value,
            entity.GetAttributeValue<OptionSetValue>("statecode")?.Value,
            entity.GetAttributeValue<OptionSetValue>("statuscode")?.Value,
            entity.GetAttributeValue<EntityReference>("ownerid")?.Id);
    }

    // ---------------------------------------------------------------
    // search_accounts_by_name -- resolves a name search (e.g. a company
    // name a relationship manager typed) to candidate Account records,
    // since nobody knows a GUID off the top of their head. Returns just
    // id + name: enough to disambiguate, not a full record (call
    // GetAccount with the chosen id for that).
    // ---------------------------------------------------------------
    public static List<(Guid AccountId, string? Name)> SearchAccountsByName(IOrganizationService svc, string nameContains, int top = 10)
    {
        var query = new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("accountid", "name"),
            TopCount = top,
        };
        query.Criteria.AddCondition("name", ConditionOperator.Like, $"%{nameContains}%");
        query.AddOrder("name", OrderType.Ascending);
        var result = svc.RetrieveMultiple(query);
        return result.Entities
            .Select(e => (e.Id, e.GetAttributeValue<string>("name")))
            .ToList();
    }

    // ---------------------------------------------------------------
    // search_contacts_by_name -- same idea, for an individual's name
    // (e.g. "Joe Doe"). Matches against firstname OR lastname OR fullname
    // containing the search term, since a banker might type a first name,
    // last name, or both.
    // ---------------------------------------------------------------
    public static List<(Guid ContactId, string? FullName)> SearchContactsByName(IOrganizationService svc, string nameContains, int top = 10)
    {
        var query = new QueryExpression("contact")
        {
            ColumnSet = new ColumnSet("contactid", "fullname"),
            TopCount = top,
            Criteria = new FilterExpression(LogicalOperator.Or),
        };
        query.Criteria.AddCondition("firstname", ConditionOperator.Like, $"%{nameContains}%");
        query.Criteria.AddCondition("lastname", ConditionOperator.Like, $"%{nameContains}%");
        query.Criteria.AddCondition("fullname", ConditionOperator.Like, $"%{nameContains}%");
        query.AddOrder("fullname", OrderType.Ascending);
        var result = svc.RetrieveMultiple(query);
        return result.Entities
            .Select(e => (e.Id, e.GetAttributeValue<string>("fullname")))
            .ToList();
    }

    // ---------------------------------------------------------------
    // get_contact (single contact by id)
    // ---------------------------------------------------------------
    public static ContactInfo GetContact(IOrganizationService svc, Guid contactId)
    {
        var entity = svc.Retrieve("contact", contactId, new ColumnSet(
            "contactid", "firstname", "lastname", "fullname", "emailaddress1",
            "mobilephone", "jobtitle", "parentcustomerid", "statecode", "statuscode", "ownerid"));
        return MapContact(entity);
    }

    /// <summary>All contacts whose parentcustomerid is this account (used by GetCustomer360).</summary>
    public static List<ContactInfo> GetContactsForAccount(IOrganizationService svc, Guid accountId)
    {
        var query = new QueryExpression("contact")
        {
            ColumnSet = new ColumnSet(
                "contactid", "firstname", "lastname", "fullname", "emailaddress1",
                "mobilephone", "jobtitle", "parentcustomerid", "statecode", "statuscode", "ownerid"),
        };
        query.Criteria.AddCondition("parentcustomerid", ConditionOperator.Equal, accountId);
        var result = svc.RetrieveMultiple(query);
        return result.Entities.Select(MapContact).ToList();
    }

    private static ContactInfo MapContact(Entity entity) => new(
        entity.Id,
        entity.GetAttributeValue<string>("firstname"),
        entity.GetAttributeValue<string>("lastname"),
        entity.GetAttributeValue<string>("fullname"),
        entity.GetAttributeValue<string>("emailaddress1"),
        entity.GetAttributeValue<string>("mobilephone"),
        entity.GetAttributeValue<string>("jobtitle"),
        entity.GetAttributeValue<EntityReference>("parentcustomerid")?.Id,
        entity.GetAttributeValue<OptionSetValue>("statecode")?.Value,
        entity.GetAttributeValue<OptionSetValue>("statuscode")?.Value,
        entity.GetAttributeValue<EntityReference>("ownerid")?.Id);

    // ---------------------------------------------------------------
    // Leads for an account (used by GetCustomer360; not separately named in
    // the README's Phase 2 list, but needed for the Customer 360 contract)
    // ---------------------------------------------------------------
    public static List<LeadInfo> GetLeadsForAccount(IOrganizationService svc, Guid accountId)
    {
        var query = new QueryExpression("nxa_lead")
        {
            ColumnSet = new ColumnSet(
                "nxa_leadid", "nxa_name", "nxa_firstname", "nxa_lastname", "nxa_companyname",
                "nxa_emailaddress", "nxa_telephone", "nxa_leadsource", "nxa_rating", "nxa_status",
                "nxa_accountid", "nxa_contactid", "ownerid"),
        };
        query.Criteria.AddCondition("nxa_accountid", ConditionOperator.Equal, accountId);
        var result = svc.RetrieveMultiple(query);
        return result.Entities.Select(e => new LeadInfo(
            e.Id,
            e.GetAttributeValue<string>("nxa_name"),
            e.GetAttributeValue<string>("nxa_firstname"),
            e.GetAttributeValue<string>("nxa_lastname"),
            e.GetAttributeValue<string>("nxa_companyname"),
            e.GetAttributeValue<string>("nxa_emailaddress"),
            e.GetAttributeValue<string>("nxa_telephone"),
            OptionSetLabels.LeadSourceLabel(e.GetAttributeValue<OptionSetValue>("nxa_leadsource")?.Value),
            OptionSetLabels.RatingLabel(e.GetAttributeValue<OptionSetValue>("nxa_rating")?.Value),
            OptionSetLabels.LeadStatusLabel(e.GetAttributeValue<OptionSetValue>("nxa_status")?.Value),
            e.GetAttributeValue<EntityReference>("nxa_accountid")?.Id,
            e.GetAttributeValue<EntityReference>("nxa_contactid")?.Id,
            e.GetAttributeValue<EntityReference>("ownerid")?.Id
        )).ToList();
    }

    // ---------------------------------------------------------------
    // get_account_opportunities
    // ---------------------------------------------------------------
    public static List<OpportunityInfo> GetAccountOpportunities(IOrganizationService svc, Guid accountId)
    {
        var query = new QueryExpression("nxa_opportunity")
        {
            ColumnSet = new ColumnSet(
                "nxa_opportunityid", "nxa_name", "nxa_accountid", "nxa_contactid", "nxa_leadid",
                "nxa_estimatedvalue", "nxa_totalamount", "nxa_closeprobability", "nxa_estimatedclosedate",
                "nxa_salesstage", "nxa_rating", "nxa_status", "ownerid"),
        };
        query.Criteria.AddCondition("nxa_accountid", ConditionOperator.Equal, accountId);
        var result = svc.RetrieveMultiple(query);
        return result.Entities.Select(e => new OpportunityInfo(
            e.Id,
            e.GetAttributeValue<string>("nxa_name"),
            e.GetAttributeValue<EntityReference>("nxa_accountid")?.Id,
            e.GetAttributeValue<EntityReference>("nxa_contactid")?.Id,
            e.GetAttributeValue<EntityReference>("nxa_leadid")?.Id,
            e.GetAttributeValue<decimal?>("nxa_estimatedvalue"),
            e.GetAttributeValue<decimal?>("nxa_totalamount"),
            e.GetAttributeValue<int?>("nxa_closeprobability"),
            e.GetAttributeValue<DateTime?>("nxa_estimatedclosedate"),
            OptionSetLabels.SalesStageLabel(e.GetAttributeValue<OptionSetValue>("nxa_salesstage")?.Value),
            OptionSetLabels.RatingLabel(e.GetAttributeValue<OptionSetValue>("nxa_rating")?.Value),
            OptionSetLabels.OpportunityStatusLabel(e.GetAttributeValue<OptionSetValue>("nxa_status")?.Value),
            e.GetAttributeValue<EntityReference>("ownerid")?.Id
        )).ToList();
    }

    // ---------------------------------------------------------------
    // get_customer_products -> product holdings
    // Works for either an account or a contact id (a holding belongs to one
    // or the other, not necessarily both -- see the data model doc).
    // ---------------------------------------------------------------
    public static List<ProductHoldingInfo> GetCustomerProductHoldings(IOrganizationService svc, Guid customerId)
    {
        var query = new QueryExpression("nxa_productholding")
        {
            ColumnSet = new ColumnSet(
                "nxa_productholdingid", "nxa_name", "nxa_accountid", "nxa_contactid", "nxa_productid",
                "nxa_holdingnumber", "nxa_startdate", "nxa_enddate", "nxa_balance", "nxa_interestrate", "nxa_status"),
            Criteria = new FilterExpression(LogicalOperator.Or),
        };
        query.Criteria.AddCondition("nxa_accountid", ConditionOperator.Equal, customerId);
        query.Criteria.AddCondition("nxa_contactid", ConditionOperator.Equal, customerId);
        var result = svc.RetrieveMultiple(query);
        return result.Entities.Select(e => new ProductHoldingInfo(
            e.Id,
            e.GetAttributeValue<string>("nxa_name"),
            e.GetAttributeValue<EntityReference>("nxa_accountid")?.Id,
            e.GetAttributeValue<EntityReference>("nxa_contactid")?.Id,
            e.GetAttributeValue<EntityReference>("nxa_productid")?.Id,
            e.GetAttributeValue<string>("nxa_holdingnumber"),
            e.GetAttributeValue<DateTime?>("nxa_startdate"),
            e.GetAttributeValue<DateTime?>("nxa_enddate"),
            e.GetAttributeValue<decimal?>("nxa_balance"),
            e.GetAttributeValue<decimal?>("nxa_interestrate"),
            OptionSetLabels.HoldingStatusLabel(e.GetAttributeValue<OptionSetValue>("nxa_status")?.Value)
        )).ToList();
    }

    // ---------------------------------------------------------------
    // get_product (single product by id)
    // ---------------------------------------------------------------
    public static ProductInfo GetProduct(IOrganizationService svc, Guid productId)
    {
        var entity = svc.Retrieve("nxa_product", productId, new ColumnSet(
            "nxa_productid", "nxa_name", "nxa_productnumber", "nxa_producttype",
            "nxa_currentcost", "nxa_price", "nxa_parentproductid"));
        return new ProductInfo(
            entity.Id,
            entity.GetAttributeValue<string>("nxa_name"),
            entity.GetAttributeValue<string>("nxa_productnumber"),
            OptionSetLabels.ProductTypeLabel(entity.GetAttributeValue<OptionSetValue>("nxa_producttype")?.Value),
            entity.GetAttributeValue<decimal?>("nxa_currentcost"),
            entity.GetAttributeValue<decimal?>("nxa_price"),
            entity.GetAttributeValue<EntityReference>("nxa_parentproductid")?.Id);
    }

    // ---------------------------------------------------------------
    // get_contact_activities
    // ---------------------------------------------------------------
    public static List<ActivityInfo> GetActivitiesForRegardingObject(IOrganizationService svc, Guid regardingObjectId, int top = 20)
    {
        var query = new QueryExpression("activitypointer")
        {
            ColumnSet = new ColumnSet(
                "activityid", "subject", "description", "activitytypecode", "statecode", "statuscode",
                "ownerid", "regardingobjectid", "scheduledstart", "scheduledend", "actualstart", "actualend"),
            TopCount = top,
        };
        query.Criteria.AddCondition("regardingobjectid", ConditionOperator.Equal, regardingObjectId);
        query.AddOrder("createdon", OrderType.Descending);
        var result = svc.RetrieveMultiple(query);
        return result.Entities.Select(e => new ActivityInfo(
            e.Id,
            e.GetAttributeValue<string>("subject"),
            e.GetAttributeValue<string>("description"),
            e.GetAttributeValue<OptionSetValue>("activitytypecode")?.Value,
            e.GetAttributeValue<OptionSetValue>("statecode")?.Value,
            e.GetAttributeValue<OptionSetValue>("statuscode")?.Value,
            e.GetAttributeValue<EntityReference>("ownerid")?.Id,
            e.GetAttributeValue<EntityReference>("regardingobjectid")?.Id,
            e.GetAttributeValue<DateTime?>("scheduledstart"),
            e.GetAttributeValue<DateTime?>("scheduledend"),
            e.GetAttributeValue<DateTime?>("actualstart"),
            e.GetAttributeValue<DateTime?>("actualend")
        )).ToList();
    }

    // ---------------------------------------------------------------
    // get_customer_360 -- composes the above into the structured contract.
    // Every related list is [] (never an error, never omitted) when that
    // table has no related records for this account.
    // ---------------------------------------------------------------
    public static Customer360Result GetCustomer360(IOrganizationService svc, Guid accountId)
    {
        var account = GetAccount(svc, accountId); // throws AccountNotFoundException if missing
        return new Customer360Result(
            account,
            GetContactsForAccount(svc, accountId),
            GetLeadsForAccount(svc, accountId),
            GetAccountOpportunities(svc, accountId),
            GetCustomerProductHoldings(svc, accountId),
            GetActivitiesForRegardingObject(svc, accountId));
    }
}