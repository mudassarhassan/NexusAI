#!/usr/bin/env python3
"""Create the NexusAI Dataverse baseline only when missing.

This script follows the approved NexusAI data model:
- Reuse OOB tables Account and Contact
- Create custom tables nxa_lead and nxa_opportunity (Lead/Opportunity are not
  standard tables in this environment, so NexusAI models them as custom tables
  instead of waiting on the standard Sales app to be provisioned)
- Add Product only when already present in the org (skipped cleanly if absent)
- Create the two AI layer tables required by NexusAICore: nxa_airecommendation, nxa_aiactionlog
- Avoid creating nxa_customerproduct
- Remain idempotent so repeated execution does not recreate metadata

The script uses supported Dataverse OAuth with Microsoft Entra ID and then performs
CRUD against the Dataverse Web API.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time
from typing import Any, Dict, Iterable, List, Optional, Tuple

import requests


DEFAULT_DATAVERSE_URL = "https://nexusai.crm12.dynamics.com"
REQUIRED_SOLUTION_NAME = "NexusAICore"

# Standard, out-of-the-box tables this script never creates: it only checks
# whether they exist in the target environment and uses them as lookup targets
# when they do.
STANDALONE_LOOKUP_TARGETS = ["account", "contact"]

# Order matters: nxa_lead, nxa_opportunity and nxa_product must be created
# before nxa_airecommendation / nxa_aiactionlog, since those tables have
# lookups pointing at nxa_lead / nxa_opportunity / nxa_product.
CUSTOM_TABLES = {
    "nxa_lead": {
        "SchemaName": "nxa_Lead",
        "display_name": "NexusAI Lead",
        "collection_name": "NexusAI Leads",
        "description": "Custom lead table used by NexusAI; standard Lead is not available in this environment.",
        "ownership": "UserOwned",
        "fields": [
            {
                "SchemaName": "nxa_name",
                "LogicalName": "nxa_name",
                "DisplayName": "Topic",
                "Type": "StringType",
                "Required": True,
                "MaxLength": 200,
                "PrimaryName": True,
            },
            {
                "SchemaName": "nxa_firstname",
                "LogicalName": "nxa_firstname",
                "DisplayName": "First Name",
                "Type": "StringType",
                "Required": False,
                "MaxLength": 100,
            },
            {
                "SchemaName": "nxa_lastname",
                "LogicalName": "nxa_lastname",
                "DisplayName": "Last Name",
                "Type": "StringType",
                "Required": False,
                "MaxLength": 100,
            },
            {
                "SchemaName": "nxa_companyname",
                "LogicalName": "nxa_companyname",
                "DisplayName": "Company Name",
                "Type": "StringType",
                "Required": False,
                "MaxLength": 200,
            },
            {
                "SchemaName": "nxa_emailaddress",
                "LogicalName": "nxa_emailaddress",
                "DisplayName": "Email",
                "Type": "StringType",
                "Required": False,
                "MaxLength": 200,
                "Format": "Email",
            },
            {
                "SchemaName": "nxa_telephone",
                "LogicalName": "nxa_telephone",
                "DisplayName": "Phone",
                "Type": "StringType",
                "Required": False,
                "MaxLength": 50,
                "Format": "Phone",
            },
            {
                "SchemaName": "nxa_accountid",
                "LogicalName": "nxa_accountid",
                "DisplayName": "Account",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "account",
            },
            {
                "SchemaName": "nxa_contactid",
                "LogicalName": "nxa_contactid",
                "DisplayName": "Contact",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "contact",
            },
            {
                "SchemaName": "nxa_leadsource",
                "LogicalName": "nxa_leadsource",
                "DisplayName": "Lead Source",
                "Type": "PicklistType",
                "Required": False,
                "OptionSet": ["Web", "Referral", "Advertisement", "Cold Call", "Partner", "Other"],
            },
            {
                "SchemaName": "nxa_rating",
                "LogicalName": "nxa_rating",
                "DisplayName": "Rating",
                "Type": "PicklistType",
                "Required": False,
                "OptionSet": ["Hot", "Warm", "Cold"],
            },
            {
                "SchemaName": "nxa_status",
                "LogicalName": "nxa_status",
                "DisplayName": "Status",
                "Type": "PicklistType",
                "Required": True,
                "OptionSet": ["New", "Contacted", "Qualified", "Disqualified", "Converted"],
            },
        ],
    },
    "nxa_opportunity": {
        "SchemaName": "nxa_Opportunity",
        "display_name": "NexusAI Opportunity",
        "collection_name": "NexusAI Opportunities",
        "description": "Custom opportunity table used by NexusAI; standard Opportunity is not available in this environment.",
        "ownership": "UserOwned",
        "fields": [
            {
                "SchemaName": "nxa_name",
                "LogicalName": "nxa_name",
                "DisplayName": "Opportunity Name",
                "Type": "StringType",
                "Required": True,
                "MaxLength": 200,
                "PrimaryName": True,
            },
            {
                "SchemaName": "nxa_accountid",
                "LogicalName": "nxa_accountid",
                "DisplayName": "Account",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "account",
            },
            {
                "SchemaName": "nxa_contactid",
                "LogicalName": "nxa_contactid",
                "DisplayName": "Contact",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "contact",
            },
            {
                "SchemaName": "nxa_leadid",
                "LogicalName": "nxa_leadid",
                "DisplayName": "Originating Lead",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "nxa_lead",
            },
            {
                "SchemaName": "nxa_estimatedvalue",
                "LogicalName": "nxa_estimatedvalue",
                "DisplayName": "Estimated Value",
                "Type": "DecimalType",
                "Required": False,
                "Precision": 2,
                "MinValue": 0.0,
                "MaxValue": 100000000.0,
            },
            {
                "SchemaName": "nxa_totalamount",
                "LogicalName": "nxa_totalamount",
                "DisplayName": "Total Amount",
                "Type": "DecimalType",
                "Required": False,
                "Precision": 2,
                "MinValue": 0.0,
                "MaxValue": 100000000.0,
            },
            {
                "SchemaName": "nxa_closeprobability",
                "LogicalName": "nxa_closeprobability",
                "DisplayName": "Close Probability",
                "Type": "IntegerType",
                "Required": False,
                "MinValue": 0,
                "MaxValue": 100,
            },
            {
                "SchemaName": "nxa_estimatedclosedate",
                "LogicalName": "nxa_estimatedclosedate",
                "DisplayName": "Estimated Close Date",
                "Type": "DateTimeType",
                "Required": False,
            },
            {
                "SchemaName": "nxa_salesstage",
                "LogicalName": "nxa_salesstage",
                "DisplayName": "Sales Stage",
                "Type": "PicklistType",
                "Required": False,
                "OptionSet": ["Qualify", "Develop", "Propose", "Close"],
            },
            {
                "SchemaName": "nxa_rating",
                "LogicalName": "nxa_rating",
                "DisplayName": "Rating",
                "Type": "PicklistType",
                "Required": False,
                "OptionSet": ["Hot", "Warm", "Cold"],
            },
            {
                "SchemaName": "nxa_status",
                "LogicalName": "nxa_status",
                "DisplayName": "Status",
                "Type": "PicklistType",
                "Required": True,
                "OptionSet": ["Open", "Won", "Lost"],
            },
        ],
    },
    "nxa_product": {
        "SchemaName": "nxa_Product",
        "display_name": "NexusAI Product",
        "collection_name": "NexusAI Products",
        "description": "Custom product table used by NexusAI; standard Product is not available in this environment.",
        "ownership": "UserOwned",
        "fields": [
            {
                "SchemaName": "nxa_name",
                "LogicalName": "nxa_name",
                "DisplayName": "Product Name",
                "Type": "StringType",
                "Required": True,
                "MaxLength": 200,
                "PrimaryName": True,
            },
            {
                "SchemaName": "nxa_productnumber",
                "LogicalName": "nxa_productnumber",
                "DisplayName": "Product Number",
                "Type": "StringType",
                "Required": False,
                "MaxLength": 100,
            },
            {
                "SchemaName": "nxa_producttype",
                "LogicalName": "nxa_producttype",
                "DisplayName": "Product Type",
                "Type": "PicklistType",
                "Required": False,
                "OptionSet": ["Inventory", "Service", "Bundle", "Other"],
            },
            {
                "SchemaName": "nxa_currentcost",
                "LogicalName": "nxa_currentcost",
                "DisplayName": "Current Cost",
                "Type": "DecimalType",
                "Required": False,
                "Precision": 2,
                "MinValue": 0.0,
                "MaxValue": 100000000.0,
            },
            {
                "SchemaName": "nxa_price",
                "LogicalName": "nxa_price",
                "DisplayName": "List Price",
                "Type": "DecimalType",
                "Required": False,
                "Precision": 2,
                "MinValue": 0.0,
                "MaxValue": 100000000.0,
            },
            {
                "SchemaName": "nxa_parentproductid",
                "LogicalName": "nxa_parentproductid",
                "DisplayName": "Parent Product",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "nxa_product",
            },
        ],
    },
    "nxa_airecommendation": {
        "SchemaName": "nxa_AIRecommendation",
        "display_name": "AI Recommendation",
        "collection_name": "AI Recommendations",
        "description": "Stores recommendations generated by the NexusAI AI agent.",
        "ownership": "UserOwned",
        "fields": [
            {
                "SchemaName": "nxa_name",
                "LogicalName": "nxa_name",
                "DisplayName": "Recommendation Name",
                "Type": "StringType",
                "Required": True,
                "MaxLength": 200,
                "PrimaryName": True,
            },
            {
                "SchemaName": "nxa_accountid",
                "LogicalName": "nxa_accountid",
                "DisplayName": "Account",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "account",
            },
            {
                "SchemaName": "nxa_contactid",
                "LogicalName": "nxa_contactid",
                "DisplayName": "Contact",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "contact",
            },
            {
                "SchemaName": "nxa_leadid",
                "LogicalName": "nxa_leadid",
                "DisplayName": "Lead",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "nxa_lead",
            },
            {
                "SchemaName": "nxa_opportunityid",
                "LogicalName": "nxa_opportunityid",
                "DisplayName": "Opportunity",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "nxa_opportunity",
            },
            {
                "SchemaName": "nxa_productid",
                "LogicalName": "nxa_productid",
                "DisplayName": "Product",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "nxa_product",
            },
            {
                "SchemaName": "nxa_recommendation",
                "LogicalName": "nxa_recommendation",
                "DisplayName": "Recommendation",
                "Type": "MemoType",
                "Required": True,
            },
            {
                "SchemaName": "nxa_reason",
                "LogicalName": "nxa_reason",
                "DisplayName": "Reason",
                "Type": "MemoType",
                "Required": True,
            },
            {
                "SchemaName": "nxa_supportingevidence",
                "LogicalName": "nxa_supportingevidence",
                "DisplayName": "Supporting Evidence",
                "Type": "MemoType",
                "Required": False,
            },
            {
                "SchemaName": "nxa_confidence",
                "LogicalName": "nxa_confidence",
                "DisplayName": "Confidence",
                "Type": "DecimalType",
                "Required": False,
                "Precision": 2,
            },
            {
                "SchemaName": "nxa_status",
                "LogicalName": "nxa_status",
                "DisplayName": "Status",
                "Type": "PicklistType",
                "Required": True,
                "OptionSet": [
                    "Draft",
                    "Pending Approval",
                    "Approved",
                    "Rejected",
                    "Completed",
                    "Cancelled",
                ],
            },
            {
                "SchemaName": "nxa_generatedby",
                "LogicalName": "nxa_generatedby",
                "DisplayName": "Generated By",
                "Type": "StringType",
                "Required": False,
                "MaxLength": 200,
            },
            {
                "SchemaName": "nxa_generatedon",
                "LogicalName": "nxa_generatedon",
                "DisplayName": "Generated On",
                "Type": "DateTimeType",
                "Required": True,
            },
            {
                "SchemaName": "nxa_humanapproved",
                "LogicalName": "nxa_humanapproved",
                "DisplayName": "Human Approved",
                "Type": "BooleanType",
                "Required": True,
            },
            {
                "SchemaName": "nxa_approvaldate",
                "LogicalName": "nxa_approvaldate",
                "DisplayName": "Approval Date",
                "Type": "DateTimeType",
                "Required": False,
            },
        ],
    },
    "nxa_aiactionlog": {
        "SchemaName": "nxa_AIActionLog",
        "display_name": "AI Action Log",
        "collection_name": "AI Action Logs",
        "description": "Record AI-proposed and AI-executed actions for auditability.",
        "ownership": "UserOwned",
        "fields": [
            {
                "SchemaName": "nxa_name",
                "LogicalName": "nxa_name",
                "DisplayName": "Action Name",
                "Type": "StringType",
                "Required": True,
                "MaxLength": 200,
                "PrimaryName": True,
            },
            {
                "SchemaName": "nxa_accountid",
                "LogicalName": "nxa_accountid",
                "DisplayName": "Account",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "account",
            },
            {
                "SchemaName": "nxa_contactid",
                "LogicalName": "nxa_contactid",
                "DisplayName": "Contact",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "contact",
            },
            {
                "SchemaName": "nxa_leadid",
                "LogicalName": "nxa_leadid",
                "DisplayName": "Lead",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "nxa_lead",
            },
            {
                "SchemaName": "nxa_opportunityid",
                "LogicalName": "nxa_opportunityid",
                "DisplayName": "Opportunity",
                "Type": "LookupType",
                "Required": False,
                "TargetEntityLogicalName": "nxa_opportunity",
            },
            {
                "SchemaName": "nxa_actiontype",
                "LogicalName": "nxa_actiontype",
                "DisplayName": "Action Type",
                "Type": "PicklistType",
                "Required": True,
                "OptionSet": [
                    "Create Record",
                    "Update Record",
                    "Send Communication",
                    "Create Task",
                    "Assign Lead",
                    "Update Opportunity",
                    "Generate Recommendation",
                    "Other",
                ],
            },
            {
                "SchemaName": "nxa_description",
                "LogicalName": "nxa_description",
                "DisplayName": "Description",
                "Type": "MemoType",
                "Required": True,
            },
            {
                "SchemaName": "nxa_agent",
                "LogicalName": "nxa_agent",
                "DisplayName": "Agent",
                "Type": "StringType",
                "Required": True,
                "MaxLength": 200,
            },
            {
                "SchemaName": "nxa_status",
                "LogicalName": "nxa_status",
                "DisplayName": "Status",
                "Type": "PicklistType",
                "Required": True,
                "OptionSet": [
                    "Proposed",
                    "Pending Approval",
                    "Approved",
                    "Rejected",
                    "Executing",
                    "Completed",
                    "Failed",
                    "Cancelled",
                ],
            },
            {
                "SchemaName": "nxa_requiresapproval",
                "LogicalName": "nxa_requiresapproval",
                "DisplayName": "Requires Approval",
                "Type": "BooleanType",
                "Required": True,
            },
            {
                "SchemaName": "nxa_approvedby",
                "LogicalName": "nxa_approvedby",
                "DisplayName": "Approved By",
                "Type": "StringType",
                "Required": False,
                "MaxLength": 200,
            },
            {
                "SchemaName": "nxa_approvedon",
                "LogicalName": "nxa_approvedon",
                "DisplayName": "Approved On",
                "Type": "DateTimeType",
                "Required": False,
            },
            {
                "SchemaName": "nxa_executedon",
                "LogicalName": "nxa_executedon",
                "DisplayName": "Executed On",
                "Type": "DateTimeType",
                "Required": False,
            },
            {
                "SchemaName": "nxa_result",
                "LogicalName": "nxa_result",
                "DisplayName": "Result",
                "Type": "MemoType",
                "Required": False,
            },
            {
                "SchemaName": "nxa_errordetails",
                "LogicalName": "nxa_errordetails",
                "DisplayName": "Error Details",
                "Type": "MemoType",
                "Required": False,
            },
        ],
    },
}


class DataverseAuthError(RuntimeError):
    pass


def log(message: str, *, level: str = "INFO") -> None:
    print(f"[{level}] {message}")


def read_env(name: str, *, required: bool = False) -> Optional[str]:
    value = os.getenv(name)
    if required and (value is None or not value.strip()):
        raise DataverseAuthError(f"Missing required environment variable: {name}")
    return value.strip() if value else None


def get_active_pac_environment() -> Optional[str]:
    pac_path = shutil.which("pac") or shutil.which("pac.cmd") or shutil.which("pac.CMD")
    if pac_path is None:
        return None

    try:
        result = subprocess.run(
            [pac_path, "auth", "list"],
            capture_output=True,
            text=True,
            check=False,
        )
        if result.returncode != 0:
            return None

        lines = result.stdout.splitlines()
        for line in lines:
            if not line.strip():
                continue
            if line.startswith("Index") or "Environment Url" in line:
                continue
            if line.strip().startswith("[") and "*" in line:
                match = re.search(r"https?://[^\s]+", line)
                if match:
                    return match.group(0).rstrip("/")
    except Exception:
        return None

    return None


def resolve_org_url() -> str:
    url = read_env("DATAVERSE_URL") or read_env("DYNAMICS_URL") or read_env("ORG_URL")
    if url:
        return url.rstrip("/")

    pac_url = get_active_pac_environment()
    if pac_url:
        return pac_url.rstrip("/")

    return DEFAULT_DATAVERSE_URL


def get_auth_config() -> Dict[str, str]:
    tenant = read_env("DATAVERSE_TENANT_ID") or read_env("AZURE_TENANT_ID")
    client_id = read_env("DATAVERSE_CLIENT_ID") or read_env("AZURE_CLIENT_ID")
    client_secret = read_env("DATAVERSE_CLIENT_SECRET") or read_env("AZURE_CLIENT_SECRET")

    config: Dict[str, str] = {}
    if tenant:
        config["tenant_id"] = tenant
    if client_id:
        config["client_id"] = client_id
    if client_secret:
        config["client_secret"] = client_secret
    return config


def acquire_token(org_url: str, config: Dict[str, str]) -> str:
    if config.get("tenant_id") and config.get("client_id") and config.get("client_secret"):
        scope = f"{org_url}/.default"
        token_url = f"https://login.microsoftonline.com/{config['tenant_id']}/oauth2/v2.0/token"

        payload = {
            "client_id": config["client_id"],
            "client_secret": config["client_secret"],
            "scope": scope,
            "grant_type": "client_credentials",
        }

        response = requests.post(token_url, data=payload, timeout=60)
        if response.status_code != 200:
            raise DataverseAuthError(f"Failed to get Dataverse access token: {response.text}")

        token_json = response.json()
        return token_json["access_token"]

    az_path = shutil.which("az") or shutil.which("az.cmd")
    if az_path:
        az_check = subprocess.run([az_path, "account", "show"], capture_output=True, text=True, check=False)
        if az_check.returncode != 0:
            raise DataverseAuthError(
                "Interactive authentication required. Sign in with Azure CLI using 'az login' "
                "and then rerun this script."
            )

        token_cmd = subprocess.run(
            [az_path, "account", "get-access-token", "--resource", org_url, "--query", "accessToken", "-o", "tsv"],
            capture_output=True,
            text=True,
            check=False,
        )
        if token_cmd.returncode != 0:
            raise DataverseAuthError(
                "Unable to acquire a Dataverse access token from Azure CLI. Run 'az login' and retry. "
                f"Details: {token_cmd.stderr.strip() or token_cmd.stdout.strip()}"
            )
        token = token_cmd.stdout.strip()
        if not token:
            raise DataverseAuthError("Azure CLI returned an empty access token.")
        return token

    raise DataverseAuthError(
        "No Dataverse app credentials were supplied and Azure CLI is not available. "
        "Authenticate interactively with Azure CLI ('az login') or provide DATAVERSE_TENANT_ID / DATAVERSE_CLIENT_ID / DATAVERSE_CLIENT_SECRET."
    )


def call_dataverse(org_url: str, token: str, relative_url: str, *, method: str = "GET", json_body: Optional[Dict[str, Any]] = None) -> Any:
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/json",
        "Content-Type": "application/json; charset=utf-8",
        "OData-Version": "4.0",
        "OData-MaxVersion": "4.0",
    }

    url = f"{org_url}{relative_url}"
    response = requests.request(method, url, headers=headers, data=(json.dumps(json_body) if json_body is not None else None), timeout=120)

    if response.status_code in (200, 201, 202, 204):
        if response.text.strip():
            try:
                return response.json()
            except ValueError:
                return response.text
        return None

    if response.status_code == 404 and method == "DELETE":
        return None

    sent_body = json.dumps(json_body, indent=2) if json_body is not None else "(none)"
    raise RuntimeError(
        f"Dataverse request failed for {method} {relative_url}: {response.status_code}\n"
        f"--- Request body sent ---\n{sent_body}\n"
        f"--- Response ---\n{response.text[:2000]}"
    )


def verify_active_environment(org_url: str, token: str) -> None:
    pac_url = get_active_pac_environment()
    if pac_url and org_url.rstrip("/") != pac_url.rstrip("/"):
        raise DataverseAuthError(
            f"Active Dataverse environment from pac auth list ({pac_url}) does not match the configured org URL ({org_url}). "
            "Aborting to avoid making changes in the wrong Dataverse environment."
        )

    whoami = call_dataverse(org_url, token, "/api/data/v9.2/WhoAmI")
    if not whoami or not isinstance(whoami, dict):
        raise DataverseAuthError("WhoAmI request did not return expected data: authentication likely failed.")

    log(f"Verified Dataverse identity; UserId: {whoami.get('UserId', 'unknown')}")


def get_solution_unique_name(org_url: str, token: str) -> str:
    query = "/api/data/v9.2/solutions?$select=solutionid,uniquename,friendlyname&$filter=(friendlyname eq 'NexusAICore' or uniquename eq 'NexusAICore')"
    result = call_dataverse(org_url, token, query)
    value = result.get("value", []) if isinstance(result, dict) else []
    for solution in value:
        if solution.get("uniquename") == REQUIRED_SOLUTION_NAME:
            return solution["uniquename"]
        if solution.get("friendlyname") == REQUIRED_SOLUTION_NAME:
            return solution["uniquename"]

    raise RuntimeError(f"Solution '{REQUIRED_SOLUTION_NAME}' was not found in the active Dataverse environment.")


def get_entity_metadata(org_url: str, token: str, logical_names: Iterable[str]) -> Dict[str, Dict[str, Any]]:
    names = [name.lower() for name in logical_names]
    query = (
        "/api/data/v9.2/EntityDefinitions?$select=LogicalName,SchemaName,DisplayName,MetadataId,IsCustomEntity&"
        "$filter=" + " or ".join(f"LogicalName eq '{name}'" for name in names)
    )
    result = call_dataverse(org_url, token, query)
    values = result.get("value", []) if isinstance(result, dict) else []
    data: Dict[str, Dict[str, Any]] = {}
    for item in values:
        logical_name = str(item.get("LogicalName") or "").lower()
        if logical_name:
            data[logical_name] = item
    return data


def find_primary_name_field(fields: List[Dict[str, Any]]) -> Dict[str, Any]:
    for field in fields:
        if field.get("PrimaryName"):
            return field
    raise ValueError("No field marked PrimaryName=True found for this table.")


def build_primary_attribute_payload(field: Dict[str, Any]) -> Dict[str, Any]:
    return {
        "@odata.type": "Microsoft.Dynamics.CRM.StringAttributeMetadata",
        "SchemaName": field["SchemaName"],
        "DisplayName": {
            "LocalizedLabels": [
                {"LanguageCode": 1033, "Label": field["DisplayName"], "IsManaged": False}
            ]
        },
        "RequiredLevel": {"Value": "ApplicationRequired" if field.get("Required") else "None", "CanBeChanged": True},
        "MaxLength": field.get("MaxLength", 100),
        "FormatName": {"Value": "Text"},
        "IsPrimaryName": True,
    }


def ensure_table_exists(org_url: str, token: str, logical_name: str, defn: Dict[str, Any]) -> bool:
    entity_metadata = get_entity_metadata(org_url, token, [logical_name])
    if logical_name in entity_metadata:
        log(f"Table already exists: {logical_name}")
        return True

    primary_field = find_primary_name_field(defn["fields"])

    payload = {
        "@odata.type": "Microsoft.Dynamics.CRM.EntityMetadata",
        "SchemaName": defn["SchemaName"],
        "LogicalName": logical_name,
        "DisplayName": {
            "LocalizedLabels": [
                {
                    "LanguageCode": 1033,
                    "Label": defn["display_name"],
                    "IsManaged": False,
                }
            ]
        },
        "Description": {
            "LocalizedLabels": [
                {
                    "LanguageCode": 1033,
                    "Label": defn["description"],
                    "IsManaged": False,
                }
            ]
        },
        "DisplayCollectionName": {
            "LocalizedLabels": [
                {
                    "LanguageCode": 1033,
                    "Label": defn["collection_name"],
                    "IsManaged": False,
                }
            ]
        },
        "OwnershipType": defn["ownership"],
        "HasActivities": False,
        "HasNotes": False,
        "IsActivity": False,
        "IsCustomEntity": True,
        "Attributes": [build_primary_attribute_payload(primary_field)],
    }
    created = call_dataverse(org_url, token, "/api/data/v9.2/EntityDefinitions", method="POST", json_body=payload)
    log(f"Created table: {logical_name} ({created.get('LogicalName') if isinstance(created, dict) else logical_name})")
    return True


def attribute_exists(org_url: str, token: str, logical_name: str, field_name: str) -> bool:
    entity = get_entity_metadata(org_url, token, [logical_name])
    if logical_name not in entity:
        return False
    metadata_id = entity[logical_name].get("MetadataId")
    if not metadata_id:
        return False

    query = f"/api/data/v9.2/EntityDefinitions({metadata_id})/Attributes?$select=LogicalName,SchemaName&$filter=LogicalName eq '{field_name}'"
    try:
        result = call_dataverse(org_url, token, query)
        values = result.get("value", []) if isinstance(result, dict) else []
        return any(item.get("LogicalName", "").lower() == field_name.lower() for item in values)
    except RuntimeError:
        return False


def build_attribute_payload(field: Dict[str, Any], *, entity_logical_name: str, tag: str) -> Dict[str, Any]:
    field_name = field["SchemaName"]
    base = {
        "SchemaName": field_name,
        "DisplayName": {
            "LocalizedLabels": [
                {"LanguageCode": 1033, "Label": field["DisplayName"], "IsManaged": False}
            ]
        },
        "Description": {
            "LocalizedLabels": [
                {"LanguageCode": 1033, "Label": f"{field['DisplayName']} for {entity_logical_name}", "IsManaged": False}
            ]
        },
        "RequiredLevel": {"Value": "ApplicationRequired" if field.get("Required") else "None", "CanBeChanged": True},
        "IsCustomAttribute": True,
    }

    if field["Type"] == "StringType":
        payload = {
            **base,
            "@odata.type": "Microsoft.Dynamics.CRM.StringAttributeMetadata",
            "IsPrimaryName": field.get("PrimaryName", False),
            "MaxLength": field.get("MaxLength", 100),
            "FormatName": {"Value": field.get("Format", "Text")},
        }
        return payload

    if field["Type"] == "MemoType":
        return {
            **base,
            "@odata.type": "Microsoft.Dynamics.CRM.MemoAttributeMetadata",
            "FormatName": {"Value": "TextArea"},
            "IsPrimaryName": False,
            "MaxLength": field.get("MaxLength", 2000),
        }

    if field["Type"] == "DateTimeType":
        return {
            **base,
            "@odata.type": "Microsoft.Dynamics.CRM.DateTimeAttributeMetadata",
            "Format": "DateOnly" if tag in ("Generated On", "Approval Date", "Executed On", "Estimated Close Date") else "DateAndTime",
        }

    if field["Type"] == "BooleanType":
        return {
            **base,
            "@odata.type": "Microsoft.Dynamics.CRM.BooleanAttributeMetadata",
            "OptionSet": {
                "TrueOption": {"Value": 1, "Label": {"LocalizedLabels": [{"LanguageCode": 1033, "Label": "Yes", "IsManaged": False}]}},
                "FalseOption": {"Value": 0, "Label": {"LocalizedLabels": [{"LanguageCode": 1033, "Label": "No", "IsManaged": False}]}},
            },
        }

    if field["Type"] == "DecimalType":
        return {
            **base,
            "@odata.type": "Microsoft.Dynamics.CRM.DecimalAttributeMetadata",
            "Precision": field.get("Precision", 2),
            "MinValue": field.get("MinValue", 0.0),
            "MaxValue": field.get("MaxValue", 100.0),
        }

    if field["Type"] == "IntegerType":
        return {
            **base,
            "@odata.type": "Microsoft.Dynamics.CRM.IntegerAttributeMetadata",
            "Format": "None",
            "MinValue": field.get("MinValue", 0),
            "MaxValue": field.get("MaxValue", 100),
        }

    if field["Type"] == "PicklistType":
        options = []
        for index, name in enumerate(field.get("OptionSet", []), start=1):
            options.append({
                "Value": index * 100000000,
                "Label": {"LocalizedLabels": [{"LanguageCode": 1033, "Label": name, "IsManaged": False}]},
            })
        return {
            **base,
            "@odata.type": "Microsoft.Dynamics.CRM.PicklistAttributeMetadata",
            "OptionSet": {
                "@odata.type": "Microsoft.Dynamics.CRM.OptionSetMetadata",
                "IsGlobal": False,
                "OptionSetType": "Picklist",
                "Options": options,
            },
        }

    raise ValueError(f"Unsupported field type: {field['Type']} for {field_name}")


def create_lookup_relationship(org_url: str, token: str, entity_logical_name: str, field: Dict[str, Any]) -> None:
    """Create a lookup column via a OneToMany relationship, which is how Dataverse's
    Web API actually creates lookups (a bare Attributes POST does not work for LookupType)."""
    field_name = field["SchemaName"]
    target_entity = field["TargetEntityLogicalName"]

    relationship_schema_name = f"{target_entity}_{entity_logical_name}_{field_name}"
    # Dataverse relationship schema names have a length limit; trim defensively.
    relationship_schema_name = relationship_schema_name[:100]

    payload = {
        "@odata.type": "Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata",
        "SchemaName": relationship_schema_name,
        "ReferencedEntity": target_entity,
        "ReferencingEntity": entity_logical_name,
        "Lookup": {
            "AttributeType": "Lookup",
            "AttributeTypeName": {"Value": "LookupType"},
            "SchemaName": field_name,
            "DisplayName": {
                "LocalizedLabels": [
                    {"LanguageCode": 1033, "Label": field["DisplayName"], "IsManaged": False}
                ]
            },
            "Description": {
                "LocalizedLabels": [
                    {
                        "LanguageCode": 1033,
                        "Label": f"Lookup to {target_entity}",
                        "IsManaged": False,
                    }
                ]
            },
            "RequiredLevel": {"Value": "ApplicationRequired" if field.get("Required") else "None", "CanBeChanged": True},
        },
    }

    call_dataverse(org_url, token, "/api/data/v9.2/RelationshipDefinitions", method="POST", json_body=payload)
    log(f"Created lookup relationship: {entity_logical_name}.{field_name} -> {target_entity}")


def ensure_field(org_url: str, token: str, entity_logical_name: str, field: Dict[str, Any], available_targets: Optional[set] = None) -> None:
    field_name = field["SchemaName"]

    if field["Type"] == "LookupType":
        target = field["TargetEntityLogicalName"]
        # A lookup target is usable if it's a standalone OOB table confirmed present
        # in this environment, OR one of our own custom tables (which this script
        # creates earlier in CUSTOM_TABLES' insertion order).
        is_available = (available_targets is not None and target in available_targets) or (target in CUSTOM_TABLES)
        if not is_available:
            log(f"Skipping lookup field {entity_logical_name}.{field_name}: target table '{target}' is not present in this environment.")
            return

    if attribute_exists(org_url, token, entity_logical_name, field_name):
        log(f"Field already exists: {entity_logical_name}.{field_name}")
        return

    log(f"Creating field: {entity_logical_name}.{field_name} (Type={field['Type']})")

    if field["Type"] == "LookupType":
        create_lookup_relationship(org_url, token, entity_logical_name, field)
        return

    entity_url = f"/api/data/v9.2/EntityDefinitions(LogicalName='{entity_logical_name}')/Attributes"
    payload = build_attribute_payload(field, entity_logical_name=entity_logical_name, tag=field.get("DisplayName", ""))
    call_dataverse(org_url, token, entity_url, method="POST", json_body=payload)
    log(f"Created field: {entity_logical_name}.{field_name}")


def ensure_fields(org_url: str, token: str, logical_name: str, fields: List[Dict[str, Any]], available_targets: Optional[set] = None) -> None:
    for field in fields:
        if field.get("PrimaryName"):
            # Created as part of entity creation (PrimaryAttribute); skip here.
            continue
        ensure_field(org_url, token, logical_name, field, available_targets=available_targets)


def add_table_to_solution(org_url: str, token: str, unique_name: str, logical_name: str) -> None:
    entity_metadata = get_entity_metadata(org_url, token, [logical_name])
    if logical_name not in entity_metadata:
        raise RuntimeError(f"Unable to add {logical_name} to solution because it does not exist.")

    metadata_id = entity_metadata[logical_name].get("MetadataId")
    if not metadata_id:
        raise RuntimeError(f"Missing MetadataId for {logical_name}.")

    add_ref = {
        "ComponentType": 1,
        "SolutionUniqueName": unique_name,
        "ComponentId": metadata_id,
        "AddRequiredComponents": False,
        "DoNotIncludeSubcomponents": False,
    }
    add_url = f"/api/data/v9.2/AddSolutionComponent"
    try:
        call_dataverse(org_url, token, add_url, method="POST", json_body=add_ref)
        log(f"Associated table with solution: {logical_name}")
    except RuntimeError as exc:
        if "already exists" in str(exc).lower() or "already associated" in str(exc).lower():
            log(f"Table already associated with solution: {logical_name}")
            return
        raise


def publish_all(org_url: str, token: str) -> None:
    call_dataverse(org_url, token, "/api/data/v9.2/PublishAllXml", method="POST", json_body={})
    log("Metadata publish requested successfully.")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create the NexusAI Dataverse foundation metadata idempotently.")
    parser.add_argument("--org-url", default=os.getenv("DATAVERSE_URL") or os.getenv("DYNAMICS_URL") or os.getenv("ORG_URL"), help="Dataverse org URL, for example https://nexusai.crm12.dynamics.com")
    parser.add_argument("--tenant-id", default=os.getenv("DATAVERSE_TENANT_ID") or os.getenv("AZURE_TENANT_ID"), help="Microsoft Entra tenant ID")
    parser.add_argument("--client-id", default=os.getenv("DATAVERSE_CLIENT_ID") or os.getenv("AZURE_CLIENT_ID"), help="Dataverse app registration client ID")
    parser.add_argument("--client-secret", default=os.getenv("DATAVERSE_CLIENT_SECRET") or os.getenv("AZURE_CLIENT_SECRET"), help="Dataverse app registration client secret")
    parser.add_argument("--solution-name", default=REQUIRED_SOLUTION_NAME, help="Solution unique name to associate created components with")
    parser.add_argument("--skip-solution-association", action="store_true", help="Skip solution association and publish step")
    parser.add_argument("--verbose", action="store_true", help="Enable verbose logging")
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    org_url = (args.org_url or resolve_org_url()).rstrip("/")
    if not org_url:
        raise DataverseAuthError("Unable to determine Dataverse org URL. Set DATAVERSE_URL or DYNAMICS_URL.")

    config = get_auth_config()
    if args.tenant_id:
        config["tenant_id"] = args.tenant_id
    if args.client_id:
        config["client_id"] = args.client_id
    if args.client_secret:
        config["client_secret"] = args.client_secret

    token = acquire_token(org_url, config)
    verify_active_environment(org_url, token)

    log(f"Target Dataverse org: {org_url}")
    solution_unique_name = get_solution_unique_name(org_url, token)
    log(f"Solution unique name resolved to: {solution_unique_name}")

    if args.solution_name and args.solution_name != REQUIRED_SOLUTION_NAME:
        log(f"Using requested solution name override: {args.solution_name}")
        solution_unique_name = args.solution_name

    environment_tables = get_entity_metadata(
        org_url,
        token,
        STANDALONE_LOOKUP_TARGETS + ["activitypointer"] + list(CUSTOM_TABLES.keys()),
    )

    available_lookup_targets = {name for name in STANDALONE_LOOKUP_TARGETS if name in environment_tables}
    missing_targets = set(STANDALONE_LOOKUP_TARGETS) - available_lookup_targets
    if missing_targets:
        log(f"Standard lookup targets not present in this environment, their lookup fields will be skipped: {sorted(missing_targets)}")

    for logical_name, defn in CUSTOM_TABLES.items():
        if logical_name in environment_tables:
            log(f"Custom table already exists; skipping creation: {logical_name}")
        else:
            ensure_table_exists(org_url, token, logical_name, defn)
        ensure_fields(org_url, token, logical_name, defn["fields"], available_targets=available_lookup_targets)

    if not args.skip_solution_association:
        for logical_name in CUSTOM_TABLES.keys():
            add_table_to_solution(org_url, token, solution_unique_name, logical_name)
        publish_all(org_url, token)

    log("Dataverse metadata sync complete.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except DataverseAuthError as exc:
        log(str(exc), level="ERROR")
        raise SystemExit(2)
    except Exception as exc:  # pragma: no cover - keep script failure explicit
        log(f"Unhandled error: {exc}", level="ERROR")
        raise SystemExit(1)