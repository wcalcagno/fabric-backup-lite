# Fabric Backup Lite

<p align="center">
  <img src="src/Fabric_backup_lite/Assets/app.png" width="96" alt="Fabric Backup Lite icon"/>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet" alt=".NET 8"/>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows" alt="Windows"/>
  <img src="https://img.shields.io/badge/license-MIT-green" alt="MIT License"/>
  <img src="https://img.shields.io/badge/version-1.5.0-blue" alt="v1.5.0"/>
  <img src="https://img.shields.io/badge/lang-ES%20%7C%20EN-orange" alt="ES | EN"/>
</p>

<p align="center">
  <a href="#español">🇪🇸 Español</a> &nbsp;|&nbsp; <a href="#english">🇺🇸 English</a>
</p>

---

## Español

Herramienta gratuita de backup para **Microsoft Fabric** y **Power BI**, con instalador MSI para Windows.

### ¿Qué hace?

Se conecta al tenant de Microsoft Fabric/Power BI mediante la API REST oficial y descarga las definiciones de los artefactos a carpetas locales, preservando la estructura y la metadata.

| Tipo de ítem | Formato guardado | Estado |
|---|---|---|
| Notebooks | `.ipynb` | ✅ Soportado |
| Data Pipelines | `.json` | ✅ Soportado |
| Dataflows Gen2 | `.json` | ✅ Soportado |
| Reports (Power BI) | PBIR (múltiples `.json`) | ✅ Soportado |
| Semantic Models | `.bim` | ✅ Soportado* |
| Lakehouses | — | ⛔ API no disponible aún |
| Warehouses | — | ⛔ API no disponible aún |
| KQL Databases | — | ⛔ API no disponible aún |

> \* Los Semantic Models requieren capacidad Premium para exportar. Los ítems no soportados aparecen en la UI con checkbox deshabilitado y tooltip explicativo.

### Novedades en v1.5.0

- **Interfaz bilingüe (ES / EN):** el botón **ES | EN** en la parte superior de la ventana cambia el idioma en tiempo real, sin reiniciar la aplicación. Todos los textos, mensajes y tooltips se actualizan al instante.
- **Instalador mejorado:** ahora desinstala la versión anterior antes de instalar la nueva, evitando residuos de archivos.

### Instalación

1. Descarga el instalador MSI desde la sección [**Releases**](https://github.com/wcalcagno/fabric-backup-lite/releases)
2. Ejecuta `FabricBackupLite.msi` como administrador
3. El instalador crea accesos directos en Escritorio y Menú Inicio
4. **No se necesita instalar .NET** — el ejecutable es autocontenido

> Si tienes una versión anterior instalada, el instalador la desinstala automáticamente antes de continuar.

### Configuración: Azure AD App Registration

La app usa autenticación delegada con MSAL. Necesitas registrar una aplicación en el tenant:

1. Ir a [portal.azure.com](https://portal.azure.com) → **Azure Active Directory** → **App registrations** → **New registration**
2. Nombre: `Fabric Backup Lite` (o el que prefieras)
3. Supported account types: `Accounts in this organizational directory only`
4. Redirect URI: plataforma **Public client/native**, URI: `http://localhost`
5. Click **Register** y copiar el **Application (client) ID**
6. Ir a **API permissions** → **Add a permission** → **APIs my organization uses** → buscar **Power BI Service**
7. Agregar permisos delegados: `Workspace.Read.All` y `Item.ReadAll` (o `Item.ReadWrite.All`)
8. Click **Grant admin consent**

Luego, editar `appsettings.json` en la carpeta de instalación (`C:\Program Files\WeData\Fabric Backup Lite\`):

```json
{
  "Authentication": {
    "ClientId": "TU-CLIENT-ID-AQUI",
    "TenantId": "TU-TENANT-ID-AQUI",
    "RedirectUri": "http://localhost",
    "Scopes": [
      "https://analysis.windows.net/powerbi/api/Workspace.Read.All"
    ]
  }
}
```

### Cómo usar

**1. Iniciar sesión**
Clic en **"Iniciar sesión"**. Se abre el navegador para autenticación con Microsoft. Una vez autenticado, la app carga los workspaces del tenant automáticamente.

**2. Seleccionar ítems**
El panel izquierdo muestra un árbol con los workspaces y sus artefactos organizados por tipo. Puedes marcar ítems individuales, tipos completos o workspaces enteros. Los botones **"✓ Todo"** y **"✗ Ninguno"** seleccionan o deseleccionan todo. Los ítems en gris itálico no tienen API de exportación disponible aún.

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
            └── SemanticModels/
```

### Logs

```
%APPDATA%\WeData\FabricBackupLite\logs\fabric-backup-YYYYMMDD.txt
```

### Troubleshooting

| Error | Causa probable | Solución |
|---|---|---|
| `GetDefinition failed: BadRequest` | Formato no soportado para ese tipo | El tipo de ítem no puede exportarse con la API actual |
| `LRO failed: Premium capacity...` | Semantic Model sin capacidad Premium | Se necesita capacidad Premium F SKU en ese workspace |
| `401 Unauthorized` | Token expirado o permisos insuficientes | Cierra la sesión y vuelve a autenticarte |
| `403 Forbidden` | Sin acceso al workspace | Pide al administrador que te agregue |
| `Timeout en LRO polling` | Ítem muy grande o capacidad cargada | Aumentar `LROPollingInterval` en `appsettings.json` |

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

### Limitaciones conocidas (v1.5)

- ❌ Solo backup, no restore — está en el roadmap
- ❌ No incremental — siempre backup completo
- ❌ No scheduling — ejecución manual
- ⛔ Lakehouses / Warehouses / KQL Databases — sin API de exportación en Microsoft Fabric aún
- ⚠️ Semantic Models requieren capacidad Premium F SKU

### Roadmap

- [ ] Restore de artefactos
- [ ] Backup incremental
- [ ] Scheduling automático (Windows Task Scheduler)
- [ ] CLI para pipelines CI/CD
- [ ] Compresión ZIP
- [ ] Notificaciones por email / Teams
- [ ] Soporte multi-tenant

### Contribuciones

Pull requests bienvenidos. Para cambios grandes, abre un issue primero para discutir el enfoque.

### Licencia

MIT © 2026 [Walter Calcagno](mailto:Walter@inegocios.cl)

> **Aviso:** Esta herramienta se encuentra en desarrollo activo. Puede presentar errores o comportamientos no esperados. Reportar bugs a [Walter@inegocios.cl](mailto:Walter@inegocios.cl) o via GitHub Issues.

---

## English

Free backup tool for **Microsoft Fabric** and **Power BI**, with an MSI installer for Windows.

### What does it do?

It connects to your Microsoft Fabric/Power BI tenant through the official REST API and downloads artifact definitions to local folders, preserving structure and metadata.

| Item type | Saved format | Status |
|---|---|---|
| Notebooks | `.ipynb` | ✅ Supported |
| Data Pipelines | `.json` | ✅ Supported |
| Dataflows Gen2 | `.json` | ✅ Supported |
| Reports (Power BI) | PBIR (multiple `.json`) | ✅ Supported |
| Semantic Models | `.bim` | ✅ Supported* |
| Lakehouses | — | ⛔ API not available yet |
| Warehouses | — | ⛔ API not available yet |
| KQL Databases | — | ⛔ API not available yet |

> \* Semantic Models require Premium capacity to export. Unsupported items are shown in the UI with a disabled checkbox and an explanatory tooltip.

### What's new in v1.5.0

- **Bilingual UI (ES / EN):** the **ES | EN** button at the top of the window switches the language in real time, without restarting the application. All texts, messages and tooltips update instantly.
- **Improved installer:** now uninstalls the previous version before installing the new one, preventing leftover files.

### Installation

1. Download the MSI installer from the [**Releases**](https://github.com/wcalcagno/fabric-backup-lite/releases) section
2. Run `FabricBackupLite.msi` as administrator
3. The installer creates shortcuts on the Desktop and Start Menu
4. **.NET is not required** — the executable is self-contained

> If you have a previous version installed, the installer will uninstall it automatically before proceeding.

### Configuration: Azure AD App Registration

The app uses delegated authentication with MSAL. You need to register an application in your tenant:

1. Go to [portal.azure.com](https://portal.azure.com) → **Azure Active Directory** → **App registrations** → **New registration**
2. Name: `Fabric Backup Lite` (or any name you prefer)
3. Supported account types: `Accounts in this organizational directory only`
4. Redirect URI: platform **Public client/native**, URI: `http://localhost`
5. Click **Register** and copy the **Application (client) ID**
6. Go to **API permissions** → **Add a permission** → **APIs my organization uses** → search for **Power BI Service**
7. Add delegated permissions: `Workspace.Read.All` and `Item.ReadAll` (or `Item.ReadWrite.All`)
8. Click **Grant admin consent**

Then edit `appsettings.json` in the installation folder (`C:\Program Files\WeData\Fabric Backup Lite\`):

```json
{
  "Authentication": {
    "ClientId": "YOUR-CLIENT-ID-HERE",
    "TenantId": "YOUR-TENANT-ID-HERE",
    "RedirectUri": "http://localhost",
    "Scopes": [
      "https://analysis.windows.net/powerbi/api/Workspace.Read.All"
    ]
  }
}
```

### How to use

**1. Sign in**
Click **"Sign In"**. A browser window opens for Microsoft authentication. Once authenticated, the app automatically loads all workspaces in the tenant.

**2. Select items**
The left panel shows a tree with all workspaces and their artifacts organized by type. You can check individual items, full types, or entire workspaces. Use **"✓ All"** and **"✗ None"** to select or deselect everything. Items shown in gray italic (Lakehouses, Warehouses, KQL Databases) do not have an export API available yet.

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
            └── SemanticModels/
```

### Logs

```
%APPDATA%\WeData\FabricBackupLite\logs\fabric-backup-YYYYMMDD.txt
```

### Troubleshooting

| Error | Likely cause | Solution |
|---|---|---|
| `GetDefinition failed: BadRequest` | Export format not supported for that type | The item type cannot be exported with the current API |
| `LRO failed: Premium capacity...` | Semantic Model without Premium capacity | Premium F SKU capacity is required in that workspace |
| `401 Unauthorized` | Expired token or insufficient permissions | Sign out and sign in again |
| `403 Forbidden` | No access to the workspace | Ask the workspace administrator to add you |
| `Timeout in LRO polling` | Very large item or busy capacity | Increase `LROPollingInterval` in `appsettings.json` |

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

### Known limitations (v1.5)

- ❌ Backup only, no restore — on the roadmap
- ❌ No incremental backup — always full backup
- ❌ No scheduling — manual execution
- ⛔ Lakehouses / Warehouses / KQL Databases — no export API in Microsoft Fabric yet
- ⚠️ Semantic Models require Premium F SKU capacity

### Roadmap

- [ ] Artifact restore
- [ ] Incremental backup
- [ ] Automatic scheduling (Windows Task Scheduler)
- [ ] CLI for CI/CD pipelines
- [ ] ZIP compression
- [ ] Email / Teams notifications
- [ ] Multi-tenant support

### Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss the approach.

### License

MIT © 2026 [Walter Calcagno](mailto:Walter@inegocios.cl)

> **Notice:** This tool is under active development and may contain bugs or unexpected behavior. Report issues to [Walter@inegocios.cl](mailto:Walter@inegocios.cl) or via GitHub Issues.
