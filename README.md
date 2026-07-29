# Fabric Backup Lite

<p align="center">
  <img src="src/Fabric_backup_lite/Assets/app.png" width="96" alt="Fabric Backup Lite icon"/>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet" alt=".NET 8"/>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows" alt="Windows"/>
  <img src="https://img.shields.io/badge/license-MIT-green" alt="MIT License"/>
  <img src="https://img.shields.io/badge/version-2.1.0-blue" alt="v2.1.0"/>
  <img src="https://img.shields.io/badge/lang-ES%20%7C%20EN-orange" alt="ES | EN"/>
</p>

<p align="center">
  <a href="#español">🇪🇸 Español</a> &nbsp;|&nbsp; <a href="#english">🇺🇸 English</a>
</p>

---

## Español

Herramienta gratuita de **backup y restauración** para **Microsoft Fabric** y **Power BI**, con instalador MSI para Windows.

### ¿Qué hace?

Se conecta al tenant de Microsoft Fabric/Power BI mediante la API REST oficial y descarga las definiciones de los artefactos a carpetas locales, preservando la estructura y la metadata. A partir de v2.0.0 también permite restaurar esos artefactos en cualquier workspace del mismo tenant.

| Tipo de ítem | Formato guardado | Backup | Restore |
|---|---|---|---|
| Notebooks | `.ipynb` | ✅ | ✅ |
| Data Pipelines | `.json` | ✅ | ✅ |
| Dataflows Gen2 | `.json` | ✅ | ✅ |
| Reports (Power BI) | PBIR (múltiples `.json`) | ✅ | ✅ |
| Semantic Models | `.bim` | ✅* | ✅* |
| Lakehouses | `.json` (metadata) | ✅ | ✅ (vacío) |
| KQL Databases | `.json` | ✅ | ✅ |
| Eventhouses | `.json` | ✅ | ✅ |
| Environments | `.json` | ✅ | ✅ |
| Spark Job Definitions | `.json` | ✅ | ✅ |
| Warehouses | Archivos vía OneLake ADLS | ✅** | ❌*** |

> \* Los Semantic Models requieren capacidad Premium para exportar/importar.
> \*\* Los Warehouses se descargan directamente desde OneLake (requiere `Azure Data Lake > user_impersonation`).
> \*\*\* Los Warehouses no pueden recrearse vía API de Fabric; deben reconstruirse manualmente.

### Novedades en v2.1.0

- **Fix `403 InsufficientScopes` en backup:** la app solo solicitaba el scope `Workspace.Read.All`, suficiente para *listar* ítems pero no para leer su definición vía `getDefinition`. Ahora el token solicita también `Item.Read.All`, `Workspace.ReadWrite.All` e `Item.ReadWrite.All`, cubriendo backup y restore. Los usuarios afectados deben **cerrar sesión y volver a autenticarse** para reconsentir los nuevos permisos.
- **Docs:** corregido el nombre del permiso `Item.Read.All` (antes `Item.ReadAll`) y agregada una fila de troubleshooting para este error.

### Novedades en v2.0.0

- **Tab de Restauración:** nueva pestaña "↩ Restore" junto a la pestaña de Backup. Selecciona la carpeta raíz de backups y la app detecta automáticamente todos los backups disponibles.
- **Selección de versión:** ComboBox con todos los backups encontrados (ordenados por fecha descendente). Selecciona el backup a restaurar.
- **Ítems granulares:** lista de artefactos del backup con casillas de verificación. Los Warehouses aparecen deshabilitados con tooltip explicativo.
- **Workspace destino:** elige un workspace existente o crea uno nuevo en el momento. Si creas uno nuevo, selecciona la capacidad Fabric (las Trial se preseleccionan automáticamente).
- **Log y progreso:** misma experiencia que el backup — log en tiempo real con timestamps y barra de progreso.
- **Cancelación:** botón de cancelar disponible en cualquier punto de la restauración.

### Novedades en v1.7.0

- **Cobertura ampliada:** Lakehouse, KQLDatabase, Eventhouse, Environment, SparkJobDefinition ahora se respaldan vía `getDefinition` API. Warehouse se respalda vía descarga directa de OneLake ADLS.
- **Fabric CLI como fallback:** si un tipo de ítem no tiene API REST, la app intenta usar `fab export` (Fabric CLI) como último recurso.
- **Ventana de Configuración (⚙):** nuevo botón en el header para editar `ClientId` y `TenantId` sin tocar archivos de texto. Los cambios se guardan en `%APPDATA%\WeData\FabricBackupLite\usersettings.json`.
- **Mensajes de error mejorados:** AADSTS650057 muestra instrucciones paso a paso; los errores HTTP incluyen el body de la respuesta en el log.
- **Nuevo layout:** header de ancho completo con título, leyenda del autor y botones de acción.

### Novedades en v1.5.0

- **Interfaz bilingüe (ES / EN):** el botón **ES | EN** en la parte superior de la ventana cambia el idioma en tiempo real, sin reiniciar la aplicación.
- **Instalador mejorado:** ahora desinstala la versión anterior antes de instalar la nueva, evitando residuos de archivos.

### Instalación

1. Descarga el instalador MSI desde la sección [**Releases**](https://github.com/wcalcagno/fabric-backup-lite/releases)
2. Ejecuta `FabricBackupLite.msi` como administrador
3. El instalador crea accesos directos en Escritorio y Menú Inicio
4. **No se necesita instalar .NET** — el ejecutable es autocontenido

> Si tienes una versión anterior instalada, el instalador la desinstala automáticamente antes de continuar.

### Configuración: Registro de Aplicación en Microsoft Entra ID

La app usa autenticación delegada con MSAL. Necesitas registrar una aplicación en el tenant:

1. Ir a [portal.azure.com](https://portal.azure.com) → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Nombre: `Fabric Backup Lite` (o el que prefieras)
3. Supported account types: `Accounts in this organizational directory only`
4. Redirect URI: plataforma **Public client/native**, URI: `http://localhost`
5. Click **Register** y copiar el **Application (client) ID**
6. Ir a **API permissions** → **Add a permission** → **APIs my organization uses** → buscar **Power BI Service**
7. Agregar permisos delegados:
   - `Workspace.Read.All` — para listar workspaces (backup)
   - `Item.Read.All` — para leer artefactos (backup)
   - `Workspace.ReadWrite.All` — para crear workspaces y restaurar ítems (**nuevo en v2.0**)
   - `Item.ReadWrite.All` — para crear ítems vía API (**nuevo en v2.0**)
8. **Para backup de Warehouses:** agregar también **Azure Data Lake** → Delegated → `user_impersonation`
9. Click **Grant admin consent**

> **Nota:** Los permisos de escritura son necesarios solo para la funcionalidad de Restore. Si solo usas Backup, los permisos de lectura son suficientes.

Una vez instalada la app, usa el botón **⚙** en el header para ingresar tu `ClientId` y `TenantId` sin editar archivos manualmente. Los valores se guardan en:

```
%APPDATA%\WeData\FabricBackupLite\usersettings.json
```

### Cómo usar

#### Backup

**1. Iniciar sesión**
Clic en **"Iniciar sesión"**. Se abre el navegador para autenticación con Microsoft. Una vez autenticado, la app carga los workspaces del tenant automáticamente.

**2. Seleccionar ítems**
El panel izquierdo muestra un árbol con los workspaces y sus artefactos organizados por tipo. Puedes marcar ítems individuales, tipos completos o workspaces enteros. Los botones **"✓ Todo"** y **"✗ Ninguno"** seleccionan o deseleccionan todo.

**3. Elegir carpeta de destino**
Clic en **"Examinar..."** y selecciona la carpeta donde guardar los backups.

**4. Iniciar backup**
Clic en **"▶ Iniciar Backup"**. El log muestra el progreso en tiempo real. Puedes cancelar en cualquier momento con **"✕ Cancelar"**.

**5. Estructura del resultado en disco**

```
{CarpetaDestino}/
└── {TenantId}/
    └── {WorkspaceName}_{WorkspaceId}/
        └── {Timestamp}_backup/
            ├── manifest.json
            ├── Notebooks/
            ├── Reports/
            ├── Pipelines/
            ├── Dataflows/
            ├── SemanticModels/
            ├── Lakehouses/
            ├── Warehouses/
            ├── KQLDatabases/
            ├── Eventhouses/
            ├── Environments/
            └── SparkJobDefinitions/
```

#### Restore

**1. Ir a la pestaña "↩ Restore"**
Haz clic en la pestaña Restore en la parte superior de la ventana (disponible después de iniciar sesión).

**2. Seleccionar la carpeta raíz de backups**
Clic en **"Examinar..."** junto a "Carpeta raíz de backups". La app escanea la carpeta recursivamente y lista todos los backups encontrados en el ComboBox.

**3. Elegir el backup**
Selecciona el backup deseado en el ComboBox. La lista de ítems se carga automáticamente desde el `manifest.json`.

**4. Seleccionar ítems a restaurar**
Marca los ítems que quieres restaurar. Los Warehouses aparecen deshabilitados (no pueden restaurarse vía API).

**5. Elegir el workspace destino**
- **Workspace existente:** selecciónalo en el ComboBox.
- **Workspace nuevo:** selecciona "＋ Nuevo workspace", ingresa el nombre y elige una capacidad Fabric.

**6. Iniciar restauración**
Clic en **"▶ Iniciar Restore"**. El log muestra el progreso ítem por ítem.

> **Importante:** La restauración es en el mismo tenant. Si un ítem ya existe en el workspace destino, se producirá un error 409 (Conflict) para ese ítem; los demás continúan.

### Logs

```
%APPDATA%\WeData\FabricBackupLite\logs\fabric-backup-YYYYMMDD.txt
```

### Troubleshooting

| Error | Causa probable | Solución |
|---|---|---|
| `AADSTS650057` | Falta permiso Azure Data Lake en el registro de la app | Agregar `Azure Data Lake > user_impersonation` en API permissions → Grant admin consent |
| `GetDefinition failed (403 Forbidden): InsufficientScopes` | El token no pide el scope `Item.Read.All`; solo conceder el permiso en el registro no basta, la app debe **pedirlo** | Verificar que `Authentication:Scopes` en `appsettings.json` incluya `Item.Read.All` (y `Item.ReadWrite.All` para restore). Cerrar sesión y volver a autenticarte para reconsentir |
| `GetDefinition failed (400 BadRequest)` | Formato no soportado para ese tipo | Ver el body del error en el log; el tipo puede requerir un parámetro específico |
| `LRO failed: Dataset Workload...` | Semantic Model sin capacidad Premium o capacidad pausada | Se necesita capacidad Premium F SKU activa |
| `401 Unauthorized` | Token expirado o permisos insuficientes | Cierra la sesión y vuelve a autenticarte |
| `403 Forbidden` | Sin acceso al workspace | Pide al administrador que te agregue |
| `409 Conflict` al restaurar | El ítem ya existe en el workspace destino | Elimina el ítem del workspace destino o elige otro workspace |
| `CreateItem failed (403)` al restaurar | Faltan permisos de escritura | Agregar `Workspace.ReadWrite.All` e `Item.ReadWrite.All` → Grant admin consent |
| `Timeout en LRO polling` | Ítem muy grande o capacidad cargada | Aumentar `LROPollingInterval` en la configuración |

### Compilar desde código fuente

**Requisitos:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) · Windows 10/11 · Visual Studio 2022 o VS Code

```bash
git clone https://github.com/wcalcagno/fabric-backup-lite.git
cd fabric-backup-lite
dotnet run --project src/Fabric_backup_lite/Fabric_backup_lite.csproj
```

**Generar MSI:**

```powershell
dotnet publish src/Fabric_backup_lite -c Release -r win-x64 --self-contained true -o installer/publish
powershell -ExecutionPolicy Bypass -File installer/GenerateFiles.ps1
dotnet build installer/wix/FabricBackupLite.wixproj -c Release
```

### Limitaciones conocidas (v2.0)

- ❌ No incremental — siempre backup completo
- ❌ No scheduling — ejecución manual
- ❌ Los Warehouses no se pueden restaurar vía API (requieren recreación manual)
- ⚠️ La restauración es siempre en el mismo tenant (característica "Lite")
- ⚠️ Semantic Models requieren capacidad Premium F SKU activa
- ⚠️ Warehouses requieren permiso adicional `Azure Data Lake > user_impersonation`

### Roadmap

- [x] Restore de artefactos — ✅ v2.0.0
- [ ] Backup incremental
- [ ] Scheduling automático (Windows Task Scheduler)
- [ ] CLI para pipelines CI/CD
- [ ] Compresión ZIP
- [ ] Notificaciones por email / Teams
- [ ] Soporte multi-tenant

### Agradecimientos

Un especial reconocimiento a **Gilbert Quevauvilliers** ([FourMoo](https://www.fourmoo.com/)) cuyo artículo fue la inspiración clave para implementar el soporte de nuevos tipos de artefactos en v1.7.0:

> [*Backing up your Microsoft Fabric Workspace: A Notebook Driven Approach to Disaster Recovery*](https://www.fourmoo.com/2025/12/03/backing-up-your-microsoft-fabric-workspace-a-notebook-driven-approach-to-disaster-recovery/)

Su trabajo permitió clarificar las APIs disponibles y el enfoque correcto para respaldar Lakehouses, Warehouses, KQL Databases, Eventhouses, Environments y Spark Job Definitions — tipos que en versiones anteriores aparecían como "no disponibles". ¡Gracias Gilbert!

### Contribuciones

Pull requests bienvenidos. Para cambios grandes, abre un issue primero para discutir el enfoque.

### Licencia

MIT © 2026 [Walter Calcagno](mailto:Walter@inegocios.cl)

> **Aviso:** Esta herramienta se encuentra en desarrollo activo. Puede presentar errores o comportamientos no esperados. Reportar bugs a [Walter@inegocios.cl](mailto:Walter@inegocios.cl) o via GitHub Issues.

---

## English

Free **backup and restore** tool for **Microsoft Fabric** and **Power BI**, with an MSI installer for Windows.

### What does it do?

It connects to your Microsoft Fabric/Power BI tenant through the official REST API and downloads artifact definitions to local folders, preserving structure and metadata. Starting with v2.0.0 it can also restore those artifacts to any workspace in the same tenant.

| Item type | Saved format | Backup | Restore |
|---|---|---|---|
| Notebooks | `.ipynb` | ✅ | ✅ |
| Data Pipelines | `.json` | ✅ | ✅ |
| Dataflows Gen2 | `.json` | ✅ | ✅ |
| Reports (Power BI) | PBIR (multiple `.json`) | ✅ | ✅ |
| Semantic Models | `.bim` | ✅* | ✅* |
| Lakehouses | `.json` (metadata) | ✅ | ✅ (empty) |
| KQL Databases | `.json` | ✅ | ✅ |
| Eventhouses | `.json` | ✅ | ✅ |
| Environments | `.json` | ✅ | ✅ |
| Spark Job Definitions | `.json` | ✅ | ✅ |
| Warehouses | Files via OneLake ADLS | ✅** | ❌*** |

> \* Semantic Models require Premium capacity to export/import.
> \*\* Warehouses are downloaded directly from OneLake (requires `Azure Data Lake > user_impersonation`).
> \*\*\* Warehouses cannot be recreated via Fabric API; they must be rebuilt manually.

### What's new in v2.1.0

- **Fix `403 InsufficientScopes` on backup:** the app only requested the `Workspace.Read.All` scope, enough to *list* items but not to read their definition via `getDefinition`. The token now also requests `Item.Read.All`, `Workspace.ReadWrite.All` and `Item.ReadWrite.All`, covering both backup and restore. Affected users must **sign out and sign in again** to re-consent to the new permissions.
- **Docs:** fixed the `Item.Read.All` permission name (was `Item.ReadAll`) and added a troubleshooting row for this error.

### What's new in v2.0.0

- **Restore tab:** new "↩ Restore" tab alongside the Backup tab. Select the backup root folder and the app auto-detects all available backups.
- **Version picker:** ComboBox listing all discovered backups (sorted by date, newest first). Select the one you want to restore.
- **Granular item selection:** checklist of artifacts from the backup. Warehouses appear disabled with an explanatory tooltip.
- **Destination workspace:** choose an existing workspace or create a new one on the fly. If creating new, pick the Fabric capacity (Trial capacities are pre-selected automatically).
- **Log and progress:** same experience as backup — real-time log with timestamps and a progress bar.
- **Cancellation:** cancel button available at any point during the restore.

### What's new in v1.7.0

- **Expanded coverage:** Lakehouse, KQLDatabase, Eventhouse, Environment, SparkJobDefinition are now backed up via the `getDefinition` API. Warehouse is backed up via direct OneLake ADLS download.
- **Fabric CLI fallback:** if an item type has no REST API, the app tries `fab export` (Fabric CLI) as a last resort.
- **Settings window (⚙):** new button in the header to edit `ClientId` and `TenantId` without touching text files. Changes are saved to `%APPDATA%\WeData\FabricBackupLite\usersettings.json`.
- **Improved error messages:** AADSTS650057 shows step-by-step instructions; HTTP errors include the response body in the log.
- **New layout:** full-width header with title, author tagline, and action buttons.

### What's new in v1.5.0

- **Bilingual UI (ES / EN):** the **ES | EN** button at the top of the window switches the language in real time, without restarting the application.
- **Improved installer:** now uninstalls the previous version before installing the new one, preventing leftover files.

### Installation

1. Download the MSI installer from the [**Releases**](https://github.com/wcalcagno/fabric-backup-lite/releases) section
2. Run `FabricBackupLite.msi` as administrator
3. The installer creates shortcuts on the Desktop and Start Menu
4. **.NET is not required** — the executable is self-contained

> If you have a previous version installed, the installer will uninstall it automatically before proceeding.

### Configuration: Microsoft Entra ID App Registration

The app uses delegated authentication with MSAL. You need to register an application in your tenant:

1. Go to [portal.azure.com](https://portal.azure.com) → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Name: `Fabric Backup Lite` (or any name you prefer)
3. Supported account types: `Accounts in this organizational directory only`
4. Redirect URI: platform **Public client/native**, URI: `http://localhost`
5. Click **Register** and copy the **Application (client) ID**
6. Go to **API permissions** → **Add a permission** → **APIs my organization uses** → search for **Power BI Service**
7. Add delegated permissions:
   - `Workspace.Read.All` — to list workspaces (backup)
   - `Item.Read.All` — to read artifacts (backup)
   - `Workspace.ReadWrite.All` — to create workspaces and restore items (**new in v2.0**)
   - `Item.ReadWrite.All` — to create items via API (**new in v2.0**)
8. **For Warehouse backup:** also add **Azure Data Lake** → Delegated → `user_impersonation`
9. Click **Grant admin consent**

> **Note:** Write permissions are only required for the Restore feature. If you only use Backup, read permissions are sufficient.

Once the app is installed, use the **⚙** button in the header to enter your `ClientId` and `TenantId` without editing files manually. Values are saved to:

```
%APPDATA%\WeData\FabricBackupLite\usersettings.json
```

### How to use

#### Backup

**1. Sign in**
Click **"Sign In"**. A browser window opens for Microsoft authentication. Once authenticated, the app automatically loads all workspaces in the tenant.

**2. Select items**
The left panel shows a tree with all workspaces and their artifacts organized by type. You can check individual items, full types, or entire workspaces. Use **"✓ All"** and **"✗ None"** to select or deselect everything.

**3. Choose a destination folder**
Click **"Browse..."** and select the folder where you want to save the backups.

**4. Start backup**
Click **"▶ Start Backup"**. The activity log shows progress in real time. You can cancel at any time with **"✕ Cancel"**.

**5. Output folder structure**

```
{DestinationFolder}/
└── {TenantId}/
    └── {WorkspaceName}_{WorkspaceId}/
        └── {Timestamp}_backup/
            ├── manifest.json
            ├── Notebooks/
            ├── Reports/
            ├── Pipelines/
            ├── Dataflows/
            ├── SemanticModels/
            ├── Lakehouses/
            ├── Warehouses/
            ├── KQLDatabases/
            ├── Eventhouses/
            ├── Environments/
            └── SparkJobDefinitions/
```

#### Restore

**1. Go to the "↩ Restore" tab**
Click the Restore tab at the top of the window (available after signing in).

**2. Select the backup root folder**
Click **"Browse..."** next to "Backup root folder". The app scans the folder recursively and lists all discovered backups in the ComboBox.

**3. Choose a backup**
Select the desired backup from the ComboBox. The item list is loaded automatically from the `manifest.json`.

**4. Select items to restore**
Check the items you want to restore. Warehouses appear disabled (cannot be restored via API).

**5. Choose the destination workspace**
- **Existing workspace:** select it from the ComboBox.
- **New workspace:** select "＋ New workspace", enter a name, and choose a Fabric capacity.

**6. Start restore**
Click **"▶ Start Restore"**. The log shows progress item by item.

> **Important:** Restore is same-tenant only. If an item already exists in the destination workspace, a 409 (Conflict) error will occur for that item; other items continue normally.

### Logs

```
%APPDATA%\WeData\FabricBackupLite\logs\fabric-backup-YYYYMMDD.txt
```

### Troubleshooting

| Error | Likely cause | Solution |
|---|---|---|
| `AADSTS650057` | Missing Azure Data Lake permission on app registration | Add `Azure Data Lake > user_impersonation` in API permissions → Grant admin consent |
| `GetDefinition failed (403 Forbidden): InsufficientScopes` | The token doesn't request the `Item.Read.All` scope; granting the permission on the app registration alone is not enough — the app must **request** it | Make sure `Authentication:Scopes` in `appsettings.json` includes `Item.Read.All` (and `Item.ReadWrite.All` for restore). Sign out and sign in again to re-consent |
| `GetDefinition failed (400 BadRequest)` | Export format not supported for that type | Check error body in the log; the type may require a specific parameter |
| `LRO failed: Dataset Workload...` | Semantic Model without Premium capacity or paused capacity | Active Premium F SKU capacity is required |
| `401 Unauthorized` | Expired token or insufficient permissions | Sign out and sign in again |
| `403 Forbidden` | No access to the workspace | Ask the workspace administrator to add you |
| `409 Conflict` on restore | Item already exists in destination workspace | Remove the item from the destination workspace or choose a different one |
| `CreateItem failed (403)` on restore | Missing write permissions | Add `Workspace.ReadWrite.All` and `Item.ReadWrite.All` → Grant admin consent |
| `Timeout in LRO polling` | Very large item or busy capacity | Increase `LROPollingInterval` in Settings |

### Build from source

**Requirements:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) · Windows 10/11 · Visual Studio 2022 or VS Code

```bash
git clone https://github.com/wcalcagno/fabric-backup-lite.git
cd fabric-backup-lite
dotnet run --project src/Fabric_backup_lite/Fabric_backup_lite.csproj
```

**Generate MSI:**

```powershell
dotnet publish src/Fabric_backup_lite -c Release -r win-x64 --self-contained true -o installer/publish
powershell -ExecutionPolicy Bypass -File installer/GenerateFiles.ps1
dotnet build installer/wix/FabricBackupLite.wixproj -c Release
```

### Known limitations (v2.0)

- ❌ No incremental backup — always full backup
- ❌ No scheduling — manual execution
- ❌ Warehouses cannot be restored via API (manual rebuild required)
- ⚠️ Restore is same-tenant only ("Lite" characteristic)
- ⚠️ Semantic Models require active Premium F SKU capacity
- ⚠️ Warehouses require additional `Azure Data Lake > user_impersonation` permission

### Roadmap

- [x] Artifact restore — ✅ v2.0.0
- [ ] Incremental backup
- [ ] Automatic scheduling (Windows Task Scheduler)
- [ ] CLI for CI/CD pipelines
- [ ] ZIP compression
- [ ] Email / Teams notifications
- [ ] Multi-tenant support

### Acknowledgements

Special thanks to **Gilbert Quevauvilliers** ([FourMoo](https://www.fourmoo.com/)) whose article was the key inspiration for implementing support for new artifact types in v1.7.0:

> [*Backing up your Microsoft Fabric Workspace: A Notebook Driven Approach to Disaster Recovery*](https://www.fourmoo.com/2025/12/03/backing-up-your-microsoft-fabric-workspace-a-notebook-driven-approach-to-disaster-recovery/)

His work helped clarify the available APIs and the correct approach for backing up Lakehouses, Warehouses, KQL Databases, Eventhouses, Environments and Spark Job Definitions — types that in previous versions appeared as "not available". Thank you, Gilbert!

### Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss the approach.

### License

MIT © 2026 [Walter Calcagno](mailto:Walter@inegocios.cl)

> **Notice:** This tool is under active development and may contain bugs or unexpected behavior. Report issues to [Walter@inegocios.cl](mailto:Walter@inegocios.cl) or via GitHub Issues.
