namespace Fabric_backup_lite.Models;

public enum FabricItemType
{
    Report,
    SemanticModel,
    Notebook,
    DataPipeline,
    Dataflow,
    Lakehouse,
    Warehouse,
    KQLDatabase,
    Eventhouse,
    Environment,
    SparkJobDefinition,
    // Added in v2.2 — all support the definition API (getDefinition + create-with-definition).
    // Enum member names MUST match the exact Fabric API type string so Enum.TryParse and
    // ToString() round-trip against the REST `type` field.
    Eventstream,
    Reflex,             // "Data Activator"
    VariableLibrary,
    MirroredDatabase,
    KQLDashboard,
    KQLQueryset,
    GraphQLApi,
    CopyJob,
    PaginatedReport,
    Unknown
}
