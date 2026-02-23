# Fabric Backup Lite

<p align="center">
  <img src="src/Fabric_backup_lite/Assets/app.png" width="96" alt="Fabric Backup Lite icon"/>
</p>

<p align="center">
  Herramienta gratuita de respaldo para <strong>Microsoft Fabric</strong> y <strong>Power BI</strong>, con instalador MSI para Windows.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet" alt=".NET 8"/>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows" alt="Windows"/>
  <img src="https://img.shields.io/badge/license-MIT-green" alt="MIT License"/>
  <img src="https://img.shields.io/badge/version-1.4.0-blue" alt="v1.4.0"/>
</p>

---

## ¿Qué hace?

Fabric Backup Lite se conecta a tu tenant de Microsoft Fabric/Power BI mediante la API REST oficial y descarga las definiciones de tus artefactos en carpetas locales, preservando la estructura y los metadatos.

| Tipo de ítem       | Formato guardado         | Estado                  |
| ------------------ | ------------------------ | ----------------------- |
| Notebooks          | `.ipynb`                 | ✅ Soportado             |
| Data Pipelines     | `.json`                  | ✅ Soportado             |
| Dataflows Gen2     | `.json`                  | ✅ Soportado             |
| Reports (Power BI) | PBIR (múltiples `.json`) | ✅ Soportado             |
| Semantic Models    | `.bim`                   | ✅ Soportado*            |
| Lakehouses         | —                        | ⛔ API no disponible aún |
| Warehouses         | —                        | ⛔ API no disponible aún |
| KQL Databases      | —                        | ⛔ API no disponible aún |

> *Los Semantic Models requieren capacidad Premium para exportar. Los ítems no soportados aparecen visibles en la interfaz, pero con el checkbox deshabilitado y un tooltip explicativo.*

---

## Instalación (usuarios finales)

1. Descarga el instalador MSI desde la sección **Releases**
   [https://github.com/wcalcagno/fabric-backup-lite/releases](https://github.com/wcalcagno/fabric-backup-lite/releases)
2. Ejecuta `FabricBackupLite.msi` como administrador.
3. El instalador crea accesos directos en el Escritorio y en el Menú Inicio.
4. No es necesario instalar .NET — el ejecutable es autocontenido.

---

## Configuración: Azure AD App Registration

La aplicación utiliza autenticación delegada con MSAL. Debes registrar una aplicación en tu tenant:

1. Ir a [https://portal.azure.com](https://portal.azure.com)
2. Azure Active Directory → App registrations → New registration
3. Nombre: `Fabric Backup Lite` (o el que prefieras)
4. Supported account types: `Accounts in this organizational directory only`
5. Redirect URI: Plataforma **Public client/native**, URI: `http://localhost`
6. Haz clic en **Register** y copia el **Application (client) ID**
7. Ir a **API permissions** → Add a permission → APIs my organization uses → buscar **Power BI Service**
8. Agregar permisos delegados:

   * `Workspace.Read.All`
   * `Item.Read.All` o `Item.ReadWrite.All`
9. Haz clic en **Grant admin consent**

Luego edita el archivo `appsettings.json` ubicado en:

```
C:\Program Files\WeData\Fabric Backup Lite\
```

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

---

## Cómo usar

### 1. Iniciar sesión

Haz clic en **Iniciar sesión**.
Se abrirá el navegador para autenticación con Microsoft.
Una vez autenticado, la aplicación carga automáticamente los workspaces del tenant.

### 2. Explorar y seleccionar ítems

El panel izquierdo muestra un árbol con todos los workspaces y sus artefactos organizados por tipo. Puedes:

* Marcar o desmarcar ítems individuales
* Marcar un tipo completo (por ejemplo, todos los Notebooks)
* Marcar un workspace completo
* Usar los botones **✓ Todo** y **✗ Ninguno**

Los ítems en gris itálico (Lakehouses, Warehouses, KQL Databases) no tienen API de exportación disponible actualmente.

### 3. Elegir carpeta de destino

Haz clic en **Examinar...** y selecciona la carpeta donde deseas guardar los respaldos.

### 4. Iniciar respaldo

Haz clic en **Iniciar Backup**.
El registro de actividad muestra el progreso en tiempo real.
Puedes cancelar en cualquier momento con **Cancelar**.

---

## Estructura del respaldo en disco

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

---

## Logs

Los logs se escriben en:

```
%APPDATA%\WeData\FabricBackupLite\logs\fabric-backup-YYYYMMDD.txt
```

Ejemplo:

```
C:\Users\{tu_usuario}\AppData\Roaming\WeData\FabricBackupLite\logs\
```

---

## Troubleshooting

| Error                                                | Causa probable                   | Solución                                              |
| ---------------------------------------------------- | -------------------------------- | ----------------------------------------------------- |
| GetDefinition failed: BadRequest                     | Formato no soportado             | El tipo de ítem no puede exportarse con la API actual |
| LRO failed: Premium capacity connection health issue | Requiere Premium                 | Activar capacidad Premium F SKU                       |
| 401 Unauthorized                                     | Token expirado                   | Cerrar sesión y volver a autenticarse                 |
| 403 Forbidden                                        | Sin acceso al workspace          | Solicitar acceso al administrador                     |
| No Location header in 202 response                   | Cambio en la API                 | Reportar issue en GitHub                              |
| Timeout en LRO polling                               | Ítem grande o capacidad saturada | Ajustar `LROPollingInterval`                          |

---

## Compilar desde código fuente

### Requisitos

* .NET 8 SDK
* Windows 10/11
* Visual Studio 2022 o VS Code con extensión C#

### Ejecutar

```bash
git clone https://github.com/wcalcagno/fabric-backup-lite.git
cd fabric-backup-lite
dotnet restore src/Fabric_backup_lite/Fabric_backup_lite.csproj
dotnet run --project src/Fabric_backup_lite/Fabric_backup_lite.csproj
```

### Generar MSI

```powershell
dotnet publish src/Fabric_backup_lite -c Release -r win-x64 --self-contained true -o installer/publish
powershell -ExecutionPolicy Bypass -File installer/GenerateFiles.ps1
dotnet build installer/wix/FabricBackupLite.wixproj -c Release
```

El MSI se genera en:

```
installer/wix/bin/x64/Release/FabricBackupLite.msi
```

---

## Limitaciones conocidas (v1.4)

* Solo respaldo, no restauración
* No incremental (siempre respaldo completo)
* Sin programación automática
* Sin soporte para Lakehouses, Warehouses y KQL Databases
* Semantic Models requieren capacidad Premium F SKU

---

## Roadmap

* Restore de artefactos
* Backup incremental
* Scheduling automático
* CLI para CI/CD
* Compresión ZIP
* Notificaciones por correo o Teams
* Soporte multi-tenant

---

## Contribuciones

Pull requests son bienvenidos. Para cambios grandes, abre un issue primero para discutir el enfoque.

---

## Licencia

MIT © 2026 Walter Calcagno

---

> Aviso: Esta herramienta se encuentra en desarrollo activo. Puede presentar errores o comportamientos no esperados.
> Reporta bugs a [Walter@inegocios.cl](mailto:Walter@inegocios.cl) o mediante GitHub Issues.
