namespace Fabric_backup_lite.Models;

/// <summary>
/// Single source of truth for per-item-type presentation and storage metadata:
/// icon, plural display name, and backup sub-folder. Centralizing this here means
/// adding a new Fabric item type only requires editing the <see cref="FabricItemType"/>
/// enum and (optionally) the entries below — not six scattered switch statements.
/// Any type not listed falls back to a generic icon / its enum name, so newly added
/// types still render and back up gracefully.
/// </summary>
public static class FabricItemCatalog
{
    public static string Icon(FabricItemType type) => type switch
    {
        FabricItemType.Report             => "📈",
        FabricItemType.SemanticModel      => "🔷",
        FabricItemType.Notebook           => "📓",
        FabricItemType.DataPipeline       => "🔄",
        FabricItemType.Dataflow           => "💧",
        FabricItemType.Lakehouse          => "🏠",
        FabricItemType.Warehouse          => "🏭",
        FabricItemType.KQLDatabase        => "🔍",
        FabricItemType.Eventhouse         => "⚡",
        FabricItemType.Environment        => "🌐",
        FabricItemType.SparkJobDefinition => "✨",
        FabricItemType.Eventstream        => "🌊",
        FabricItemType.Reflex             => "🔔",
        FabricItemType.VariableLibrary    => "📦",
        FabricItemType.MirroredDatabase   => "🪞",
        FabricItemType.KQLDashboard       => "📊",
        FabricItemType.KQLQueryset        => "🔎",
        FabricItemType.GraphQLApi         => "🕸️",
        FabricItemType.CopyJob            => "📋",
        FabricItemType.PaginatedReport    => "📃",
        _                                 => "📄"
    };

    public static string PluralName(FabricItemType type) => type switch
    {
        FabricItemType.Report             => "Reports",
        FabricItemType.SemanticModel      => "Semantic Models",
        FabricItemType.Notebook           => "Notebooks",
        FabricItemType.DataPipeline       => "Data Pipelines",
        FabricItemType.Dataflow           => "Dataflows",
        FabricItemType.Lakehouse          => "Lakehouses",
        FabricItemType.Warehouse          => "Warehouses",
        FabricItemType.KQLDatabase        => "KQL Databases",
        FabricItemType.Eventhouse         => "Eventhouses",
        FabricItemType.Environment        => "Environments",
        FabricItemType.SparkJobDefinition => "Spark Job Definitions",
        FabricItemType.Eventstream        => "Eventstreams",
        FabricItemType.Reflex             => "Data Activators",
        FabricItemType.VariableLibrary    => "Variable Libraries",
        FabricItemType.MirroredDatabase   => "Mirrored Databases",
        FabricItemType.KQLDashboard       => "KQL Dashboards",
        FabricItemType.KQLQueryset        => "KQL Querysets",
        FabricItemType.GraphQLApi         => "GraphQL APIs",
        FabricItemType.CopyJob            => "Copy Jobs",
        FabricItemType.PaginatedReport    => "Paginated Reports",
        _                                 => type.ToString()
    };

    /// <summary>Backup sub-folder for the type. New types default to their enum name.</summary>
    public static string SubFolder(FabricItemType type) => type switch
    {
        FabricItemType.Report             => "Reports",
        FabricItemType.SemanticModel      => "SemanticModels",
        FabricItemType.Notebook           => "Notebooks",
        FabricItemType.DataPipeline       => "Pipelines",
        FabricItemType.Dataflow           => "Dataflows",
        FabricItemType.Lakehouse          => "Lakehouses",
        FabricItemType.Warehouse          => "Warehouses",
        FabricItemType.KQLDatabase        => "KQLDatabases",
        FabricItemType.Eventhouse         => "Eventhouses",
        FabricItemType.Environment        => "Environments",
        FabricItemType.SparkJobDefinition => "SparkJobDefinitions",
        FabricItemType.Eventstream        => "Eventstreams",
        FabricItemType.Reflex             => "Reflexes",
        FabricItemType.VariableLibrary    => "VariableLibraries",
        FabricItemType.MirroredDatabase   => "MirroredDatabases",
        FabricItemType.KQLDashboard       => "KQLDashboards",
        FabricItemType.KQLQueryset        => "KQLQuerysets",
        FabricItemType.GraphQLApi         => "GraphQLApis",
        FabricItemType.CopyJob            => "CopyJobs",
        FabricItemType.PaginatedReport    => "PaginatedReports",
        FabricItemType.Unknown            => "Other",
        _                                 => type.ToString()
    };

    /// <summary>
    /// Item types that expose NO definition API and cannot be exported via getDefinition.
    /// Warehouse is handled separately through OneLake; the rest are simply skipped.
    /// </summary>
    public static bool HasDefinitionApi(FabricItemType type) => type switch
    {
        FabricItemType.Warehouse => false,
        FabricItemType.Unknown   => false,
        _                        => true
    };
}
