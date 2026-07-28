namespace Reimaginate.DataHub.Auth;


public static class DataHubPermissions
{
    public static readonly string BulkDeleteJobs = "bulkdelete:jobs";
    public static readonly string BulkDeleteDuplicates = "bulkdelete:duplicatemergejobs";
    public static readonly string DeleteAlerts = "delete:alerts";
    public static readonly string DeleteEntities = "delete:entities";
    public static readonly string DeleteJobs = "delete:jobs";
    public static readonly string DeleteDuplicates = "delete:duplicatemergejobs";
    public static readonly string DeleteLogEntries = "delete:logentries";
    public static readonly string DeleteRoles = "delete:roles";
    public static readonly string DeleteSourceEntities = "delete:sourceentities";
    public static readonly string DeleteSyncMarkers = "delete:syncmarkers";
    public static readonly string DeleteTrackingData = "delete:trackingdata";
    public static readonly string DetachEntities = "detach:entities";
    public static readonly string DisableUsers = "disable:users";
    public static readonly string EnableUsers = "enable:users";
    public static readonly string ImportEntities = "patch:entities";
    public static readonly string PatchEntities = "patch:entities";
    public static readonly string QueryAlerts = "query:entities";
    public static readonly string ReadDuplicates = "query:duplicatemergejobs";
    public static readonly string QueryEntities = "query:entities";
    public static readonly string QueryDiagnostics = "query:diagnostics";
    public static readonly string QueryJobs = "query:jobs";
    public static readonly string QueryProcessingLocks = "query:processinglocks";
    public static readonly string QueryPermissions = "query:permissions";
    public static readonly string QueryRoles = "query:roles";
    public static readonly string QuerySyncFailures = "query:syncfailures";
    public static readonly string QuerySyncMarkers = "query:syncmarkers";
    public static readonly string QueryTrackingData = "query:trackingdata";
    public static readonly string QueryUsers = "query:users";
    public static readonly string RebaseTrackingData = "rebase:trackingdata";
    public static readonly string RegisterAlternateKeys = "register:alternatekeys";
    public static readonly string RegisterRoles = "register:roles";
    public static readonly string RegisterUsers = "register:users";
    public static readonly string SubmitAgentJobs = "submit:agentjobs";
    public static readonly string UpdateRoles = "update:roles";
    public static readonly string UpdateSyncMarkers = "update:syncmarkers";
    public static readonly string UpdateUsers = "update:users";
}
