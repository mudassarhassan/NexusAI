# DataverseModelGenerator (.NET 10)

A .NET 10 port of `create_dataverse_model.py` / `generate_sample_data.py`,
using the official `Microsoft.PowerPlatform.Dataverse.Client` SDK instead of
raw Web API calls. Same data model, same idempotency guarantees, same
synthetic dataset — just strongly typed instead of hand-built JSON payloads,
and now combined into one tool with an interactive menu instead of two
separate scripts.

Two sibling projects build on this one:
- **`NexusAI.Agent`** — the read-only `GetCustomer360` data-access layer
- **`NexusAI.LlmBasics`** — the Phase 1 "minimal LLM call" learning exercise

Neither of those needs anything from this project at build time, but both
assume the tables and sample data this project creates already exist in the
target environment.

## What it does

On startup it connects, then either asks interactively or does what you told
it to via `--action`:

```
What would you like to do?
  1) Create/update Dataverse metadata (tables, fields, solution)
  2) Generate sample data
  3) Both
```

### 1) Create/update metadata

Creates, in dependency order: `nxa_lead`, `nxa_opportunity`, `nxa_product`,
`nxa_productholding`, `nxa_airecommendation`, `nxa_aiactionlog`. Reuses
`account` / `contact` (standard tables) as lookup targets when present, and
cleanly skips (logging why) any lookup whose target table isn't in the
environment. Associates every custom table with the `NexusAICore` solution
and publishes all customizations at the end. Safe to run repeatedly — checks
for existing tables/fields/solution components before creating them.

### 2) Generate sample data

Populates all tables with synthetic banking data (same pools/templates and
default counts as the Python `generate_sample_data.py`: 60 accounts, 80
contacts, 15 products, 90 product holdings, 80 leads, 70 opportunities, 90
recommendations, 70 action logs). You'll be asked which of the 8 steps to
run — press Enter for all, or pick a subset (e.g. just `product_holdings`)
to add data to one table while **reusing** (not duplicating) whatever
already exists in the tables you skip.

**Not idempotent** if you select "all" twice — running the full set again
creates a second batch. Use step selection to add data incrementally instead.

## Authentication

Always interactive browser sign-in, using Microsoft's published sample
application ID — no app registration, no client secret, no credentials of
any kind to manage. A browser window opens the first time you run it (or
whenever the cached token expires).

This intentionally does **not** replicate the Python script's `pac auth
list` safety check (cross-verifying the active CLI environment matches the
target org) — that was specific to the Azure CLI subprocess workflow this
version doesn't use. Just double check `--org-url` / `DATAVERSE_URL` points
at the right environment before running.

## Build and run

```bash
cd DataverseModelGenerator
dotnet restore
dotnet build
dotnet run
```

Optional arguments, to skip the interactive prompts (e.g. for automation):

```bash
dotnet run -- --action create-metadata
dotnet run -- --action generate-sample-data --only product_holdings
dotnet run -- --action generate-sample-data --only leads,opportunities
dotnet run -- --action both
dotnet run -- --org-url "https://nexusai.crm12.dynamics.com"
dotnet run -- --skip-solution-association
dotnet run -- --solution-name "SomeOtherSolution"
```

Valid `--only` step names (also the dependency order): `accounts`,
`contacts`, `products`, `product_holdings`, `leads`, `opportunities`,
`recommendations`, `action_logs`.

## Project layout

- `TableDefinitions.cs` — the data model (`FieldDefinition`, `TableDefinition`,
  `DataModel.CustomTables`), the single source of truth for table/field/picklist
  definitions; also reused by `SampleDataGenerator.cs` to derive picklist
  values offline, and independently duplicated (by necessity — see its own
  README) in `NexusAI.Agent/OptionSetLabels.cs`.
- `DataverseMetadataService.cs` — metadata operations: table/field existence
  checks, creation, solution association, publish.
- `SampleDataPools.cs` — name/company/template pools and default record
  counts for synthetic data generation (exact port of the Python version's
  pools, including its intentional duplicate entries).
- `SampleDataGenerator.cs` — the actual record-creation logic, using the
  SDK's `Entity`/`EntityReference`/`OptionSetValue` types directly (no entity-set-name
  resolution needed, unlike the raw-Web-API Python version).
- `Program.cs` — argument parsing, the interactive action/step menus,
  authentication, orchestration.

## Build issues found and fixed so far

Kept here as a reference, since the same classes of mistake are worth
knowing about if you extend this further:

- `MemoAttributeMetadata` has **no** `Format` property (there's no
  `MemoFormat` enum) — only `MaxLength` matters for a multiline text field.
  Fixed by removing the bogus assignment.
- An XML comment in the `.csproj` contained a literal `--` inside the
  comment body (`--outdated`), which is invalid XML (`--` is only legal as
  the comment's closing delimiter) and broke the whole project file parse.
  Fixed by rewording the comment.
- Interactive sign-in failed with `MsalClientException: ErrorCode:
  loopback_redirect_uri` — the original `RedirectUri` used an old ADAL-era
  custom URI scheme (`app://58145b91-...`), but MSAL (which `ServiceClient`
  uses) only supports loopback redirects (`http://localhost`) for
  interactive sign-in. Fixed by changing `InteractiveSampleRedirectUri` to
  `"http://localhost"`, per Microsoft's current OAuth-with-Dataverse docs.

## What hasn't been verified yet

Three spots flagged from the start that haven't been confirmed either way
yet (no build output involving them has come back):

1. **`BooleanOptionSetMetadata` constructor** (`DataverseMetadataService.cs`,
   `BuildAttributeMetadata`, `FieldType.Boolean` case) — currently
   `new BooleanOptionSetMetadata(trueOption, falseOption)`. If that
   constructor overload doesn't exist in the installed SDK version, switch to:
   ```csharp
   OptionSet = new BooleanOptionSetMetadata
   {
       TrueOption = new OptionMetadata(Lbl("Yes"), 1),
       FalseOption = new OptionMetadata(Lbl("No"), 0),
   }
   ```
2. **`AddSolutionComponentRequest.ComponentType`** — set to the literal `1`
   (Entity). If the compiler wants an explicit enum or cast instead of a bare
   int, the value itself (1 = Entity) is still correct.
3. **`StringFormatName`** (`StringFormatName.Email` / `.Phone` / `.Text`) —
   a sealed class with static instances in the SDK version I'm recalling,
   not a plain enum. A compile error here is almost certainly a
   namespace/version mismatch, not a wrong concept.

Paste whatever the compiler says for any of these (or anything else) and
we'll fix it the same way we've fixed everything else in this project.