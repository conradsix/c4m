#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Logging;

internal static partial class CmsEngineLoggerMessages
{
    private const int EventIdStart = 11000;

    // Pages (11000-11019)
    [LoggerMessage(
        EventIdStart + 0,
        LogLevel.Information,
        "CreatePage completed: Page {PageId} created with path {Path} in {ElapsedTime}ms")]
    public static partial void CreatePageEndpointCompleted(
        this ILogger logger,
        Guid pageId,
        string path,
        long elapsedTime);

    [LoggerMessage(
        EventIdStart + 1,
        LogLevel.Warning,
        "CreatePage: Conflict creating page with path {Path}")]
    public static partial void CreatePageEndpointConflict(
        this ILogger logger,
        string path);

    [LoggerMessage(
        EventIdStart + 2,
        LogLevel.Information,
        "GetPage completed for page {PageId} in {ElapsedTime}ms")]
    public static partial void GetPageEndpointCompleted(
        this ILogger logger,
        Guid pageId,
        long elapsedTime);

    [LoggerMessage(
        EventIdStart + 3,
        LogLevel.Warning,
        "GetPage: Page {PageId} not found")]
    public static partial void GetPageEndpointNotFound(
        this ILogger logger,
        Guid pageId);

    [LoggerMessage(
        EventIdStart + 4,
        LogLevel.Information,
        "GetPageByPath completed for path {Path} in {ElapsedTime}ms")]
    public static partial void GetPageByPathCompleted(
        this ILogger logger,
        string path,
        long elapsedTime);

    [LoggerMessage(
        EventIdStart + 5,
        LogLevel.Warning,
        "GetPageByPath: Page with path {Path} not found")]
    public static partial void GetPageByPathNotFound(
        this ILogger logger,
        string path);

    [LoggerMessage(
        EventIdStart + 6,
        LogLevel.Information,
        "UpdatePage completed for page {PageId} in {ElapsedTime}ms")]
    public static partial void UpdatePageEndpointCompleted(
        this ILogger logger,
        Guid pageId,
        long elapsedTime);

    [LoggerMessage(
        EventIdStart + 7,
        LogLevel.Information,
        "ListPages completed in {ElapsedTime}ms: {Count} pages returned")]
    public static partial void ListPagesCompleted(
        this ILogger logger,
        long elapsedTime,
        int count);

    [LoggerMessage(
        EventIdStart + 8,
        LogLevel.Information,
        "GetPagePathAvailability completed for path {Path} with status {Status} in {ElapsedMs:D} ms")]
    public static partial void GetPagePathAvailabilityCompleted(
        this ILogger logger,
        string path,
        string status,
        long elapsedMs);

    [LoggerMessage(
        EventIdStart + 9,
        LogLevel.Information,
        "UnpublishPage completed for page {PageId} in {ElapsedMs:D} ms")]
    public static partial void UnpublishPageEnd(this ILogger logger, Guid pageId, long elapsedMs);

    [LoggerMessage(
        EventIdStart + 10,
        LogLevel.Information,
        "ArchivePage completed for page {PageId} in {ElapsedMs:D} ms")]
    public static partial void ArchivePageEnd(this ILogger logger, Guid pageId, long elapsedMs);

    [LoggerMessage(
        EventIdStart + 11,
        LogLevel.Information,
        "ReparentPageChildren moved {Count} children of page {PageId} in {ElapsedMs:D} ms")]
    public static partial void ReparentPageChildrenEnd(this ILogger logger, Guid pageId, int count, long elapsedMs);

    [LoggerMessage(
        EventIdStart + 12,
        LogLevel.Information,
        "Page details saved for {PageId} in {ElapsedMs:D} ms")]
    public static partial void UpdatePageDetailsEnd(this ILogger logger, Guid pageId, long elapsedMs);

    // Block types and menu action types (11020-11029)
    [LoggerMessage(
        EventIdStart + 20,
        LogLevel.Information,
        "GetBlockType completed for block type {BlockTypeId} in {ElapsedTime}ms")]
    public static partial void GetBlockTypeCompleted(
        this ILogger logger,
        Guid blockTypeId,
        long elapsedTime);

    [LoggerMessage(
        EventIdStart + 21,
        LogLevel.Warning,
        "GetBlockType: BlockType {BlockTypeId} not found")]
    public static partial void GetBlockTypeNotFound(
        this ILogger logger,
        Guid blockTypeId);

    [LoggerMessage(
        EventIdStart + 22,
        LogLevel.Warning,
        "Create {TypeName} conflict for name {Name}")]
    public static partial void CreateCmsTypeDescriptorConflict(
        this ILogger logger,
        string typeName,
        string name);

    [LoggerMessage(
        EventIdStart + 23,
        LogLevel.Information,
        "Create {TypeName} completed for {Id} with name {Name} in {ElapsedMs:D} ms")]
    public static partial void CreateCmsTypeDescriptorCompleted(
        this ILogger logger,
        string typeName,
        Guid id,
        string name,
        long elapsedMs);

    [LoggerMessage(
        EventIdStart + 24,
        LogLevel.Information,
        "GetMenuActionType started for action type {ActionTypeId}")]
    public static partial void GetMenuActionTypeStart(
        this ILogger logger,
        Guid actionTypeId);

    [LoggerMessage(
        EventIdStart + 25,
        LogLevel.Information,
        "GetMenuActionType completed for action type {ActionTypeId} in {ElapsedMs:D} ms")]
    public static partial void GetMenuActionTypeEnd(
        this ILogger logger,
        Guid actionTypeId,
        long elapsedMs);

    [LoggerMessage(
        EventIdStart + 26,
        LogLevel.Information,
        "ListMenuActionTypes started")]
    public static partial void ListMenuActionTypesStart(
        this ILogger logger);

    [LoggerMessage(
        EventIdStart + 27,
        LogLevel.Information,
        "ListMenuActionTypes completed: {Count} action types returned in {ElapsedMs:D} ms")]
    public static partial void ListMenuActionTypesEnd(
        this ILogger logger,
        int count,
        long elapsedMs);

    // Menus (11030-11049)
    [LoggerMessage(
        EventIdStart + 30,
        LogLevel.Information,
        "GetMenu started for menu {MenuId}")]
    public static partial void GetMenuStart(
        this ILogger logger,
        Guid menuId);

    [LoggerMessage(
        EventIdStart + 31,
        LogLevel.Information,
        "GetMenu completed for menu {MenuId} in {ElapsedMs:D} ms")]
    public static partial void GetMenuEnd(
        this ILogger logger,
        Guid menuId,
        long elapsedMs);

    [LoggerMessage(
        EventIdStart + 32,
        LogLevel.Information,
        "CreateMenu completed for menu {MenuId} with name {Name} in {ElapsedMs:D} ms")]
    public static partial void CreateMenuCompleted(
        this ILogger logger,
        Guid menuId,
        string name,
        long elapsedMs);

    [LoggerMessage(
        EventIdStart + 33,
        LogLevel.Warning,
        "CreateMenu conflict for name {Name}")]
    public static partial void CreateMenuConflict(
        this ILogger logger,
        string name);

    [LoggerMessage(
        EventIdStart + 34,
        LogLevel.Error,
        "CreateMenu failed for name {Name}: {Error}")]
    public static partial void CreateMenuFailed(
        this ILogger logger,
        string name,
        string error);

    [LoggerMessage(
        EventIdStart + 35,
        LogLevel.Information,
        "UpdateMenu completed for menu {MenuId} in {ElapsedMs:D} ms")]
    public static partial void UpdateMenuCompleted(
        this ILogger logger,
        Guid menuId,
        long elapsedMs);

    [LoggerMessage(
        EventIdStart + 36,
        LogLevel.Warning,
        "UpdateMenu: Menu {MenuId} not found")]
    public static partial void UpdateMenuNotFound(
        this ILogger logger,
        Guid menuId);

    [LoggerMessage(
        EventIdStart + 37,
        LogLevel.Error,
        "UpdateMenu failed for menu {MenuId}: {Error}")]
    public static partial void UpdateMenuFailed(
        this ILogger logger,
        Guid menuId,
        string error);

    [LoggerMessage(
        EventIdStart + 38,
        LogLevel.Information,
        "ListMenus started with limit {Limit} and offset {Offset}")]
    public static partial void ListMenusStart(
        this ILogger logger,
        int limit,
        int offset);

    [LoggerMessage(
        EventIdStart + 39,
        LogLevel.Information,
        "ListMenus completed: {Count} menus returned in {ElapsedMs:D} ms")]
    public static partial void ListMenusEnd(
        this ILogger logger,
        int count,
        long elapsedMs);

    // Endpoints (11050-11059)
    [LoggerMessage(
        EventIdStart + 50,
        LogLevel.Error,
        "Endpoint error in {EndpointName}")]
    public static partial void EndpointError(
        this ILogger logger,
        Exception exception,
        string endpointName);

    // Schema (11060-11069)
    [LoggerMessage(
        EventIdStart + 60,
        LogLevel.Information,
        "Migrating the CMS engine schema; pending migrations: {Migrations}")]
    public static partial void CmsEngineMigrating(
        this ILogger logger,
        string[] migrations);
}
