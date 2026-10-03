# NexusAI — Enterprise Relationship Manager Agent

## 1. Overview

**NexusAI** is an enterprise agentic AI project for learning and demonstrating how to design, build, evaluate, secure, and operate AI systems integrated with Microsoft Dataverse.

The solution combines:

- Microsoft Dataverse
- LLMs / Generative AI
- Retrieval-Augmented Generation (RAG)
- AI agents and tool calling
- MCP
- Azure AI Foundry
- Azure AI Search
- Python / FastAPI
- Evaluation, security, monitoring, and human approval

The goal is to build an AI Relationship Manager that can understand customer context, retrieve relevant enterprise knowledge, generate recommendations, and perform approved business actions.

> **Data rule:** Use synthetic/demo data only. Never use real customer or confidential enterprise data.

---

# 2. Source Control and ALM

NexusAI is maintained in **Azure DevOps (ADO)** using Git.

### Repository

```text
Repository: NexusAI
Branch: main
Root Git folder: /dataverse
```

### Dataverse Solution

```text
Solution Display Name: NexusAICore
Publisher: NexusAI
Publisher Prefix: nxa
```

Custom Dataverse components should therefore use the `nxa_` publisher prefix.

Examples:

```text
nxa_lead
nxa_opportunity
nxa_product
nxa_productholding
nxa_airecommendation
nxa_aiactionlog
```

### Repository structure

The repository is intended to contain both Dataverse solution artifacts and AI application source code:

```text
NexusAI/
│
├── README.md
│
├── dataverse/
│   └── NexusAI solution artifacts
│
├── scripts/
│   ├── create_dataverse_model.py
│   └── generate_sample_data.py
│
├── src/
│   ├── agent/
│   ├── tools/
│   ├── rag/
│   └── api/
│
├── docs/
│   └── NexusAI_Data_Model.md
│
├── tests/
│
└── infrastructure/
```

The `/dataverse` folder is the root folder for Dataverse Git integration.

---

# 3. Core Architecture Principle

## Reuse Dataverse First — With a Documented Fallback

NexusAI must **reuse existing out-of-the-box Dataverse tables and capabilities wherever possible**.

Before creating a new table, inspect the existing Dataverse environment and determine whether an OOB table, column, relationship, activity, or existing component can satisfy the requirement.

**Finding for this environment:** Inspection of the target Dataverse environment (`nexusai.crm12.dynamics.com`) confirmed that the standard **Lead**, **Opportunity**, and **Product** tables are **not present** (the Dynamics 365 Sales app/tables are not provisioned here). Rather than block on platform-level provisioning, NexusAI models these as custom tables with the `nxa_` prefix:

- `nxa_lead` (fallback for standard Lead)
- `nxa_opportunity` (fallback for standard Opportunity)
- `nxa_product` (fallback for standard Product)

**Account**, **Contact**, and **Activities** remain standard OOB tables and are reused as-is — these were confirmed present in the environment.

Create custom Dataverse components only where there is a genuine NexusAI-specific requirement, or where a required standard table is confirmed absent from the environment (as above). See `docs/NexusAI_Data_Model.md` for the full rationale and field-level detail.

---

# 4. Project Workstreams

NexusAI has **two connected but separately implemented workstreams**.

## Workstream A — Dataverse Solution

Inspect the existing Dataverse environment first.

Reuse standard Dataverse tables (Account, Contact, Activities) and extend them only where necessary.

Create NexusAI-specific custom components, and custom fallbacks for standard tables confirmed absent from this environment (Lead, Opportunity, Product), where required.

## Workstream B — AI Agent

Build the AI agent that consumes Dataverse data and progressively adds:

- LLM reasoning
- Customer 360
- RAG
- Tool calling
- MCP
- Recommendations
- Human approval
- Action execution
- Evaluation
- Security
- Monitoring

Do not attempt to implement both workstreams at once. Follow the phases below.

---

# 5. Target Architecture

```text
                         USER / BANKER
                              |
                              v
                    +-------------------+
                    |    NexusAI Agent  |
                    +---------+---------+
                              |
             +----------------+----------------+
             |                |                |
             v                v                v
       Dataverse Tools    RAG / Search    Product Tools
             |                |                |
             v                v                v
       Existing OOB      Azure AI Search   APIs / MCP
       + Custom Data
             |
     +-------+--------+
     |       |        |
 Account  Contact  nxa_Opportunity
     |                  |
 Activities        nxa_Lead
     |                  |
     +------------------+
            |
            v
      nxa_Product
            |
            v
   nxa_Product Holding
     (current holdings)
            |
            v
      NexusAI Custom Data
      - AI Recommendation
      - AI Action Log
            |
            v
      Human Approval
            |
            v
         Execute Action
```

---

# 6. Dataverse Solution

## 6.1 Solution Configuration

Create/use the Dataverse solution:

**Display Name:** NexusAICore  
**Unique Name:** NexusAICore  
**Publisher:** NexusAI  
**Publisher Prefix:** nxa

All NexusAI customizations should be contained within this solution.

Do not modify unrelated solutions or components.

---

# 7. Dataverse Components

The implementation inspected the environment before creating components (see Section 3).

## 7.1 Account

**Uses the existing OOB Account table.** Confirmed present; reused as-is.

Do not create a custom Customer or Organization table to replace Account.

Potentially useful standard fields include:

- Account Name
- Account Number
- Primary Contact
- Email
- Phone
- Address
- Industry
- Status
- Owner

Only add custom columns if NexusAI has a clearly documented requirement.

---

## 7.2 Contact

**Uses the existing OOB Contact table.** Confirmed present; reused as-is.

Do not create a custom Customer table to replace Contact.

Potentially useful standard fields include:

- First Name
- Last Name
- Full Name
- Email
- Mobile Phone
- Parent Customer
- Job Title
- Status

Only add custom columns where required.

---

## 7.3 Lead (custom fallback: `nxa_lead`)

**Standard Lead is not present in this environment.** NexusAI uses a custom `nxa_lead` table instead of waiting on platform-level provisioning (installing the Dynamics 365 Sales app). See `docs/NexusAI_Data_Model.md` Section 7 for the full field list.

If the Dynamics 365 Sales app is provisioned in this environment in future, re-evaluate whether to migrate to the standard Lead table.

---

## 7.4 Opportunity (custom fallback: `nxa_opportunity`)

**Standard Opportunity is not present in this environment.** NexusAI uses a custom `nxa_opportunity` table instead. See `docs/NexusAI_Data_Model.md` Section 8.

Preserves the Lead → Opportunity relationship via a lookup to `nxa_lead`.

---

## 7.5 Product (custom fallback: `nxa_product`)

**Standard Product is not present in this environment.** NexusAI uses a custom `nxa_product` table instead. See `docs/NexusAI_Data_Model.md` Section 9.

Includes a self-referencing Parent Product lookup for product bundling/hierarchy.

---

## 7.6 Activities

Inspect and reuse existing Dataverse activity capabilities.

Potential sources include:

- Task
- Phone Call
- Appointment
- Email
- Other applicable activities

Do not create a generic Interaction table if existing activities can represent the required information.

If a custom interaction abstraction is required, document the reason before creating it.

---

# 8. NexusAI Custom Dataverse Components

## 8.1 AI Recommendation

**Logical name:** `nxa_airecommendation`

Purpose:

Stores recommendations generated by NexusAI.

Suggested columns:

| Column | Type |
|---|---|
| Recommendation Name | Text |
| Account | Lookup → Account |
| Contact | Lookup → Contact |
| Lead | Lookup → nxa_lead |
| Opportunity | Lookup → nxa_opportunity |
| Product | Lookup → nxa_product |
| Recommendation | Multiline Text |
| Reason | Multiline Text |
| Supporting Evidence | Multiline Text |
| Confidence | Decimal |
| Status | Choice |
| Generated By | Text |
| Generated On | Date/Time |
| Human Approved | Yes/No |
| Approval Date | Date/Time |

The Account/Contact relationships use the existing OOB tables; Lead/Opportunity/Product use the NexusAI custom fallback tables described in Section 7.

---

## 8.2 AI Action Log

**Logical name:** `nxa_aiactionlog`

Purpose:

Provides an audit trail of actions proposed or executed by the AI agent.

Suggested columns:

| Column | Type |
|---|---|
| Action Name | Text |
| Account | Lookup → Account |
| Contact | Lookup → Contact |
| Lead | Lookup → nxa_lead |
| Opportunity | Lookup → nxa_opportunity |
| Action Type | Choice |
| Description | Multiline Text |
| Agent | Text |
| Status | Choice |
| Requires Approval | Yes/No |
| Approved By | Text |
| Approved On | Date/Time |
| Executed On | Date/Time |
| Result | Multiline Text |
| Error Details | Multiline Text |

---

## 8.3 Product Holding

**Logical name:** `nxa_productholding`

This resolves the originally-deferred "Customer Product" question (previously tracked under the working name `nxa_customerproduct`). After confirming Product itself required a custom fallback (`nxa_product`), the equivalent standard sales-process structures (Quote/Order/Invoice) were not available either, so a dedicated holdings table was introduced.

Purpose:

Represents a product currently or previously held by an Account or Contact — independent of the sales pipeline (Lead → Opportunity) that may have produced it.

Suggested columns:

| Column | Type |
|---|---|
| Holding Name | Text |
| Account | Lookup → Account |
| Contact | Lookup → Contact |
| Product | Lookup → nxa_product |
| Holding Number | Text |
| Start Date | Date |
| End Date | Date |
| Balance | Decimal |
| Interest Rate | Decimal |
| Status | Choice |

A holding belongs to either an Account or a Contact (not necessarily both) — not every lookup needs to be populated. See `docs/NexusAI_Data_Model.md` Section 17 for full detail.

---

# 9. Dataverse Relationship Model

```text
Account
   |
   +----< Contacts
   |
   +----< nxa_Opportunities
   |
   +----< Activities
   |
   +----< AI Recommendations
   |
   +----< AI Action Logs
   |
   +----< nxa_Product Holdings
              |
              +----> nxa_Product

Contact
   |
   +----< Activities
   |
   +----< nxa_Opportunities
   |
   +----< AI Recommendations
   |
   +----< AI Action Logs
   |
   +----< nxa_Product Holdings

nxa_Lead
   |
   +----< nxa_Opportunities (originating lead)
   |
   +----< AI Recommendations
   |
   +----< AI Action Logs
```

---

# 10. Synthetic Data

Use realistic but completely synthetic data.

Current generator (`scripts/generate_sample_data.py`) default dataset:

- 60 Accounts
- 80 Contacts
- 15 Products (`nxa_product`)
- 90 Product Holdings (`nxa_productholding`)
- 80 Leads (`nxa_lead`)
- 70 Opportunities (`nxa_opportunity`)
- 90 AI Recommendations (`nxa_airecommendation`)
- 70 AI Action Logs (`nxa_aiactionlog`)
- Activities (future — not yet generated by the script)

Counts are configurable via `RECORD_COUNTS` in the script.

Do not use:

- Real customer names
- Real account numbers
- Real financial information
- Confidential enterprise information
- Production customer data

---

# 11. Workstream B — AI Agent

The agent must be developed incrementally.

## Phase 1 — Basic LLM

Build a minimal Python application capable of:

- Sending a user request to an LLM
- Receiving a response
- Returning structured output
- Handling errors

No RAG or complex orchestration yet.

---

# 12. Phase 2 — Dataverse Integration

Create tools for:

```text
get_account()
get_contact()
get_customer_360()
get_account_opportunities()
get_contact_activities()
get_customer_product_holdings()
get_product()
```

The exact tool set should be refined after inspecting the existing Dataverse schema. Note the custom table names (`nxa_lead`, `nxa_opportunity`, `nxa_product`, `nxa_productholding`) when implementing these tools in this environment.

The agent should retrieve information from Dataverse rather than inventing it.

---

# 13. Phase 3 — Customer 360

Create a Customer 360 capability.

Example:

```text
User:
"Prepare me for my meeting with Sarah."

Agent:
1. Identify Sarah
2. Retrieve Account/Contact information
3. Retrieve product holdings
4. Retrieve recent activities
5. Retrieve opportunities
6. Summarize customer context
7. Identify relevant discussion points
```

The output should clearly distinguish:

- Retrieved facts
- AI-generated interpretation
- Recommendations

---

# 14. Phase 4 — RAG

Add an enterprise knowledge base.

Potential synthetic documents:

- Product descriptions
- Product eligibility rules
- Service procedures
- Lending policies
- Customer service procedures
- Internal knowledge articles

Target architecture:

```text
Documents
   |
   v
Ingestion
   |
   v
Chunking
   |
   v
Embeddings
   |
   v
Azure AI Search
   |
   v
Retrieval
   |
   v
Reranking
   |
   v
LLM
   |
   v
Answer + Citations
```

The agent must cite retrieved knowledge where appropriate.

---

# 15. Phase 5 — Agent Tools

The agent should decide when to use tools.

Example:

```text
User Question
     |
     v
   Agent
     |
     +--> Dataverse Tool
     |
     +--> Customer 360 Tool
     |
     +--> RAG Tool
     |
     +--> Product Tool
     |
     +--> Calculator/API Tool
```

The agent should not call unnecessary tools.

---

# 16. Phase 6 — MCP

Introduce MCP after the basic tool architecture is working.

Potential MCP capabilities:

- Customer data
- Product information
- Knowledge search
- External APIs
- Business actions

Do not introduce MCP before the underlying tool/function concepts are understood.

---

# 17. Phase 7 — Recommendations

The agent should be able to generate a recommendation.

Example:

```text
User:
"What products should I discuss with this customer?"
```

The agent should:

1. Retrieve customer context.
2. Retrieve existing product holdings.
3. Retrieve relevant knowledge.
4. Evaluate available products.
5. Generate a recommendation.
6. Explain the recommendation using available evidence.
7. Store the recommendation in Dataverse.

The recommendation must not be presented as an authoritative financial decision.

---

# 18. Phase 8 — Human Approval

Consequential actions require human approval.

```text
Agent
  |
  v
Proposed Action
  |
  v
Human Approval
  |
  +---- Reject
  |
  +---- Approve
            |
            v
       Execute Action
            |
            v
       Write Audit Log
```

The agent must not silently execute consequential actions.

---

# 19. Phase 9 — Evaluation

Create a test dataset containing representative questions.

Measure:

- Answer correctness
- Retrieval relevance
- Groundedness
- Citation quality
- Hallucination rate
- Tool selection
- Tool execution success
- Response latency
- Cost

Create regression tests so changes to prompts, models, or retrieval do not silently reduce quality.

---

# 20. Phase 10 — Security and Production

Implement:

- Microsoft Entra ID
- Authentication
- Authorization
- Least-privilege access
- Secrets management
- Prompt-injection defenses
- Input validation
- Output validation
- Audit logging
- Monitoring
- Tracing
- Error handling
- Cost controls
- CI/CD

---

# 21. Suggested Technology Stack

### AI

- LLM
- Generative AI
- RAG
- Embeddings
- Tool calling
- Agents
- MCP
- Evaluation

### Microsoft

- Microsoft Dataverse
- Azure AI Foundry
- Azure AI Search
- Microsoft Entra ID
- Azure Functions where appropriate

### Development

- Python
- FastAPI
- Git
- Azure DevOps
- Docker
- Automated tests
- CI/CD

---

# 22. Implementation Rules for Agentic VS Code

When modifying the project:

1. **Read this README before implementing a new feature.**
2. Inspect the existing Dataverse environment before creating tables or components.
3. **Reuse OOB Dataverse tables first** (Account, Contact, Activities — confirmed present in this environment).
4. For Lead, Opportunity, and Product — confirmed **absent** from this environment — use the existing custom fallbacks (`nxa_lead`, `nxa_opportunity`, `nxa_product`) rather than recreating the inspection/decision; do not create additional duplicate custom tables for these.
5. Keep NexusAI customizations inside the NexusAI solution.
6. Use the `nxa` publisher prefix for custom Dataverse components.
7. Use synthetic data only.
8. Do not invent Dataverse schema outside `docs/NexusAI_Data_Model.md` without documenting the change there first.
9. Prefer small, testable changes.
10. Add tests for important functionality.
11. Update documentation when architecture changes.
12. Mark completed work in the implementation checklist.
13. Never expose secrets, credentials, or connection strings in source control.
14. Do not execute consequential business actions without explicit human approval.
15. Do not implement future phases until the current phase is working and tested.
16. Before changing the data model, explain the reason and identify whether an OOB component can satisfy the requirement.
17. Keep Dataverse solution artifacts under `/dataverse`.
18. Use Git commits with clear messages describing the change.
19. Do not overwrite unrelated changes in the repository.
20. Treat `main` as the stable branch. Prefer feature branches for substantial changes once the basic repository setup is complete.

---

# 23. Implementation Checklist

## Dataverse Foundation

- [ ] Connect Dev Dataverse environment to ADO Git repository
- [ ] Confirm repository is `NexusAI`
- [ ] Confirm branch is `main`
- [ ] Confirm Dataverse root Git folder is `/dataverse`
- [ ] Confirm NexusAI solution
- [ ] Confirm publisher prefix is `nxa`
- [x] Inspect existing Dataverse tables and solutions
- [x] Confirm Account OOB table can be reused
- [x] Confirm Contact OOB table can be reused
- [x] Confirm Lead/Opportunity/Product are NOT present in this environment → use custom fallbacks
- [x] Inspect existing Activities
- [x] Create `nxa_lead` (custom fallback)
- [x] Create `nxa_opportunity` (custom fallback)
- [x] Create `nxa_product` (custom fallback)
- [x] Create `nxa_productholding` (resolves deferred Customer Product question)
- [x] Create AI Recommendation table (`nxa_airecommendation`)
- [x] Create AI Action Log table (`nxa_aiactionlog`)
- [x] Configure required relationships (lookups)
- [x] Configure choices (picklists)
- [ ] Configure views/forms
- [ ] Configure security
- [x] Create synthetic test data (`scripts/generate_sample_data.py`)

## AI Agent

- [ ] Create Python project
- [ ] Connect to LLM
- [ ] Implement structured outputs
- [ ] Implement Dataverse authentication
- [ ] Implement Account tool
- [ ] Implement Contact tool
- [ ] Implement Customer 360
- [ ] Implement RAG
- [ ] Implement citations
- [ ] Implement Product tool
- [ ] Implement agent tool selection
- [ ] Implement MCP
- [ ] Implement recommendations
- [ ] Store recommendations in Dataverse
- [ ] Implement human approval
- [ ] Implement approved actions
- [ ] Implement AI Action Log

## Production AI

- [ ] Evaluation dataset
- [ ] Automated evaluation
- [ ] Regression testing
- [ ] Tracing
- [ ] Monitoring
- [ ] Security controls
- [ ] Prompt-injection defenses
- [ ] Cost monitoring
- [ ] Docker
- [ ] CI/CD
- [ ] Deployment documentation

---

# 24. Definition of Done

NexusAI is considered complete when a user can ask:

> "Prepare me for my meeting with Sarah and tell me which relevant products I should discuss."

The system should:

1. Identify Sarah using the Account/Contact model (OOB) combined with the NexusAI Lead/Opportunity model (custom fallback, this environment).
2. Retrieve relevant customer information from Dataverse.
3. Retrieve product holdings, activities, and opportunities using the implemented Dataverse tables.
4. Search relevant enterprise knowledge using RAG.
5. Generate a grounded meeting briefing.
6. Provide supporting citations.
7. Generate clearly identified recommendations.
8. Store the recommendation in the custom AI Recommendation table.
9. Propose an action when appropriate.
10. Request human approval before executing the action.
11. Execute the approved action.
12. Record the action in the custom AI Action Log.
13. Provide telemetry and evaluation data to assess system quality.
14. Keep all source-controlled Dataverse artifacts under `/dataverse`.

---

# 25. Project Principle

> **Reuse before reinventing. Build one capability at a time. Understand why it works. Test it. Document it. Then move to the next capability.**

NexusAI is not intended to be a simple chatbot. It is a practical learning project for understanding how to design, build, evaluate, secure, and operate **enterprise agentic AI systems integrated with Microsoft Dataverse**.