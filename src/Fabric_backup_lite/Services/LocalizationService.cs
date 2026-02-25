using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Fabric_backup_lite.Services;

/// <summary>
/// Singleton localization service. Exposes all UI strings as properties.
/// Raises PropertyChanged("") when language switches so WPF bindings refresh automatically.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public static readonly LocalizationService Instance = new();
    private LocalizationService() { }

    private string _language = "es";

    public string Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            OnPropertyChanged(string.Empty); // refresh all bindings
        }
    }

    public bool IsSpanish => _language == "es";

    public void ToggleLanguage() => Language = IsSpanish ? "en" : "es";

    // ---- App tagline (header) ----
    public string AppTagline => IsSpanish
        ? "Software libre desarrollado por Walter Calcagno Lucares, Microsoft MVP Data Platform en Santiago de Chile — MMXXVI"
        : "Free software developed by Walter Calcagno Lucares, Microsoft MVP Data Platform in Santiago, Chile — MMXXVI";

    // ---- Language selector ----
    public string LanguageButtonText => IsSpanish ? "EN" : "ES";

    // ---- Left panel ----
    public string ExplorerHeader   => IsSpanish ? "EXPLORADOR" : "EXPLORER";
    public string SelectAllButton  => IsSpanish ? "✓ Todo"     : "✓ All";
    public string SelectNoneButton => IsSpanish ? "✗ Ninguno"  : "✗ None";

    // ---- Header ----
    public string SignInButton => IsSpanish ? "Iniciar sesión" : "Sign In";

    // ---- Destination ----
    public string DestinationFolderLabel => IsSpanish ? "Carpeta de destino" : "Destination folder";
    public string BrowseButton           => IsSpanish ? "Examinar..."        : "Browse...";

    // ---- Activity log ----
    public string ActivityLogLabel => IsSpanish ? "Log de actividad" : "Activity log";

    // ---- Action buttons ----
    public string StartBackupButton => IsSpanish ? "▶  Iniciar Backup" : "▶  Start Backup";
    public string CancelButton      => IsSpanish ? "✕  Cancelar"       : "✕  Cancel";

    // ---- Beta warning ----
    public string BetaWarningHeader => IsSpanish
        ? "⚠  Versión en desarrollo — leer antes de usar"
        : "⚠  Development version — read before using";

    public string BetaWarningText1 => IsSpanish
        ? "Este software permite realizar backup y restauración de distintos componentes de Microsoft Fabric y Power BI.\n\n" +
          "Se encuentra en fase de desarrollo, por lo que puede presentar errores o comportamientos no esperados. " +
          "Ha sido construido con dedicación para aportar valor a la comunidad y su uso es completamente libre.\n\n" +
          "Si detectas algún bug o inconveniente, por favor repórtalo a "
        : "This software allows you to back up and restore various Microsoft Fabric and Power BI components.\n\n" +
          "It is currently in development, so it may contain bugs or unexpected behavior. " +
          "It has been built with dedication to provide value to the community and its use is completely free.\n\n" +
          "If you find a bug or issue, please report it to ";

    public string BetaWarningText2 => IsSpanish
        ? " para poder corregirlo en futuras versiones.\n\nGracias por colaborar en su mejora continua.\n\n" +
          "Software libre desarrollado por Walter Calcagno Lucares, Microsoft MVP Data Platform en Santiago de Chile — MMXXVI"
        : " so it can be fixed in future versions.\n\nThank you for contributing to its continuous improvement.\n\n" +
          "Free software developed by Walter Calcagno Lucares, Microsoft MVP Data Platform in Santiago, Chile — MMXXVI";

    // ---- ViewModel: authentication ----
    public string NotAuthenticated    => IsSpanish ? "No autenticado"                   : "Not authenticated";
    public string UnknownUser         => IsSpanish ? "Usuario desconocido"              : "Unknown user";
    public string SigningIn           => IsSpanish ? "Iniciando sesión..."               : "Signing in...";
    public string SessionStartedAsFmt => IsSpanish ? "Sesión iniciada como {0}"         : "Signed in as {0}";
    public string LoadingWorkspaces   => IsSpanish ? "Cargando workspaces..."           : "Loading workspaces...";
    public string FoundWorkspacesFmt  => IsSpanish ? "Se encontraron {0} workspace(s)" : "Found {0} workspace(s)";
    public string AuthenticatedFmt    => IsSpanish ? "Autenticado — {0} workspace(s) disponibles" : "Authenticated — {0} workspace(s) available";
    public string SignInErrorStatus   => IsSpanish ? "Error al iniciar sesión"          : "Sign in error";
    public string CouldNotSignInFmt   => IsSpanish ? "No se pudo iniciar sesión: {0}"  : "Couldn't sign in: {0}";

    // ---- ViewModel: selection info ----
    public string ZeroItemsSelected      => IsSpanish ? "0 ítems seleccionados"              : "0 items selected";
    public string ItemsSelectedSingleFmt => IsSpanish ? "{0} ítem(s) seleccionado(s)"        : "{0} item(s) selected";
    public string ItemsSelectedMultiFmt  => IsSpanish ? "{0} ítem(s) en {1} workspace(s)"    : "{0} item(s) in {1} workspace(s)";
    public string NoItems                => IsSpanish ? "Sin elementos"                       : "No items";
    public string LoadedItemsFmt         => IsSpanish ? "Cargados {0} ítem(s) en '{1}'"     : "Loaded {0} item(s) in '{1}'";
    public string ErrorFmt               => IsSpanish ? "Error: {0}"                         : "Error: {0}";

    // ---- ViewModel: status ----
    public string Ready => IsSpanish ? "Listo" : "Ready";

    // ---- ViewModel: destination ----
    public string SelectDestinationTitle      => IsSpanish ? "Selecciona la carpeta de destino"  : "Select the destination folder";
    public string DestinationFmt              => IsSpanish ? "Destino: {0}"                      : "Destination: {0}";
    public string DestinationFolderSelectedSt => IsSpanish ? "Carpeta de destino seleccionada"   : "Destination folder selected";

    // ---- ViewModel: backup start ----
    public string SelectAtLeastOneMsg  => IsSpanish
        ? "Selecciona al menos un ítem en el árbol de workspaces para hacer backup."
        : "Select at least one item in the workspace tree to backup.";
    public string NoItemsSelectedTitle => IsSpanish ? "Sin ítems seleccionados"           : "No items selected";
    public string StartingBackupFmt    => IsSpanish ? "Iniciando backup de {0} ítem(s)..." : "Starting backup of {0} item(s)...";
    public string BackupInProgress     => IsSpanish ? "Backup en progreso..."              : "Backup in progress...";

    // ---- ViewModel: backup success ----
    public string BackupCompletedLog    => IsSpanish ? "✓ Backup completado exitosamente!"  : "✓ Backup completed successfully!";
    public string ItemsSavedFmt         => IsSpanish ? "  Ítems guardados: {0}"            : "  Items saved: {0}";
    public string LocationFmt           => IsSpanish ? "  Ubicación: {0}"                  : "  Location: {0}";
    public string BackupCompletedStatus => IsSpanish ? "Backup completado"                  : "Backup completed";
    public string BackupCompletedMsgFmt => IsSpanish
        ? "Backup completado exitosamente!\n\nÍtems guardados: {0}\nUbicación: {1}"
        : "Backup completed successfully!\n\nItems saved: {0}\nLocation: {1}";
    public string BackupCompleteTitle   => IsSpanish ? "Backup completo"  : "Backup complete";

    // ---- ViewModel: backup with errors ----
    public string BackupWithErrorsLog        => IsSpanish ? "⚠ Backup completado con errores:" : "⚠ Backup completed with errors:";
    public string BackupWithErrorsStatusFmt  => IsSpanish ? "Backup con {0} error(es)"         : "Backup with {0} error(s)";
    public string BackupWithErrorsMsgFmt     => IsSpanish
        ? "Backup completado con errores.\n\nÍtems guardados: {0}\nErrores: {1}\n\nRevisa el log para más detalles."
        : "Backup completed with errors.\n\nItems saved: {0}\nErrors: {1}\n\nCheck the log for more details.";
    public string BackupWithErrorsTitle      => IsSpanish ? "Backup con errores" : "Backup with errors";

    // ---- ViewModel: backup cancelled ----
    public string BackupCancelledLog    => IsSpanish ? "Backup cancelado por el usuario" : "Backup cancelled by user";
    public string BackupCancelledStatus => IsSpanish ? "Backup cancelado"               : "Backup cancelled";
    public string BackupCancelledMsg    => IsSpanish ? "El backup fue cancelado."        : "The backup was cancelled.";
    public string BackupCancelledTitle  => IsSpanish ? "Cancelado"                       : "Cancelled";
    public string CancellingBackupLog   => IsSpanish ? "Cancelando backup..."           : "Cancelling backup...";
    public string CancellingStatus      => IsSpanish ? "Cancelando..."                  : "Cancelling...";

    // ---- ViewModel: backup error ----
    public string BackupErrorFmt    => IsSpanish ? "✗ Error en el backup: {0}" : "✗ Backup error: {0}";
    public string BackupErrorStatus => IsSpanish ? "Error en el backup"        : "Backup error";
    public string BackupErrorMsgFmt => IsSpanish ? "Error en el backup: {0}"  : "Backup error: {0}";
    public string BackupErrorTitle  => IsSpanish ? "Error"                     : "Error";

    // ---- Settings window ----
    public string SettingsTitle       => IsSpanish ? "Configuración"                   : "Settings";
    public string SettingsSubtitle    => IsSpanish ? "Microsoft Fabric Backup Lite"    : "Microsoft Fabric Backup Lite";
    public string SettingsAuthSection => IsSpanish ? "Autenticación Azure AD"          : "Azure AD Authentication";
    public string ClientIdLabel       => IsSpanish ? "Client ID (App ID):"             : "Client ID (App ID):";
    public string TenantIdLabel       => IsSpanish ? "Tenant ID:"                      : "Tenant ID:";
    public string SaveSettingsButton  => IsSpanish ? "Guardar"                         : "Save";
    public string CloseSettingsButton => IsSpanish ? "Cerrar"                          : "Close";
    public string SettingsHint        => IsSpanish
        ? "Los cambios se guardan en AppData\\WeData\\FabricBackupLite\\usersettings.json y se aplican al reiniciar la aplicación."
        : "Changes are saved to AppData\\WeData\\FabricBackupLite\\usersettings.json and apply on next application restart.";
    public string SettingsSavedMsg    => IsSpanish
        ? "Configuración guardada correctamente.\n\nReinicie la aplicación para aplicar los cambios."
        : "Settings saved successfully.\n\nPlease restart the application to apply the changes.";
    public string SettingsSavedTitle  => IsSpanish ? "Guardado"                        : "Saved";

    // ---- Tab headers ----
    public string BackupTabHeader  => IsSpanish ? "💾  Backup"  : "💾  Backup";
    public string RestoreTabHeader => IsSpanish ? "↩  Restaurar" : "↩  Restore";

    // ---- Restore — left panel ----
    public string RestoreExplorerHeader    => IsSpanish ? "ÍTEMS A RESTAURAR"                 : "ITEMS TO RESTORE";
    public string RestoreZeroSelected      => IsSpanish ? "0 ítems seleccionados"              : "0 items selected";
    public string RestoreItemsSelectedFmt  => IsSpanish ? "{0} ítem(s) seleccionado(s)"        : "{0} item(s) selected";

    // ---- Restore — source folder ----
    public string SourceFolderLabel       => IsSpanish ? "Carpeta raíz de backups"            : "Backup root folder";
    public string DiscoveredBackupsLabel  => IsSpanish ? "Versión de backup"                  : "Backup version";
    public string NoBackupsFound          => IsSpanish ? "No se encontraron backups en la carpeta seleccionada." : "No backups found in the selected folder.";
    public string SelectSourceTitle       => IsSpanish ? "Seleccionar carpeta raíz de backups" : "Select backup root folder";

    // ---- Restore — destination workspace ----
    public string DestWorkspaceLabel      => IsSpanish ? "Área de trabajo destino"            : "Destination workspace";
    public string NewWorkspaceOption      => IsSpanish ? "＋  Nuevo workspace"                : "＋  New workspace";
    public string NewWorkspaceNameLabel   => IsSpanish ? "Nombre del nuevo workspace"         : "New workspace name";
    public string CapacityLabel           => IsSpanish ? "Capacidad Fabric"                   : "Fabric capacity";
    public string LoadingCapacities       => IsSpanish ? "Cargando capacidades..."            : "Loading capacities...";
    public string NoCapacitiesFound       => IsSpanish ? "No se encontraron capacidades."     : "No capacities found.";

    // ---- Restore — action button ----
    public string StartRestoreButton      => IsSpanish ? "▶  Iniciar Restore"                 : "▶  Start Restore";

    // ---- Restore — tooltip ----
    public string WarehouseRestoreTooltip => IsSpanish
        ? "Los Warehouses no pueden restaurarse mediante API. Debe recrearse manualmente."
        : "Warehouses cannot be restored via API. Must be recreated manually.";

    // ---- Restore — status & log messages ----
    public string RestoreReady              => IsSpanish ? "Listo para restaurar."             : "Ready to restore.";
    public string RestoreInProgress         => IsSpanish ? "Restauración en progreso..."       : "Restore in progress...";
    public string RestoringItemFmt          => IsSpanish ? "Restaurando {0}: {1}"              : "Restoring {0}: {1}";
    public string RestoreCompletedStatus    => IsSpanish ? "Restauración completada."          : "Restore completed.";
    public string RestoreCompletedLog       => IsSpanish ? "✅ Restauración completada."       : "✅ Restore completed.";
    public string ItemsRestoredFmt          => IsSpanish ? "{0} ítem(s) restaurado(s)."        : "{0} item(s) restored.";
    public string RestoreCompletedMsgFmt    => IsSpanish
        ? "Se restauraron {0} ítem(s) correctamente en el workspace '{1}'."
        : "{0} item(s) restored successfully to workspace '{1}'.";
    public string RestoreCompleteTitle      => IsSpanish ? "Restauración completada"           : "Restore Complete";
    public string RestoreWithErrorsLog      => IsSpanish ? "⚠️ Restauración con errores."      : "⚠️ Restore completed with errors.";
    public string RestoreWithErrorsStatusFmt => IsSpanish ? "Restauración con {0} error(es)."  : "Restore completed with {0} error(s).";
    public string RestoreWithErrorsMsgFmt   => IsSpanish
        ? "Se restauraron {0} ítem(s), pero {1} error(es) ocurrieron."
        : "{0} item(s) restored, but {1} error(s) occurred.";
    public string RestoreWithErrorsTitle    => IsSpanish ? "Restauración con errores"          : "Restore with Errors";
    public string RestoreCancelledLog       => IsSpanish ? "🚫 Restauración cancelada."        : "🚫 Restore cancelled.";
    public string RestoreCancelledStatus    => IsSpanish ? "Restauración cancelada."           : "Restore cancelled.";
    public string RestoreCancelledMsg       => IsSpanish ? "La restauración fue cancelada por el usuario." : "The restore was cancelled by the user.";
    public string RestoreCancelledTitle     => IsSpanish ? "Cancelado"                         : "Cancelled";
    public string RestoreErrorFmt           => IsSpanish ? "❌ Error en restauración: {0}"     : "❌ Restore error: {0}";
    public string RestoreErrorStatus        => IsSpanish ? "Error en restauración."            : "Restore error.";
    public string RestoreErrorMsgFmt        => IsSpanish ? "Error al restaurar: {0}"           : "Restore failed: {0}";
    public string RestoreErrorTitle         => IsSpanish ? "Error de restauración"             : "Restore Error";
    public string CreatingWorkspaceLog      => IsSpanish ? "Creando workspace '{0}'..."        : "Creating workspace '{0}'...";
    public string WorkspaceCreatedLog       => IsSpanish ? "✅ Workspace '{0}' creado (ID: {1})." : "✅ Workspace '{0}' created (ID: {1}).";
    public string SelectAtLeastOneRestoreMsg => IsSpanish
        ? "Seleccione al menos un ítem para restaurar."
        : "Please select at least one item to restore.";
    public string NoBackupSelectedMsg       => IsSpanish
        ? "Seleccione un backup y un workspace de destino."
        : "Please select a backup and a destination workspace.";
    public string LoadingWorkspacesForRestore => IsSpanish
        ? "Cargando workspaces para restauración..."
        : "Loading workspaces for restore...";

    // ---- WorkspaceTreeNode ----
    public string LoadingPlaceholder => IsSpanish ? "Cargando..." : "Loading...";
    public string UnsupportedTooltip => IsSpanish
        ? "No disponible — Microsoft aún no expone una API de exportación para este tipo de ítem."
        : "Not available — Microsoft does not yet expose an export API for this item type.";

    // ---- BackupService ----
    public string NoItemsForBackup       => IsSpanish ? "No hay ítems seleccionados para hacer backup." : "No items selected for backup.";
    public string SavingItemFmt          => IsSpanish ? "Guardando {0}: {1}"                           : "Saving {0}: {1}";
    public string ErrorSavingItemFmt     => IsSpanish ? "Error al guardar {0}: {1}"                    : "Error saving {0}: {1}";
    public string BackupCompletedSvcFmt  => IsSpanish ? "Backup completado: {0} ítem(s) guardado(s)." : "Backup completed: {0} item(s) saved.";
    public string BackupWithErrorsSvcFmt => IsSpanish ? "Backup con errores: {0} guardado(s), {1} error(es)." : "Backup with errors: {0} saved, {1} error(s).";
    public string BackupCancelledSvcMsg  => IsSpanish ? "Backup cancelado por el usuario."             : "Backup cancelled by user.";

    // ---- INotifyPropertyChanged ----
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
