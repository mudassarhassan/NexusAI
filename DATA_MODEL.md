# NexusAI Data Model

## 1. Purpose

This document defines the Dataverse data model for the **NexusAI** project.

The model is designed to support an AI Relationship Manager / Customer 360 agent while reusing Microsoft Dataverse standard tables wherever possible.

The solution should avoid creating custom replacements for standard CRM entities **unless the standard entity is not available in the target environment**, in which case a custom equivalent is used instead.

---

# 2. Dataverse Environment

| Setting | Value |
|---|---|
| Environment | nexusAI |
| Solution | NexusAICore |
| Publisher | NexusAI |
| Publisher Prefix | nxa |
| Azure DevOps Repository | NexusAI |
| Git Branch | main |
| Dataverse Git Root Folder | `/dataverse` |

---

# 3. Data Model Principles

1. Reuse existing Dataverse standard tables where available.
2. Add standard tables to the NexusAICore solution rather than creating custom replacements, when the standard table exists in the environment.
3. Where a standard table required by the model (Lead, Opportunity) is **not present** in the target environment, create a custom `nxa_` equivalent instead of depending on platform-level provisioning (e.g. installing the Dynamics 365 Sales app).
4. Create custom tables only for NexusAI-specific capabilities or for standard tables unavailable in this environment.
5. Keep the initial model intentionally small.
6. Support Customer 360 and agentic AI scenarios.
7. Use synthetic data only.
8. Keep relationships explicit and understandable.
9. Do not create `nxa_customerproduct` at this stage.
10. Extend the model only when an actual agent use case demonstrates a gap.

---

# 4. Core Dataverse Model

The initial model consists of the following components:

```text
                         Account
                           |
          +----------------+----------------+
          |                |                |
          v                v                v
       Contact         nxa_Lead       nxa_Opportunity
          |                                 |
          |                                 |
          +-------------+-------------------+
                        |
                        v
                   nxa_Product
                        |
                        v
                    Activities

                         |
                         v
              +-----------------------+
              |   NexusAI AI Layer    |
              +-----------------------+
                 |                |
                 v                v
          AI Recommendation   AI Action Log
```

---

# 5. Standard Dataverse Tables

## 5.1 Account

**Logical name:** `account`

**Status:** Existing / reuse

The standard Account table represents the organization/customer relationship.

### Key fields

| Field | Logical Name | Type |
|---|---|---|
| Account Name | `name` | Text |
| Account Number | `accountnumber` | Text |
| Primary Contact | `primarycontactid` | Lookup → Contact |
| Email | `emailaddress1` | Email |
| Phone | `telephone1` | Phone |
| Industry | `industrycode` | Choice |
| Status | `statecode` | Choice |
| Status Reason | `statuscode` | Choice |
| Owner | `ownerid` | Owner |

### Role in NexusAI

Account is the primary organization/customer context used by the Customer 360 agent.

---

# 6. Contact

**Logical name:** `contact`

**Status:** Existing / reuse

Contact represents an individual associated with an Account or customer relationship.

### Key fields

| Field | Logical Name | Type |
|---|---|---|
| First Name | `firstname` | Text |
| Last Name | `lastname` | Text |
| Full Name | `fullname` | Text |
| Email | `emailaddress1` | Email |
| Mobile Phone | `mobilephone` | Phone |
| Parent Customer | `parentcustomerid` | Customer lookup |
| Job Title | `jobtitle` | Text |
| Status | `statecode` | Choice |
| Status Reason | `statuscode` | Choice |
| Owner | `ownerid` | Owner |

### Role in NexusAI

Contact provides the individual customer/person context for agent conversations and Customer 360.

---

# 7. NexusAI Lead (custom)

**Logical name:** `nxa_lead`

**Status:** Custom table — standard Lead is not available in this Dataverse environment (the Sales app/tables are not provisioned here), so NexusAI models Lead as a custom table instead of waiting on platform-level provisioning.

`nxa_lead` represents a potential customer or sales prospect.

### Key fields

| Field | Logical Name | Type |
|---|---|---|
| Topic | `nxa_name` | Text (primary) |
| First Name | `nxa_firstname` | Text |
| Last Name | `nxa_lastname` | Text |
| Company Name | `nxa_companyname` | Text |
| Email | `nxa_emailaddress` | Text (Email format) |
| Phone | `nxa_telephone` | Text (Phone format) |
| Account | `nxa_accountid` | Lookup → Account |
| Contact | `nxa_contactid` | Lookup → Contact |
| Lead Source | `nxa_leadsource` | Choice |
| Rating | `nxa_rating` | Choice |
| Status | `nxa_status` | Choice |

### Lead Source choices

```text
Web
Referral
Advertisement
Cold Call
Partner
Other
```

### Rating choices

```text
Hot
Warm
Cold
```

### Status choices

```text
New
Contacted
Qualified
Disqualified
Converted
```

### Role in NexusAI

The `nxa_lead` table supports AI-assisted lead qualification, summarization, prioritization, and allocation scenarios.

Potential future agent capability:

```text
New Lead
   |
   v
NexusAI
   |
   +--> Summarize lead
   +--> Identify customer context
   +--> Recommend next action
   +--> Suggest owner/allocation
```

---

# 8. NexusAI Opportunity (custom)

**Logical name:** `nxa_opportunity`

**Status:** Custom table — standard Opportunity is not available in this Dataverse environment, so NexusAI models Opportunity as a custom table instead of waiting on platform-level provisioning.

`nxa_opportunity` represents a potential sales opportunity.

### Key fields

| Field | Logical Name | Type |
|---|---|---|
| Opportunity Name | `nxa_name` | Text (primary) |
| Account | `nxa_accountid` | Lookup → Account |
| Contact | `nxa_contactid` | Lookup → Contact |
| Originating Lead | `nxa_leadid` | Lookup → nxa_lead |
| Estimated Value | `nxa_estimatedvalue` | Decimal |
| Total Amount | `nxa_totalamount` | Decimal |
| Close Probability | `nxa_closeprobability` | Whole Number (0–100) |
| Estimated Close Date | `nxa_estimatedclosedate` | Date |
| Sales Stage | `nxa_salesstage` | Choice |
| Rating | `nxa_rating` | Choice |
| Status | `nxa_status` | Choice |

### Sales Stage choices

```text
Qualify
Develop
Propose
Close
```

### Rating choices

```text
Hot
Warm
Cold
```

### Status choices

```text
Open
Won
Lost
```

### Role in NexusAI

`nxa_opportunity` provides the sales context required for AI-assisted opportunity analysis and recommendations, and preserves the Lead → Opportunity flow via `nxa_leadid`.

Potential future agent capability:

```text
Opportunity
     |
     v
NexusAI
     |
     +--> Analyze opportunity
     +--> Retrieve customer history
     +--> Retrieve relevant product knowledge
     +--> Recommend next best action
     +--> Generate meeting briefing
```

---

# 9. NexusAI Product (custom)

**Logical name:** `nxa_product`

**Status:** Custom table — standard Product is not available in this Dataverse environment, so NexusAI models Product as a custom table instead of waiting on platform-level provisioning.

`nxa_product` represents products/services that can be associated with opportunities and recommendations.

### Key fields

| Field | Logical Name | Type |
|---|---|---|
| Product Name | `nxa_name` | Text (primary) |
| Product Number | `nxa_productnumber` | Text |
| Product Type | `nxa_producttype` | Choice |
| Current Cost | `nxa_currentcost` | Decimal |
| List Price | `nxa_price` | Decimal |
| Parent Product | `nxa_parentproductid` | Lookup → nxa_product (self-referencing) |

### Product Type choices

```text
Inventory
Service
Bundle
Other
```

### Role in NexusAI

`nxa_product` provides the product catalogue used by the AI agent when generating product-related recommendations.

---

# 10. Activities

**Root logical name:** `activitypointer`

**Status:** Existing / reuse

Activities represent interactions and customer engagement.

Examples:

- Task
- Phone Call
- Appointment
- Email

### Important fields

| Field | Logical Name | Type |
|---|---|---|
| Subject | `subject` | Text |
| Description | `description` | Multiline Text |
| Owner | `ownerid` | Owner |
| Regarding | `regardingobjectid` | Lookup |
| Activity Type | `activitytypecode` | Choice |
| Status | `statecode` | Choice |
| Status Reason | `statuscode` | Choice |

### Role in NexusAI

Activities provide historical customer interaction context for Customer 360.

Example:

```text
Account / Contact
      |
      +---- Email
      +---- Phone Call
      +---- Appointment
      +---- Task
```

The agent can use this information to prepare a customer briefing.

---

# 11. NexusAI Custom Tables

Custom entities in this model:

1. `nxa_lead` (custom fallback for standard Lead — see Section 7)
2. `nxa_opportunity` (custom fallback for standard Opportunity — see Section 8)
3. `nxa_product` (custom fallback for standard Product — see Section 9)
4. `nxa_airecommendation`
5. `nxa_aiactionlog`

Do not create `nxa_customerproduct` at this stage.

---

# 12. AI Recommendation

**Logical name:** `nxa_airecommendation`

Purpose:

Stores recommendations generated by the NexusAI agent.

## Fields

| Field | Logical Name | Type | Required |
|---|---|---|---|
| Recommendation Name | `nxa_name` | Text | Yes |
| Account | `nxa_accountid` | Lookup → Account | No |
| Contact | `nxa_contactid` | Lookup → Contact | No |
| Lead | `nxa_leadid` | Lookup → nxa_lead | No |
| Opportunity | `nxa_opportunityid` | Lookup → nxa_opportunity | No |
| Product | `nxa_productid` | Lookup → Product | No |
| Recommendation | `nxa_recommendation` | Multiline Text | Yes |
| Reason | `nxa_reason` | Multiline Text | Yes |
| Supporting Evidence | `nxa_supportingevidence` | Multiline Text | No |
| Confidence | `nxa_confidence` | Decimal | No |
| Status | `nxa_status` | Choice | Yes |
| Generated By | `nxa_generatedby` | Text | No |
| Generated On | `nxa_generatedon` | Date/Time | Yes |
| Human Approved | `nxa_humanapproved` | Yes/No | Yes |
| Approval Date | `nxa_approvaldate` | Date/Time | No |

### Recommendation Status

Initial choices:

```text
Draft
Pending Approval
Approved
Rejected
Completed
Cancelled
```

### Design note

The recommendation can be related to different business contexts.

For example:

```text
Account
Contact
nxa_lead
nxa_opportunity
nxa_product
```

Not every lookup needs to be populated.

---

# 13. AI Action Log

**Logical name:** `nxa_aiactionlog`

Purpose:

Records actions proposed or executed by the AI agent.

## Fields

| Field | Logical Name | Type | Required |
|---|---|---|---|
| Action Name | `nxa_name` | Text | Yes |
| Account | `nxa_accountid` | Lookup → Account | No |
| Contact | `nxa_contactid` | Lookup → Contact | No |
| Lead | `nxa_leadid` | Lookup → nxa_lead | No |
| Opportunity | `nxa_opportunityid` | Lookup → nxa_opportunity | No |
| Action Type | `nxa_actiontype` | Choice | Yes |
| Description | `nxa_description` | Multiline Text | Yes |
| Agent | `nxa_agent` | Text | Yes |
| Status | `nxa_status` | Choice | Yes |
| Requires Approval | `nxa_requiresapproval` | Yes/No | Yes |
| Approved By | `nxa_approvedby` | Text | No |
| Approved On | `nxa_approvedon` | Date/Time | No |
| Executed On | `nxa_executedon` | Date/Time | No |
| Result | `nxa_result` | Multiline Text | No |
| Error Details | `nxa_errordetails` | Multiline Text | No |

### Action Status

```text
Proposed
Pending Approval
Approved
Rejected
Executing
Completed
Failed
Cancelled
```

### Action Type

Initial choices can include:

```text
Create Record
Update Record
Send Communication
Create Task
Assign Lead
Update Opportunity
Generate Recommendation
Other
```

---

# 14. Relationship Model

## Customer 360

```text
                         Account
                            |
             +--------------+--------------+
             |              |              |
             v              v              v
         Contacts      nxa_Leads    nxa_Opportunities
             |                             |
             |                             |
             +-------------+---------------+
                           |
                           v
                      nxa_Products
                           |
                           v
                       Activities
```

Activities can be associated with Account, Contact, nxa_lead, nxa_opportunity, nxa_product, and other supported Dataverse records through the standard activity/Regarding relationship.

---

# 15. AI Layer Relationships

```text
Account ───────────────┐
Contact ───────────────┤
nxa_Lead ──────────────┤
nxa_Opportunity ───────┼──> AI Recommendation
nxa_Product ────────────┘

Account ───────────────┐
Contact ───────────────┤
nxa_Lead ──────────────┤
nxa_Opportunity ───────┼──> AI Action Log
                        ┘
```

This allows the AI layer to remain connected to the core data model, whether a given entity is a standard Dataverse table or a NexusAI custom fallback.

---

# 16. Why Lead, Opportunity and Product Are Included

The initial NexusAI scenario should demonstrate more than a simple customer chatbot.

The data model needs to support an end-to-end business flow:

```text
nxa_Lead
  |
  v
Qualification
  |
  v
nxa_Opportunity
  |
  v
nxa_Product
  |
  v
Recommendation
  |
  v
Human Approval
  |
  v
Business Action
  |
  v
Audit Log
```

This creates a practical foundation for demonstrating:

- AI-assisted lead qualification
- Customer 360
- Opportunity analysis
- Product recommendations
- RAG-based product knowledge
- Next-best-action reasoning
- Agent tool calling
- Human-in-the-loop approval
- AI auditability

---

# 17. Customer Product Decision

Do **not** create `nxa_customerproduct` initially.

The project should first determine whether the standard Dataverse sales model is sufficient.

Potential standard entities/processes to evaluate include:

- Product
- Opportunity Product
- Quote
- Quote Product
- Order
- Order Product
- Invoice
- Invoice Product
- Other applicable standard customer/product structures

If the agent later requires a persistent "products currently held by this customer" abstraction that cannot be represented cleanly with the standard model, then a custom `nxa_customerproduct` table can be introduced.

---

# 18. Initial Data Flow

```text
                 User / Banker
                       |
                       v
                 NexusAI Agent
                       |
       +---------------+----------------+
       |               |                |
       v               v                v
    Account         Contact         nxa_Lead
       |               |                |
       +---------------+----------------+
                       |
                       v
                 nxa_Opportunity
                       |
                       v
                  nxa_Product
                       |
                       v
                   Activities
                       |
                       v
              Customer 360 Context
                       |
                       v
                     RAG
                       |
                       v
                AI Recommendation
                       |
                       v
                Human Approval
                       |
                       v
                  AI Action
                       |
                       v
                AI Action Log
```

---

# 19. Initial Implementation Checklist

## Existing / Reuse

- [x] Account exists
- [x] Contact exists
- [x] Activity exists
- [ ] Add/confirm Account in NexusAICore solution
- [ ] Add/confirm Contact in NexusAICore solution
- [ ] Add/confirm Activity capabilities in NexusAICore solution

## Standard Tables to Add (if present)

- [ ] Inspect standard relationships
- [ ] Confirm required columns
- [ ] Confirm views/forms required for NexusAI

## Custom Tables

- [ ] Create `nxa_lead` (custom fallback for standard Lead)
- [ ] Create `nxa_opportunity` (custom fallback for standard Opportunity)
- [ ] Create `nxa_product` (custom fallback for standard Product)
- [ ] Create `nxa_airecommendation`
- [ ] Create `nxa_aiactionlog`

## Deferred

- [ ] Do not create `nxa_customerproduct` yet
- [ ] Re-evaluate after Customer 360 and agent implementation
- [ ] Re-evaluate whether standard Lead/Opportunity/Product should replace the `nxa_` custom tables if the Dynamics 365 Sales app is later provisioned in this environment

---

# 20. Definition of Done — Dataverse Foundation

The Dataverse foundation is complete when:

1. Account is available to the NexusAI solution.
2. Contact is available to the NexusAI solution.
3. Activity capabilities are available to the NexusAI solution.
4. `nxa_lead` is created and available to the NexusAI solution.
5. `nxa_opportunity` is created and available to the NexusAI solution.
6. `nxa_product` is created and available to the NexusAI solution.
7. Standard relationships required by the initial Customer 360 scenario are available.
8. `nxa_airecommendation` is created.
9. `nxa_aiactionlog` is created.
10. No unnecessary duplicate CRM tables have been created.
11. `nxa_customerproduct` remains deferred.
12. Changes are source controlled through the NexusAI ADO repository.

---

# 21. Design Principle

> **Use the standard Dataverse CRM model as the foundation and add only the AI-specific data structures required to make NexusAI agentic — falling back to a custom table only where a standard table required by the model is not available in the target environment.**

The first implementation goal is not to build the entire CRM.

The goal is to create the minimum enterprise data foundation required for the NexusAI agent to understand:

**who the customer is → what opportunities exist → what products are relevant → what interactions have occurred → what the AI recommends → what action was approved and executed.**