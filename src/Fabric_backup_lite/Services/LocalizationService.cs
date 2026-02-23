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
        ? " para poder corregirlo en futuras versiones.\n\nGracias por colaborar en su mejora continua."
        : " so it can be fixed in future versions.\n\nThank you for contributing to its continuous improvement.";

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
