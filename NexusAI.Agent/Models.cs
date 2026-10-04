namespace NexusAI.Agent;

public sealed record AccountInfo(
    Guid AccountId,
    string? Name,
    string? AccountNumber,
    string? Email,
    string? Phone,
    string? City,
    int? IndustryCode,
    int? StateCode,
    int? StatusCode,
    Guid? OwnerId);

public sealed record ContactInfo(
    Guid ContactId,
    string? FirstName,
    string? LastName,
    string? FullName,
    string? Email,
    string? MobilePhone,
    string? JobTitle,
    Guid? ParentCustomerId,
    int? StateCode,
    int? StatusCode,
    Guid? OwnerId);

public sealed record LeadInfo(
    Guid LeadId,
    string? Name,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? LeadSource,
    string? Rating,
    string? Status,
    Guid? AccountId,
    Guid? ContactId,
    Guid? OwnerId);

public sealed record OpportunityInfo(
    Guid OpportunityId,
    string? Name,
    Guid? AccountId,
    Guid? ContactId,
    Guid? LeadId,
    decimal? EstimatedValue,
    decimal? TotalAmount,
    int? CloseProbability,
    DateTime? EstimatedCloseDate,
    string? SalesStage,
    string? Rating,
    string? Status,
    Guid? OwnerId);

public sealed record ProductHoldingInfo(
    Guid ProductHoldingId,
    string? Name,
    Guid? AccountId,
    Guid? ContactId,
    Guid? ProductId,
    string? HoldingNumber,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal? Balance,
    decimal? InterestRate,
    string? Status);

public sealed record ActivityInfo(
    Guid ActivityId,
    string? Subject,
    string? Description,
    int? ActivityTypeCode,
    int? StateCode,
    int? StatusCode,
    Guid? OwnerId,
    Guid? RegardingObjectId,
    DateTime? ScheduledStart,
    DateTime? ScheduledEnd,
    DateTime? ActualStart,
    DateTime? ActualEnd);

public sealed record ProductInfo(
    Guid ProductId,
    string? Name,
    string? ProductNumber,
    string? ProductType,
    decimal? CurrentCost,
    decimal? Price,
    Guid? ParentProductId);

public sealed record Customer360Result(
    AccountInfo Account,
    IReadOnlyList<ContactInfo> Contacts,
    IReadOnlyList<LeadInfo> Leads,
    IReadOnlyList<OpportunityInfo> Opportunities,
    IReadOnlyList<ProductHoldingInfo> ProductHoldings,
    IReadOnlyList<ActivityInfo> Activities);

public sealed class AccountNotFoundException(Guid accountId)
    : Exception($"Account not found: {accountId}")
{
    public Guid AccountId { get; } = accountId;
}
