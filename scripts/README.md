# Dataverse Metadata Generator

This folder contains the NexusAI Dataverse metadata deployment script for the approved Dataverse foundation.

The script is intentionally conservative:
- It reuses standard Dataverse tables where possible.
- It does not create duplicate Account/Contact tables.
- It does not create `nxa_customerproduct`.
- It does not recreate metadata that already exists.
- It only creates the custom NexusAI tables approved by the data model.

## Authentication

The script supports two supported authentication paths:

1. App registration / client-credentials flow (recommended for automation)
2. Interactive Azure CLI sign-in for local/manual usage

For automation, set these environment variables before running the script:

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
az account set --subscription "<your subscription>"
python .\scripts\create_dataverse_model.py --org-url "https://nexusai.crm12.dynamics.com"
```

The script also checks the active `pac auth list` environment to confirm that the Dataverse URL matches the configured environment before it makes any metadata changes.

## Prerequisites

Before running the script, ensure:

1. A Microsoft Dataverse app registration exists for the environment.
2. The app registration has sufficient Dataverse permissions to create tables, columns, and solution components.
3. The app registration is added to the target environment with the appropriate security roles.
4. The Power Platform CLI (`pac`) is installed if you want to validate the active environment.
5. Python 3.9+ is available.
6. The required Python package is installed:

```bash
pip install requests msal
```

## What it creates

The script checks for the following tables and creates only the missing metadata:

- `account`
- `contact`
- `activitypointer`
- `lead`
- `opportunity`
- `product`
- `nxa_airecommendation`
- `nxa_aiactionlog`

It does not recreate standard Dataverse tables that already exist. It also does not create `nxa_customerproduct`.

The script creates the custom tables:

- `nxa_airecommendation`
- `nxa_aiactionlog`

and the required fields, option sets, and lookup relationships defined in `DATA_MODEL.md`.

## How to run the script

From the repository root:

```bash
python .\scripts\create_dataverse_model.py
```

Optional arguments:

```bash
python .\scripts\create_dataverse_model.py --org-url "https://nexusai.crm12.dynamics.com" --tenant-id "<tenant-guid>" --client-id "<client-id>" --client-secret "<client-secret>"
```

If no values are passed, the script uses the environment variables or the currently active `pac` environment.

## Idempotency behavior

The script is designed to be safe to run multiple times:

- It checks whether each custom table already exists before creating it.
- It checks whether each field already exists before creating it.
- It checks whether each component is already part of the solution before adding it again.
- It only adds missing metadata.

This prevents duplicates and repeated metadata churn.

## Validation

After the script runs, validate the result in Dataverse or with PAC tooling:

1. Confirm the active environment is the intended NexusAI Dataverse org.
2. Confirm the solution exists and is resolved to its actual unique name.
3. Open the solution and verify the standard OOB tables are present and reused.
4. Verify the custom tables exist:
   - `nxa_airecommendation`
   - `nxa_aiactionlog`
5. Verify all columns, choice values, and lookup relationships are present.
6. Confirm `nxa_customerproduct` was not created.
7. Publish the customizations after creation.

Example PAC validation approach:

```bash
pac auth list
pac solution list
```

Then inspect the solution in the Dataverse environment and verify the metadata visually.

## Notes

- This script does not commit or push repository changes automatically.
- It is intentionally scoped to the NexusAI data foundation and does not create unrelated customizations.
- If the active environment does not match the target org, the script exits before making metadata changes.
