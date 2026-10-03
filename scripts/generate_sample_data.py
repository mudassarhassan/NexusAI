#!/usr/bin/env python3
"""Generate synthetic sample data at scale for the NexusAI Dataverse model.

Populates, in dependency order:
  1. account             (standard table, reused)
  2. contact              (standard table, reused)
  3. nxa_product          (custom)
  4. nxa_productholding   (custom -- products currently held by an account/contact)
  5. nxa_lead             (custom)
  6. nxa_opportunity      (custom)
  7. nxa_airecommendation (custom)
  8. nxa_aiactionlog      (custom)

All data is synthetic, per the NexusAI data model principle "Use synthetic
data only." Running this script with no arguments creates a NEW batch of
records for EVERY table (it is not idempotent) -- only do this once per
environment, or you'll get duplicates.

To add data for just one or two tables without duplicating what's already
there, use --only with a comma-separated list of steps. Steps not listed are
NOT created -- instead, existing records for that table are fetched and used
to satisfy any dependency (e.g. product_holdings needs existing accounts,
contacts and products to link to).

Examples:
    python generate_sample_data.py
        # creates everything (first run on a fresh environment)

    python generate_sample_data.py --only product_holdings
        # creates ONLY new nxa_productholding records, linking to whatever
        # accounts/contacts/products already exist (does not touch any
        # other table, so nothing gets duplicated)

    python generate_sample_data.py --only leads,opportunities
        # creates ONLY new leads and opportunities, reusing existing
        # accounts/contacts to link to

Valid step names (also the dependency order): accounts, contacts, products,
product_holdings, leads, opportunities, recommendations, action_logs.

Record counts are configurable via the RECORD_COUNTS dict below.

Reuses the same auth and Web API plumbing as create_dataverse_model.py.
"""

from __future__ import annotations

import argparse
import datetime
import json
import random
from typing import Any, Dict, List

# The order below also defines dependency order: a step can only use records
# from a step listed before it (e.g. product_holdings needs accounts/contacts/products).
STEP_KEYS = [
    "accounts",
    "contacts",
    "products",
    "product_holdings",
    "leads",
    "opportunities",
    "recommendations",
    "action_logs",
]

from create_dataverse_model import (
    CUSTOM_TABLES,  # noqa: F401 (also re-used for offline picklist value derivation below)
    DataverseAuthError,
    acquire_token,
    call_dataverse,
    get_auth_config,
    log,
    resolve_org_url,
    verify_active_environment,
)

# ---------------------------------------------------------------------------
# Record counts -- tune these as needed. Product is kept smaller than the
# rest since a realistic banking product catalog rarely has 100 distinct
# products; everything else defaults into the 50-100 range you asked for.
# ---------------------------------------------------------------------------
RECORD_COUNTS = {
    "accounts": 60,
    "contacts": 80,
    "products": 15,
    "product_holdings": 90,
    "leads": 80,
    "opportunities": 70,
    "recommendations": 90,
    "action_logs": 70,
}

RANDOM_SEED = 42  # change or remove for different-but-reproducible data

# ---------------------------------------------------------------------------
# Name / content generation pools
# ---------------------------------------------------------------------------
FIRST_NAMES = [
    "Olivia", "Liam", "Ava", "Noah", "Isla", "Jack", "Mia", "Lucas", "Harrison",
    "Grace", "Ethan", "Chloe", "Henry", "Amelia", "Jacob", "Zoe", "Samuel", "Ruby",
    "Charlotte", "William", "Sophie", "James", "Emily", "Oliver", "Ella", "Thomas",
    "Lily", "Benjamin", "Hannah", "Daniel", "Matilda", "Alexander", "Scarlett",
    "Michael", "Audrey", "Joshua", "Georgia", "Nathan", "Harper", "Samuel",
    "Freya", "Cooper", "Willow", "Riley", "Poppy", "Mason", "Evie", "Hugo",
]
LAST_NAMES = [
    "Lee", "O'Connor", "Mitchell", "Baxter", "Walsh", "Foster", "Reid", "Sullivan",
    "Doyle", "Nash", "Nguyen", "Patterson", "Singh", "Williams", "Thompson",
    "Robertson", "Campbell", "Brennan", "Carter", "Bennett", "Hughes", "Murphy",
    "Kelly", "Ryan", "Byrne", "Walker", "Hayes", "Fox", "Dunn", "Shaw", "Barrett",
    "Gallagher", "Quinn", "Price", "Marsh", "Lambert", "Pearce", "Chapman",
]
COMPANY_CORE = [
    "Meadowbrook", "Sunrise", "Harborview", "Blue Gum", "Fernridge", "Lee Family",
    "O'Connor", "Mitchell Household", "Baxter Retail", "Walsh Super Fund",
    "Foster", "Reid Earthmoving", "Sullivan Creative", "Doyle Household",
    "Nash Cafe", "Ironbark", "Silverleaf", "Red Gum", "Northbank", "Southport",
    "Riverside", "Hillcrest", "Coastal", "Highland", "Stonegate", "Wattlewood",
    "Goldfield", "Brightwater", "Eastgate", "Westmead", "Clearview", "Oakridge",
    "Pinehurst", "Marlowe", "Cedarbrook", "Thornbury", "Greystone", "Ashwood",
    "Lakeside", "Summerhill",
]
COMPANY_SUFFIX = [
    "Pty Ltd", "Group", "Holdings Pty Ltd", "Trading Co", "Logistics Pty Ltd",
    "Construction Pty Ltd", "Hospitality Group", "Consulting", "Retail Group",
    "Studio", "Pty Ltd", "& Co", "Super Fund", "Household", "Enterprises",
]
JOB_TITLES = [
    "Finance Manager", "Operations Director", "Owner", "CFO", "Managing Director",
    "Procurement Lead", "General Manager", "Finance Director", "Office Manager",
    "Business Owner", "Head of Operations", "Company Secretary",
]
LEAD_TOPIC_TEMPLATES = [
    "New business banking enquiry",
    "Referral from existing customer",
    "Home loan refinance interest",
    "Business overdraft enquiry",
    "Term deposit rollover enquiry",
    "Credit card upgrade enquiry",
    "Equipment finance enquiry",
    "Savings account for new business",
    "Personal loan enquiry",
    "Merchant facility enquiry",
    "Foreign currency account enquiry",
    "Trade finance enquiry",
    "Business insurance bundling enquiry",
    "Asset finance enquiry",
    "Cash flow lending enquiry",
]
OPPORTUNITY_TEMPLATES = [
    "Home loan refinance",
    "Business overdraft",
    "Term deposit rollover",
    "Equipment finance",
    "Merchant facility",
    "Credit card upgrade",
    "Personal loan",
    "Business banking package",
    "Trade finance facility",
    "Asset finance arrangement",
]
RECOMMENDATION_TEMPLATES = [
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
]
ACTION_TEMPLATES = [
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
]
PRODUCT_DEFS = [
    ("Everyday Savings Account", "Service", 0.0, 0.0),
    ("12-Month Term Deposit", "Service", 0.0, 0.0),
    ("24-Month Term Deposit", "Service", 0.0, 0.0),
    ("Personal Loan", "Service", 0.0, 0.0),
    ("Home Loan - Variable Rate", "Service", 0.0, 0.0),
    ("Home Loan - Fixed Rate", "Service", 0.0, 0.0),
    ("Platinum Business Credit Card", "Service", 0.0, 99.0),
    ("Rewards Business Credit Card", "Service", 0.0, 49.0),
    ("Business Overdraft Facility", "Service", 0.0, 0.0),
    ("Merchant Payment Terminal", "Inventory", 250.0, 0.0),
    ("Trade Finance Facility", "Service", 0.0, 0.0),
    ("Asset Finance Package", "Bundle", 0.0, 0.0),
    ("Foreign Currency Account", "Service", 0.0, 0.0),
    ("Cash Flow Lending Product", "Service", 0.0, 0.0),
    ("Business Insurance Bundle", "Bundle", 0.0, 0.0),
]


def get_entity_set_names(org_url: str, token: str, logical_names: List[str]) -> Dict[str, str]:
    filter_clause = " or ".join(f"LogicalName eq '{name}'" for name in logical_names)
    query = f"/api/data/v9.2/EntityDefinitions?$select=LogicalName,EntitySetName&$filter={filter_clause}"
    result = call_dataverse(org_url, token, query)
    values = result.get("value", []) if isinstance(result, dict) else []
    mapping = {item["LogicalName"]: item["EntitySetName"] for item in values}
    missing = set(logical_names) - set(mapping.keys())
    if missing:
        raise RuntimeError(f"Could not resolve EntitySetName for: {missing}. Has create_dataverse_model.py been run successfully?")
    return mapping


def get_picklist_values(org_url: str, token: str, entity_logical_name: str, attribute_logical_name: str) -> Dict[str, int]:
    """Compute option values offline from CUSTOM_TABLES, rather than querying the API.

    Dataverse assigns picklist option Values sequentially, starting at 100000000 and
    incrementing by 100000000, in the exact order the options were listed when the
    field was created (confirmed against a live solution export: nxa_salesstage ->
    Qualify=100000000, Develop=200000000, Propose=300000000, Close=400000000, which
    matches CUSTOM_TABLES's OptionSet order exactly). Since create_dataverse_model.py
    is the single source of truth for that order, we derive values from it directly
    instead of making an extra metadata round-trip per picklist field.
    """
    table_def = CUSTOM_TABLES.get(entity_logical_name)
    if table_def is None:
        raise RuntimeError(f"'{entity_logical_name}' is not defined in CUSTOM_TABLES; cannot derive option values.")

    field_def = None
    for field in table_def["fields"]:
        if field.get("LogicalName") == attribute_logical_name or field.get("SchemaName") == attribute_logical_name:
            field_def = field
            break
    if field_def is None:
        raise RuntimeError(f"Field '{attribute_logical_name}' not found on '{entity_logical_name}' in CUSTOM_TABLES.")

    option_set = field_def.get("OptionSet", [])
    return {label: (index + 1) * 100000000 for index, label in enumerate(option_set)}


def call_dataverse_with_headers(org_url: str, token: str, relative_url: str, *, method: str = "GET", json_body=None, extra_headers=None) -> Any:
    import requests

    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/json",
        "Content-Type": "application/json; charset=utf-8",
        "OData-Version": "4.0",
        "OData-MaxVersion": "4.0",
    }
    if extra_headers:
        headers.update(extra_headers)

    url = f"{org_url}{relative_url}"
    response = requests.request(method, url, headers=headers, data=(json.dumps(json_body) if json_body is not None else None), timeout=120)

    if response.status_code in (200, 201, 202, 204):
        if response.text.strip():
            return response.json()
        return {}

    raise RuntimeError(f"Dataverse request failed for {method} {relative_url}: {response.status_code} - {response.text[:1000]}")


def get_existing_ids(org_url: str, token: str, entity_set: str, id_field: str, top: int = 5000) -> List[str]:
    """Fetch GUIDs of records that already exist, for steps we're told to skip
    (so dependent steps -- e.g. product_holdings needing accounts -- can still
    link to real records instead of creating new ones)."""
    query = f"/api/data/v9.2/{entity_set}?$select={id_field}&$top={top}"
    result = call_dataverse(org_url, token, query)
    values = result.get("value", []) if isinstance(result, dict) else []
    ids = [v[id_field] for v in values if id_field in v]
    if not ids:
        raise RuntimeError(
            f"No existing records found in '{entity_set}' (field '{id_field}'). "
            f"This step was skipped via --only, but a dependent step needs existing records to link to -- "
            f"run that step at least once first (without --only, or including it in --only)."
        )
    return ids


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate synthetic sample data for the NexusAI Dataverse model.")
    parser.add_argument(
        "--only",
        default=None,
        help=(
            "Comma-separated list of steps to actually CREATE new records for; all other steps "
            "fetch existing records instead of creating duplicates. Steps (in dependency order): "
            + ",".join(STEP_KEYS)
            + ". Example: --only product_holdings"
        ),
    )
    return parser.parse_args()


def create_record(org_url: str, token: str, entity_set: str, data: Dict[str, Any], id_field: str) -> str:
    headers_extra = {"Prefer": "return=representation"}
    result = call_dataverse_with_headers(org_url, token, f"/api/data/v9.2/{entity_set}", method="POST", json_body=data, extra_headers=headers_extra)
    new_id = result.get(id_field)
    if not new_id:
        raise RuntimeError(f"Create succeeded but no '{id_field}' found in response: {result}")
    return new_id


def iso(days_ago: int = 0, days_ahead: int = 0) -> str:
    dt = datetime.datetime.utcnow() + datetime.timedelta(days=days_ahead - days_ago)
    return dt.strftime("%Y-%m-%dT%H:%M:%SZ")


def random_company_name(used: set) -> str:
    while True:
        name = f"{random.choice(COMPANY_CORE)} {random.choice(COMPANY_SUFFIX)}"
        if name not in used:
            used.add(name)
            return name


def random_person(used: set):
    while True:
        first = random.choice(FIRST_NAMES)
        last = random.choice(LAST_NAMES)
        key = (first, last)
        if key not in used:
            used.add(key)
            return first, last


def progress(label: str, i: int, total: int) -> None:
    step = max(total // 10, 1)
    if i == 0 or (i + 1) % step == 0 or i + 1 == total:
        log(f"{label}: {i + 1}/{total}")


def main() -> int:
    random.seed(RANDOM_SEED)

    args = parse_args()
    selected = set(s.strip() for s in args.only.split(",")) if args.only else set(STEP_KEYS)
    unknown = selected - set(STEP_KEYS)
    if unknown:
        raise RuntimeError(f"Unknown --only step(s): {sorted(unknown)}. Valid steps: {STEP_KEYS}")
    if args.only:
        log(f"--only given: will CREATE new records only for {sorted(selected)}; all other steps will reuse existing records.")

    org_url = resolve_org_url().rstrip("/")
    config = get_auth_config()
    token = acquire_token(org_url, config)
    verify_active_environment(org_url, token)
    log(f"Target Dataverse org: {org_url}")

    entity_sets = get_entity_set_names(
        org_url, token,
        ["account", "contact", "nxa_product", "nxa_productholding", "nxa_lead", "nxa_opportunity", "nxa_airecommendation", "nxa_aiactionlog"],
    )
    log(f"Resolved entity sets: {entity_sets}")

    lead_source_values = get_picklist_values(org_url, token, "nxa_lead", "nxa_leadsource")
    lead_rating_values = get_picklist_values(org_url, token, "nxa_lead", "nxa_rating")
    lead_status_values = get_picklist_values(org_url, token, "nxa_lead", "nxa_status")
    opp_salesstage_values = get_picklist_values(org_url, token, "nxa_opportunity", "nxa_salesstage")
    opp_rating_values = get_picklist_values(org_url, token, "nxa_opportunity", "nxa_rating")
    opp_status_values = get_picklist_values(org_url, token, "nxa_opportunity", "nxa_status")
    product_type_values = get_picklist_values(org_url, token, "nxa_product", "nxa_producttype")
    holding_status_values = get_picklist_values(org_url, token, "nxa_productholding", "nxa_status")
    rec_status_values = get_picklist_values(org_url, token, "nxa_airecommendation", "nxa_status")
    log_actiontype_values = get_picklist_values(org_url, token, "nxa_aiactionlog", "nxa_actiontype")
    log_status_values = get_picklist_values(org_url, token, "nxa_aiactionlog", "nxa_status")

    used_company_names: set = set()
    used_person_names: set = set()

    # ---------------------------------------------------------------
    # 1. Accounts
    # ---------------------------------------------------------------
    if "accounts" in selected:
        n = RECORD_COUNTS["accounts"]
        account_ids: List[str] = []
        for i in range(n):
            name = random_company_name(used_company_names)
            data = {
                "name": name,
                "accountnumber": f"ACC-{1000 + i}",
                "emailaddress1": f"contact@{name.split()[0].lower().replace(chr(39), '')}.example.com",
                "telephone1": f"07 3{100 + i % 900:03d} {2000 + i:04d}",
            }
            new_id = create_record(org_url, token, entity_sets["account"], data, "accountid")
            account_ids.append(new_id)
            progress("Accounts created", i, n)
    else:
        account_ids = get_existing_ids(org_url, token, entity_sets["account"], "accountid")
        log(f"Reusing {len(account_ids)} existing accounts (skipped creating new ones).")

    # ---------------------------------------------------------------
    # 2. Contacts
    # ---------------------------------------------------------------
    if "contacts" in selected:
        n = RECORD_COUNTS["contacts"]
        contact_ids: List[str] = []
        for i in range(n):
            first, last = random_person(used_person_names)
            account_id = random.choice(account_ids)
            data = {
                "firstname": first,
                "lastname": last,
                "jobtitle": random.choice(JOB_TITLES),
                "emailaddress1": f"{first.lower()}.{last.lower().replace(chr(39), '')}@example.com",
                "mobilephone": f"04{10 + i % 90:02d} {100 + i % 900:03d} {200 + i % 800:03d}",
                "parentcustomerid_account@odata.bind": f"/{entity_sets['account']}({account_id})",
            }
            new_id = create_record(org_url, token, entity_sets["contact"], data, "contactid")
            contact_ids.append(new_id)
            progress("Contacts created", i, n)
    else:
        contact_ids = get_existing_ids(org_url, token, entity_sets["contact"], "contactid")
        log(f"Reusing {len(contact_ids)} existing contacts (skipped creating new ones).")

    # ---------------------------------------------------------------
    # 3. Products
    # ---------------------------------------------------------------
    if "products" in selected:
        n = min(RECORD_COUNTS["products"], len(PRODUCT_DEFS))
        if RECORD_COUNTS["products"] > len(PRODUCT_DEFS):
            log(f"Requested {RECORD_COUNTS['products']} products but only {len(PRODUCT_DEFS)} distinct product templates are defined; creating {len(PRODUCT_DEFS)}.")
        product_ids: List[str] = []
        for i, (pname, ptype, cost, price) in enumerate(PRODUCT_DEFS[:n]):
            data = {
                "nxa_name": pname,
                "nxa_productnumber": f"PRD-{1000 + i}",
                "nxa_producttype": product_type_values.get(ptype),
                "nxa_currentcost": cost,
                "nxa_price": price,
            }
            new_id = create_record(org_url, token, entity_sets["nxa_product"], data, "nxa_productid")
            product_ids.append(new_id)
            progress("Products created", i, n)
    else:
        product_ids = get_existing_ids(org_url, token, entity_sets["nxa_product"], "nxa_productid")
        log(f"Reusing {len(product_ids)} existing products (skipped creating new ones).")

    # ---------------------------------------------------------------
    # 3b. Product Holdings (which account/contact currently holds which product)
    # ---------------------------------------------------------------
    if "product_holdings" in selected:
        n = RECORD_COUNTS["product_holdings"]
        holding_statuses = list(holding_status_values.keys())
        for i in range(n):
            product_id = random.choice(product_ids)
            # Roughly 60% held by an account (organisation), 40% by an individual contact.
            held_by_account = random.random() < 0.6
            status = random.choice(holding_statuses)
            is_closed = status in ("Closed", "Matured")

            if held_by_account:
                holder_id = random.choice(account_ids)
                holder_binding = {"nxa_accountid@odata.bind": f"/{entity_sets['account']}({holder_id})"}
            else:
                holder_id = random.choice(contact_ids)
                holder_binding = {"nxa_contactid@odata.bind": f"/{entity_sets['contact']}({holder_id})"}

            data = {
                "nxa_name": f"Product Holding {i + 1:04d}",
                "nxa_holdingnumber": f"HLD-{10000 + i}",
                "nxa_startdate": iso(days_ago=random.randint(30, 1500))[:10] + "T00:00:00Z",
                "nxa_balance": round(random.uniform(500, 450000), 2),
                "nxa_interestrate": round(random.uniform(0.5, 9.5), 2),
                "nxa_status": holding_status_values.get(status),
                "nxa_productid@odata.bind": f"/{entity_sets['nxa_product']}({product_id})",
                **holder_binding,
            }
            if is_closed:
                data["nxa_enddate"] = iso(days_ago=random.randint(0, 29))[:10] + "T00:00:00Z"
            new_id = create_record(org_url, token, entity_sets["nxa_productholding"], data, "nxa_productholdingid")
            progress("Product holdings created", i, n)
    else:
        log("Skipping nxa_productholding (not in --only).")

    # ---------------------------------------------------------------
    # 4. Leads
    # ---------------------------------------------------------------
    if "leads" in selected:
        n = RECORD_COUNTS["leads"]
        lead_sources = list(lead_source_values.keys())
        lead_ratings = list(lead_rating_values.keys())
        lead_statuses = list(lead_status_values.keys())
        lead_ids: List[str] = []
        for i in range(n):
            first, last = random.choice(FIRST_NAMES), random.choice(LAST_NAMES)
            company = random_company_name(used_company_names)
            topic = f"{random.choice(LEAD_TOPIC_TEMPLATES)} - {company}"
            data = {
                "nxa_name": topic,
                "nxa_firstname": first,
                "nxa_lastname": last,
                "nxa_companyname": company,
                "nxa_emailaddress": f"{first.lower()}.{last.lower().replace(chr(39), '')}@example.com",
                "nxa_telephone": f"04{20 + i % 80:02d} {300 + i % 700:03d} {400 + i % 600:03d}",
                "nxa_leadsource": lead_source_values.get(random.choice(lead_sources)),
                "nxa_rating": lead_rating_values.get(random.choice(lead_ratings)),
                "nxa_status": lead_status_values.get(random.choice(lead_statuses)),
            }
            if random.random() < 0.5:
                data["nxa_accountid@odata.bind"] = f"/{entity_sets['account']}({random.choice(account_ids)})"
                data["nxa_contactid@odata.bind"] = f"/{entity_sets['contact']}({random.choice(contact_ids)})"
            new_id = create_record(org_url, token, entity_sets["nxa_lead"], data, "nxa_leadid")
            lead_ids.append(new_id)
            progress("Leads created", i, n)
    elif "opportunities" in selected or "recommendations" in selected or "action_logs" in selected:
        lead_ids = get_existing_ids(org_url, token, entity_sets["nxa_lead"], "nxa_leadid")
        log(f"Reusing {len(lead_ids)} existing leads (skipped creating new ones).")
    else:
        lead_ids = []

    # ---------------------------------------------------------------
    # 5. Opportunities
    # ---------------------------------------------------------------
    if "opportunities" in selected:
        n = RECORD_COUNTS["opportunities"]
        opp_stages = list(opp_salesstage_values.keys())
        opp_ratings = list(opp_rating_values.keys())
        opp_statuses = list(opp_status_values.keys())
        opportunity_ids: List[str] = []
        for i in range(n):
            company = random_company_name(used_company_names)
            name = f"{random.choice(OPPORTUNITY_TEMPLATES)} - {company}"
            est_value = round(random.uniform(5000, 500000), 2)
            data = {
                "nxa_name": name,
                "nxa_estimatedvalue": est_value,
                "nxa_totalamount": est_value,
                "nxa_closeprobability": random.randint(10, 95),
                "nxa_estimatedclosedate": iso(days_ahead=random.randint(10, 180)),
                "nxa_salesstage": opp_salesstage_values.get(random.choice(opp_stages)),
                "nxa_rating": opp_rating_values.get(random.choice(opp_ratings)),
                "nxa_status": opp_status_values.get(random.choice(opp_statuses)),
                "nxa_accountid@odata.bind": f"/{entity_sets['account']}({random.choice(account_ids)})",
                "nxa_contactid@odata.bind": f"/{entity_sets['contact']}({random.choice(contact_ids)})",
                "nxa_leadid@odata.bind": f"/{entity_sets['nxa_lead']}({random.choice(lead_ids)})",
            }
            new_id = create_record(org_url, token, entity_sets["nxa_opportunity"], data, "nxa_opportunityid")
            opportunity_ids.append(new_id)
            progress("Opportunities created", i, n)
    elif "recommendations" in selected or "action_logs" in selected:
        opportunity_ids = get_existing_ids(org_url, token, entity_sets["nxa_opportunity"], "nxa_opportunityid")
        log(f"Reusing {len(opportunity_ids)} existing opportunities (skipped creating new ones).")
    else:
        opportunity_ids = []

    # ---------------------------------------------------------------
    # 6. AI Recommendations
    # ---------------------------------------------------------------
    if "recommendations" in selected:
        n = RECORD_COUNTS["recommendations"]
        rec_statuses = list(rec_status_values.keys())
        for i in range(n):
            title, reason = random.choice(RECOMMENDATION_TEMPLATES)
            status = random.choice(rec_statuses)
            data = {
                "nxa_name": title,
                "nxa_recommendation": title,
                "nxa_reason": reason,
                "nxa_supportingevidence": f"Derived from account activity and product usage patterns as of {iso()[:10]}.",
                "nxa_confidence": round(random.uniform(50, 98), 2),
                "nxa_status": rec_status_values.get(status),
                "nxa_generatedby": "NexusAI Recommendation Agent v1",
                "nxa_generatedon": iso(days_ago=random.randint(0, 30)),
                "nxa_humanapproved": status in ("Approved", "Completed"),
                "nxa_accountid@odata.bind": f"/{entity_sets['account']}({random.choice(account_ids)})",
                "nxa_contactid@odata.bind": f"/{entity_sets['contact']}({random.choice(contact_ids)})",
                "nxa_leadid@odata.bind": f"/{entity_sets['nxa_lead']}({random.choice(lead_ids)})",
                "nxa_opportunityid@odata.bind": f"/{entity_sets['nxa_opportunity']}({random.choice(opportunity_ids)})",
                "nxa_productid@odata.bind": f"/{entity_sets['nxa_product']}({random.choice(product_ids)})",
            }
            if status in ("Approved", "Completed"):
                data["nxa_approvaldate"] = iso(days_ago=random.randint(0, 10))
            new_id = create_record(org_url, token, entity_sets["nxa_airecommendation"], data, "nxa_airecommendationid")
            progress("AI recommendations created", i, n)
    else:
        log("Skipping nxa_airecommendation (not in --only).")

    # ---------------------------------------------------------------
    # 7. AI Action Log
    # ---------------------------------------------------------------
    if "action_logs" in selected:
        n = RECORD_COUNTS["action_logs"]
        log_statuses = list(log_status_values.keys())
        for i in range(n):
            name, action_type, description = random.choice(ACTION_TEMPLATES)
            status = random.choice(log_statuses)
            requires_approval = random.random() < 0.5
            data = {
                "nxa_name": name,
                "nxa_actiontype": log_actiontype_values.get(action_type),
                "nxa_description": description,
                "nxa_agent": "NexusAI Action Agent v1",
                "nxa_status": log_status_values.get(status),
                "nxa_requiresapproval": requires_approval,
                "nxa_accountid@odata.bind": f"/{entity_sets['account']}({random.choice(account_ids)})",
                "nxa_contactid@odata.bind": f"/{entity_sets['contact']}({random.choice(contact_ids)})",
                "nxa_leadid@odata.bind": f"/{entity_sets['nxa_lead']}({random.choice(lead_ids)})",
                "nxa_opportunityid@odata.bind": f"/{entity_sets['nxa_opportunity']}({random.choice(opportunity_ids)})",
            }
            if requires_approval and status == "Completed":
                data["nxa_approvedby"] = "Relationship Manager"
                data["nxa_approvedon"] = iso(days_ago=random.randint(1, 10))
            if status == "Completed":
                data["nxa_executedon"] = iso(days_ago=random.randint(0, 5))
                data["nxa_result"] = "Action completed successfully."
            new_id = create_record(org_url, token, entity_sets["nxa_aiactionlog"], data, "nxa_aiactionlogid")
            progress("AI action logs created", i, n)
    else:
        log("Skipping nxa_aiactionlog (not in --only).")

    log(f"Sample data generation complete. Created new records for: {sorted(selected)}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except DataverseAuthError as exc:
        log(str(exc), level="ERROR")
        raise SystemExit(2)
    except Exception as exc:  # pragma: no cover
        log(f"Unhandled error: {exc}", level="ERROR")
        raise SystemExit(1)