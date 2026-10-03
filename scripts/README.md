# Dataverse Metadata and Sample Data Scripts

This folder contains two scripts for the NexusAI Dataverse foundation:

1. **`create_dataverse_model.py`** — creates the Dataverse tables, columns, choices, and lookup relationships approved in `docs/NexusAI_Data_Model.md`, and associates them with the NexusAICore solution.
2. **`generate_sample_data.py`** — populates all tables with realistic, entirely synthetic banking data for development and demos.

Both scripts are intentionally conservative:
- They reuse standard Dataverse tables where possible (Account, Contact — confirmed present in this environment).
- They do not create duplicate Account/Contact tables.
- They do not recreate metadata or records that already exist where avoidable (see Idempotency below).
- `create_dataverse_model.py` only creates the custom NexusAI tables approved by the data model.

## What's created

Inspection of this Dataverse environment confirmed that **Lead**, **Opportunity**, and **Product** are not provisioned (the Dynamics 365 Sales app/tables are absent). Rather than wait on platform-level provisioning, the approved data model uses custom `nxa_` equivalents for these, plus a dedicated product-holdings table that resolves the originally-deferred "Customer Product" question:

- `nxa_lead` (custom fallback for standard Lead)
- `nxa_opportunity` (custom fallback for standard Opportunity)
- `nxa_product` (custom fallback for standard Product)
- `nxa_productholding` (products currently/previously held by an Account or Contact)
- `nxa_airecommendation` (AI Recommendation)
- `nxa_aiactionlog` (AI Action Log)

`account` and `contact` remain the standard OOB tables and are reused as-is; `activitypointer` (Activities) is inspected but not created or modified by these scripts.

## Authentication

Both scripts support two authentication paths:

1. App registration / client-credentials flow (recommended for automation)
2. Interactive Azure CLI sign-in for local/manual usage

For automation, set these environment variables before running either script:

```bash
export DATAVERSE_TENANT_ID="<tenant-guid>"
export DATAVERSE_CLIENT_ID="<application-client-id>"
export DATAVERSE_CLIENT_SECRET="<application-client-secret>"
export DATAVERSE_URL="https://nexusai.crm12.dynamics.com"
```

You can also use the Azure-style variable names:

```bash
export AZURE_TENANT_ID="<tenant-guid>"
export AZURE_CLIENT_ID="<application-client-id>"
export AZURE_CLIENT_SECRET="<application-client-secret>"
export DYNAMICS_URL="https://nexusai.crm12.dynamics.com"
```

If you do not have a client ID/secret, you can authenticate interactively:

```bash
az login
python .\scripts\create_dataverse_model.py --org-url "https://nexusai.crm12.dynamics.com"
```

Both scripts also check the active `pac auth list` environment to confirm that the Dataverse URL matches the configured environment before making any changes.

## Prerequisites

Before running either script, ensure:

1. A Microsoft Dataverse app registration exists for the environment (or you can sign in interactively via Azure CLI).
2. The app registration/user has sufficient Dataverse permissions to create tables, columns, solution components, and records.
3. The Power Platform CLI (`pac`) is installed if you want to validate the active environment (used automatically if present).
4. Python 3.9+ is available.
5. The required Python package is installed:

```bash
pip install requests
```

## How to run `create_dataverse_model.py`

Run this first — it creates the schema that `generate_sample_data.py` depends on.

```bash
python .\scripts\create_dataverse_model.py
```

Optional arguments:

```bash
python .\scripts\create_dataverse_model.py --org-url "https://nexusai.crm12.dynamics.com" --tenant-id "<tenant-guid>" --client-id "<client-id>" --client-secret "<client-secret>"
```

If no values are passed, the script uses the environment variables or the currently active `pac` environment.

### Idempotency behavior

`create_dataverse_model.py` is safe to run multiple times:

- It checks whether each custom table already exists before creating it.
- It checks whether each field already exists before creating it.
- It skips lookup fields whose target table isn't present in this environment (logged, not an error).
- It checks whether each component is already part of the solution before adding it again.
- It only adds missing metadata.

This prevents duplicates and repeated metadata churn.

## How to run `generate_sample_data.py`

Run this **after** `create_dataverse_model.py` has completed successfully — it depends on the tables, fields, and picklist option sets already existing.

```bash
python .\scripts\generate_sample_data.py
```

It reuses the same authentication configuration as `create_dataverse_model.py` (same environment variables, same `az login` fallback), and imports its auth/request plumbing directly — so both scripts must stay in the same folder.

### Record counts

Default dataset size (configurable via `RECORD_COUNTS` at the top of the script):

| Table | Default count |
|---|---|
| Account | 60 |
| Contact | 80 |
| `nxa_product` | 15 |
| `nxa_productholding` | 90 |
| `nxa_lead` | 80 |
| `nxa_opportunity` | 70 |
| `nxa_airecommendation` | 90 |
| `nxa_aiactionlog` | 70 |

Product is kept smaller than the rest since a realistic banking product catalog rarely has 100 distinct products.

### Important: NOT idempotent by default — use `--only` to avoid duplicates

Running `generate_sample_data.py` with no arguments creates a full new batch of records for **every** table. Only do this once per environment (or after a deliberate reset) — running it again will duplicate everything.

To add data for specific tables without duplicating what already exists, use `--only` with a comma-separated list of steps. Any step you don't list is **not created** — instead, the script fetches existing records for that table and uses them to satisfy dependencies:

```bash
# Add only new product holdings, linking to whatever accounts/contacts/products already exist
python .\scripts\generate_sample_data.py --only product_holdings

# Add only new leads and opportunities, reusing existing accounts/contacts
python .\scripts\generate_sample_data.py --only leads,opportunities
```

Valid step names (also the dependency order): `accounts`, `contacts`, `products`, `product_holdings`, `leads`, `opportunities`, `recommendations`, `action_logs`.

If a skipped step has no existing records to fetch (e.g. you run `--only product_holdings` before ever creating any accounts), the script raises a clear error telling you to run that step first.

### How option set (choice) values are resolved

Dataverse assigns picklist option values sequentially (100000000, 200000000, 200000000, ...) in the exact order the options were defined when the field was created. `generate_sample_data.py` computes these offline from `CUSTOM_TABLES` in `create_dataverse_model.py` (the single source of truth for creation order) rather than querying the API for each one — this has been verified against live solution exports (e.g. `nxa_opportunity.nxa_salesstage` → Qualify=100000000, Develop=200000000, Propose=300000000, Close=400000000) and matches exactly.

## Validation

After `create_dataverse_model.py` runs, validate the result in Dataverse or with PAC tooling:

1. Confirm the active environment is the intended NexusAI Dataverse org.
2. Confirm the solution exists and is resolved to its actual unique name.
3. Open the solution and verify `account`/`contact` are present and reused.
4. Verify the custom tables exist:
   - `nxa_lead`
   - `nxa_opportunity`
   - `nxa_product`
   - `nxa_productholding`
   - `nxa_airecommendation`
   - `nxa_aiactionlog`
5. Verify all columns, choice values, and lookup relationships are present.
6. Publish the customizations after creation (the script does this automatically via `PublishAllXml`).

After `generate_sample_data.py` runs, spot-check a few records in each table via the Dataverse UI or a Web API query to confirm lookups resolved correctly.

Example PAC validation approach:

```bash
pac auth list
pac solution list
```

Then inspect the solution in the Dataverse environment and verify the metadata visually.

## Notes

- Neither script commits or pushes repository changes automatically.
- Both are intentionally scoped to the NexusAI data foundation and do not create unrelated customizations.
- If the active environment does not match the target org, `create_dataverse_model.py` exits before making metadata changes (the same safety check is not currently duplicated in `generate_sample_data.py` beyond reusing the same `verify_active_environment` call).