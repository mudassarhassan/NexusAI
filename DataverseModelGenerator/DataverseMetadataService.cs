using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseModelGenerator;

public static class DataverseMetadataService
{
    private static Label Lbl(string text) => new(text, 1033);

    private static AttributeRequiredLevelManagedProperty Req(bool required) =>
        new(required ? AttributeRequiredLevel.ApplicationRequired : AttributeRequiredLevel.None);

    public static bool TryGetEntityMetadata(IOrganizationService svc, string logicalName, out EntityMetadata? metadata)
    {
        try
        {
            var response = (RetrieveEntityResponse)svc.Execute(new RetrieveEntityRequest
            {
                LogicalName = logicalName,
                EntityFilters = EntityFilters.Entity,
            });
            metadata = response.EntityMetadata;
            return true;
        }
        catch (Exception)
        {
            // Dataverse throws when the entity does not exist; treat any
            // exception here as "not found" rather than trying to parse the
            // fault code, matching the Python script's try/except pattern.
            metadata = null;
            return false;
        }
    }

    public static bool AttributeExists(IOrganizationService svc, string entityLogicalName, string attributeLogicalName)
    {
        try
        {
            svc.Execute(new RetrieveAttributeRequest
            {
                EntityLogicalName = entityLogicalName,
                LogicalName = attributeLogicalName,
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void EnsureTableExists(IOrganizationService svc, TableDefinition table)
    {
        if (TryGetEntityMetadata(svc, table.LogicalName, out _))
        {
            Log.Info($"Table already exists: {table.LogicalName}");
            return;
        }

        var primary = table.PrimaryField;

        var entityMetadata = new EntityMetadata
        {
            SchemaName = table.SchemaName,
            DisplayName = Lbl(table.DisplayName),
            DisplayCollectionName = Lbl(table.CollectionName),
            Description = Lbl(table.Description),
            OwnershipType = OwnershipTypes.UserOwned,
            HasActivities = false,
            HasNotes = false,
        };

        var primaryAttribute = new StringAttributeMetadata
        {
            SchemaName = primary.SchemaName,
            DisplayName = Lbl(primary.DisplayName),
            RequiredLevel = Req(primary.Required),
            MaxLength = primary.MaxLength ?? 100,
            FormatName = StringFormatName.Text,
        };

        var request = new CreateEntityRequest
        {
            Entity = entityMetadata,
            PrimaryAttribute = primaryAttribute,
        };

        svc.Execute(request);
        Log.Info($"Created table: {table.LogicalName}");
    }

    private static AttributeMetadata BuildAttributeMetadata(FieldDefinition field, string entityLogicalName)
    {
        var descriptionLabel = Lbl($"{field.DisplayName} for {entityLogicalName}");

        switch (field.Type)
        {
            case FieldType.String:
                var stringFormat = field.Format switch
                {
                    "Email" => StringFormatName.Email,
                    "Phone" => StringFormatName.Phone,
                    _ => StringFormatName.Text,
                };
                return new StringAttributeMetadata
                {
                    SchemaName = field.SchemaName,
                    DisplayName = Lbl(field.DisplayName),
                    Description = descriptionLabel,
                    RequiredLevel = Req(field.Required),
                    MaxLength = field.MaxLength ?? 100,
                    FormatName = stringFormat,
                };

            case FieldType.Memo:
                return new MemoAttributeMetadata
                {
                    SchemaName = field.SchemaName,
                    DisplayName = Lbl(field.DisplayName),
                    Description = descriptionLabel,
                    RequiredLevel = Req(field.Required),
                    MaxLength = field.MaxLength ?? 2000,
                };

            case FieldType.DateTime:
                var dtFormat = DataModel.DateOnlyDisplayNames.Contains(field.DisplayName)
                    ? DateTimeFormat.DateOnly
                    : DateTimeFormat.DateAndTime;
                return new DateTimeAttributeMetadata
                {
                    SchemaName = field.SchemaName,
                    DisplayName = Lbl(field.DisplayName),
                    Description = descriptionLabel,
                    RequiredLevel = Req(field.Required),
                    Format = dtFormat,
                };

            case FieldType.Boolean:
                return new BooleanAttributeMetadata
                {
                    SchemaName = field.SchemaName,
                    DisplayName = Lbl(field.DisplayName),
                    Description = descriptionLabel,
                    RequiredLevel = Req(field.Required),
                    OptionSet = new BooleanOptionSetMetadata(
                        new OptionMetadata(Lbl("Yes"), 1),
                        new OptionMetadata(Lbl("No"), 0)),
                };

            case FieldType.Decimal:
                return new DecimalAttributeMetadata
                {
                    SchemaName = field.SchemaName,
                    DisplayName = Lbl(field.DisplayName),
                    Description = descriptionLabel,
                    RequiredLevel = Req(field.Required),
                    Precision = field.Precision ?? 2,
                    MinValue = (decimal)(field.MinValue ?? 0.0),
                    MaxValue = (decimal)(field.MaxValue ?? 100.0),
                };

            case FieldType.Integer:
                return new IntegerAttributeMetadata
                {
                    SchemaName = field.SchemaName,
                    DisplayName = Lbl(field.DisplayName),
                    Description = descriptionLabel,
                    RequiredLevel = Req(field.Required),
                    Format = IntegerFormat.None,
                    MinValue = field.MinValueInt ?? 0,
                    MaxValue = field.MaxValueInt ?? 100,
                };

            case FieldType.Picklist:
                var optionSetMetadata = new OptionSetMetadata
                {
                    IsGlobal = false,
                    OptionSetType = OptionSetType.Picklist,
                };
                var index = 1;
                foreach (var label in field.OptionSet ?? Array.Empty<string>())
                {
                    optionSetMetadata.Options.Add(new OptionMetadata(Lbl(label), index * 100000000));
                    index++;
                }
                return new PicklistAttributeMetadata
                {
                    SchemaName = field.SchemaName,
                    DisplayName = Lbl(field.DisplayName),
                    Description = descriptionLabel,
                    RequiredLevel = Req(field.Required),
                    OptionSet = optionSetMetadata,
                };

            default:
                throw new InvalidOperationException($"BuildAttributeMetadata does not handle {field.Type}; lookups go through CreateLookupRelationship instead.");
        }
    }

    public static void CreateLookupRelationship(IOrganizationService svc, string entityLogicalName, FieldDefinition field)
    {
        var target = field.TargetEntityLogicalName
            ?? throw new InvalidOperationException($"Lookup field {field.SchemaName} has no TargetEntityLogicalName.");

        var relationshipSchemaName = $"{target}_{entityLogicalName}_{field.SchemaName}";
        if (relationshipSchemaName.Length > 100)
        {
            relationshipSchemaName = relationshipSchemaName[..100];
        }

        var request = new CreateOneToManyRequest
        {
            OneToManyRelationship = new OneToManyRelationshipMetadata
            {
                SchemaName = relationshipSchemaName,
                ReferencedEntity = target,
                ReferencingEntity = entityLogicalName,
            },
            Lookup = new LookupAttributeMetadata
            {
                SchemaName = field.SchemaName,
                DisplayName = Lbl(field.DisplayName),
                Description = Lbl($"Lookup to {target}"),
                RequiredLevel = Req(field.Required),
            },
        };

        svc.Execute(request);
        Log.Info($"Created lookup relationship: {entityLogicalName}.{field.SchemaName} -> {target}");
    }

    public static void EnsureField(IOrganizationService svc, string entityLogicalName, FieldDefinition field, IReadOnlySet<string> availableTargets, IReadOnlySet<string> customTableLogicalNames)
    {
        if (field.Type == FieldType.Lookup)
        {
            var target = field.TargetEntityLogicalName!;
            var isAvailable = availableTargets.Contains(target) || customTableLogicalNames.Contains(target);
            if (!isAvailable)
            {
                Log.Info($"Skipping lookup field {entityLogicalName}.{field.SchemaName}: target table '{target}' is not present in this environment.");
                return;
            }
        }

        if (AttributeExists(svc, entityLogicalName, field.SchemaName))
        {
            Log.Info($"Field already exists: {entityLogicalName}.{field.SchemaName}");
            return;
        }

        Log.Info($"Creating field: {entityLogicalName}.{field.SchemaName} (Type={field.Type})");

        if (field.Type == FieldType.Lookup)
        {
            CreateLookupRelationship(svc, entityLogicalName, field);
            return;
        }

        var attributeMetadata = BuildAttributeMetadata(field, entityLogicalName);
        svc.Execute(new CreateAttributeRequest
        {
            EntityName = entityLogicalName,
            Attribute = attributeMetadata,
        });
        Log.Info($"Created field: {entityLogicalName}.{field.SchemaName}");
    }

    public static void EnsureFields(IOrganizationService svc, TableDefinition table, IReadOnlySet<string> availableTargets, IReadOnlySet<string> customTableLogicalNames)
    {
        foreach (var field in table.Fields)
        {
            if (field.IsPrimaryName)
            {
                // Created as part of entity creation (CreateEntityRequest.PrimaryAttribute); skip here.
                continue;
            }
            EnsureField(svc, table.LogicalName, field, availableTargets, customTableLogicalNames);
        }
    }

    public static string GetSolutionUniqueName(IOrganizationService svc, string requiredName)
    {
        var query = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("uniquename", "friendlyname"),
            Criteria = new FilterExpression(LogicalOperator.Or),
        };
        query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, requiredName);
        query.Criteria.AddCondition("friendlyname", ConditionOperator.Equal, requiredName);

        var results = svc.RetrieveMultiple(query);
        foreach (var entity in results.Entities)
        {
            var uniqueName = entity.GetAttributeValue<string>("uniquename");
            var friendlyName = entity.GetAttributeValue<string>("friendlyname");
            if (uniqueName == requiredName || friendlyName == requiredName)
            {
                return uniqueName!;
            }
        }

        throw new InvalidOperationException($"Solution '{requiredName}' was not found in the active Dataverse environment.");
    }

    public static void AddTableToSolution(IOrganizationService svc, string solutionUniqueName, string logicalName)
    {
        if (!TryGetEntityMetadata(svc, logicalName, out var metadata) || metadata?.MetadataId is null)
        {
            throw new InvalidOperationException($"Unable to add {logicalName} to solution because it does not exist.");
        }

        var request = new AddSolutionComponentRequest
        {
            ComponentType = 1, // Entity
            ComponentId = metadata.MetadataId.Value,
            SolutionUniqueName = solutionUniqueName,
            AddRequiredComponents = false,
        };

        try
        {
            svc.Execute(request);
            Log.Info($"Associated table with solution: {logicalName}");
        }
        catch (Exception ex) when (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
        {
            Log.Info($"Table already associated with solution: {logicalName}");
        }
    }

    public static void PublishAll(IOrganizationService svc)
    {
        svc.Execute(new PublishAllXmlRequest());
        Log.Info("Metadata publish requested successfully.");
    }
}

public static class Log
{
    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message) => Console.WriteLine($"[{level}] {message}");
}