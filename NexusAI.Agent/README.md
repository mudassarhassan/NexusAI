# NexusAI.Agent

The growing "Workstream B" agent app. Everything in the agent-development
roadmap converges into this one project over time — the Dataverse
data-access layer, the LLM connection, and eventually tool-calling,
grounding, and orchestration all live here rather than being split across
separate throwaway exercises.

## Where this sits in the roadmap

| Phase | What it is | Status |
|---|---|---|
| 1 | Basic LLM call (no data) | ✅ Working — proven separately, now merged in as `LlmClientFactory.cs` |
| 2 | Dataverse data-access tool functions | ✅ Working — `GetCustomer360` confirmed end-to-end against the live environment |
| 3 | Grounding — feed `GetCustomer360`'s result to the LLM for a briefing | Next |
| 5 | Tool calling (Semantic Kernel) — the model decides to call the tools itself | Later |
| 4, 6-10 | RAG, MCP, recommendations, human approval, evaluation | Later |

## What's implemented so far

### Dataverse tools (Phase 2 — read-only)

| Tool function (project README Phase 2 naming) | Method | Notes |
|---|---|---|
| `get_account()` | `DataverseTools.GetAccount` | Throws `AccountNotFoundException` if the GUID doesn't resolve |
| `get_contact()` | `DataverseTools.GetContact` | Single contact by ID |
| — (used by GetCustomer360) | `DataverseTools.GetContactsForAccount` | Via `contact.parentcustomerid` |
| — (used by GetCustomer360) | `DataverseTools.GetLeadsForAccount` | Via `nxa_lead.nxa_accountid` |
| `get_account_opportunities()` | `DataverseTools.GetAccountOpportunities` | Via `nxa_opportunity.nxa_accountid` |
| `get_customer_products()` | `DataverseTools.GetCustomerProductHoldings` | "Product holdings" per the data model; accepts an account or contact ID |
| `get_product()` | `DataverseTools.GetProduct` | Single product by ID |
| `get_contact_activities()` (generalized) | `DataverseTools.GetActivitiesForRegardingObject` | Works for any regarding object, most recent 20 |
| `get_customer_360()` | `DataverseTools.GetCustomer360` | Composes all of the above into the structured contract |
| — (name resolution) | `DataverseTools.SearchAccountsByName` | Wildcard `LIKE` match on Account name; returns `(Guid, name)` pairs for disambiguation — nobody knows a GUID off the top of their head |
| — (name resolution) | `DataverseTools.SearchContactsByName` | Same idea for Contacts; matches against firstname, lastname, or fullname |

Strictly read-only — no create/update/delete, no LLM reasoning mixed in at this layer. See the project README Section 6 for why that separation matters.

### LLM connection (Phase 1 — proven working)

`LlmClientFactory.Connect()` returns a connected `ChatClient` against Azure AI Foundry. This is the exact logic that was proven working in the earlier `Customer360` exercise (after working through: GitHub Models being retired, `gpt-4o-mini` being deprecated, and an endpoint-URL mismatch) — now living permanently in this project instead of a throwaway one.

## Authentication — both pieces are credential-free to set up

- **Dataverse**: always interactive browser sign-in (`DataverseConnectionFactory.cs`), using Microsoft's published sample `AppId` with a `http://localhost` loopback redirect URI (MSAL, which `ServiceClient` uses, only supports loopback redirects for interactive sign-in — an earlier version of this code used an old ADAL-era `app://...` custom URI scheme, which fails with `MsalClientException: loopback_redirect_uri` on current MSAL). No app registration, no client ID/secret anywhere in this project.
- **Azure AI Foundry**: reads three environment variables — `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_KEY`, `AZURE_OPENAI_DEPLOYMENT` — set these in your terminal session before running anything that calls `LlmClientFactory.Connect()`.

## Build and run

```bash
cd NexusAI.Agent
dotnet restore
dotnet build
dotnet run -- --account-id <some-account-guid>
dotnet run -- --account-name "Meadowbrook"
```

Omit both flags to be prompted interactively for either a GUID or a name. With `--account-name` (or a typed name at the prompt): one match resolves automatically, several matches get listed for you to pick from, zero matches reports clearly rather than erroring.

(This project was scaffolded via `dotnet new console` rather than hand-built files — `.csproj` and `Program.cs` reflect that starting point, extended with the files below.)

## Project files

- `DataverseConnectionFactory.cs` — interactive Dataverse auth
- `DataverseTools.cs` — the tool functions table above
- `Models.cs` — data records (`AccountInfo`, `ContactInfo`, `Customer360Result`, etc.)
- `OptionSetLabels.cs` — decodes picklist integer values back to labels (see caveat below)
- `LlmClientFactory.cs` — Azure AI Foundry connection
- `Program.cs` — CLI entry point; resolves an account by GUID or name search, runs `GetCustomer360`, prints JSON — Step 3 will extend this to also call the LLM

## Picklist labels

Picklist fields are decoded to their text label (e.g. `"Qualified"` instead of `300000000`) via `OptionSetLabels.cs`, which mirrors the option order in `DataverseModelGenerator/TableDefinitions.cs` **offline, by deliberate duplication** rather than a project reference — the agent and the metadata-admin tool are separate concerns. **If you ever reorder or add options in `TableDefinitions.cs`, update `OptionSetLabels.cs` to match.** Standard Dataverse choice fields (`statecode`, `statuscode`, `industrycode`, `activitytypecode`) are returned as raw integer codes — no local metadata exists for those here.

## Field-name corrections (carried over from the original Python port)

`nxa_lead` uses `nxa_emailaddress`/`nxa_telephone` (not `nxa_email`/`nxa_phone`); neither `nxa_lead` nor `nxa_opportunity` has a `description` field; `nxa_opportunity` uses `nxa_closeprobability`/`nxa_estimatedclosedate` (not `nxa_probability`/`nxa_expectedclosedate`).

## Not yet implemented (later milestones, by design)

RAG, vector search, AI recommendations, tool-calling/orchestration, MCP, human approval, any write operations.

## Confirmed working

`GetCustomer360` has now run successfully end-to-end against the live environment (via `--account-name` search → disambiguation → `GetCustomer360`), returning real JSON. This confirms: interactive Dataverse auth (with the `http://localhost` redirect fix), `QueryExpression` filtering on `parentcustomerid` (polymorphic Customer lookup), the account name search/disambiguation flow, and the overall read path all work correctly.

## Known remaining caveat

Since no sample activities exist in the environment yet (`SampleDataGenerator` doesn't create any), `GetActivitiesForRegardingObject` will currently return `[]` for every account — expected, not a bug. `regardingobjectid` filtering itself hasn't specifically been exercised yet as a result (empty result either way, so this hasn't been distinguished from "filter works but finds nothing" vs. "filter has an issue").