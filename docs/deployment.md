# Deployment

One-time setup on the Windows 11 host.

## Prerequisites

- SQL Server (any edition) with the `KanbanBoard` database created by `dotnet ef database update`.
- IIS with the **ASP.NET Core Hosting Bundle** for .NET 8 installed. Without it IIS returns
  HTTP 500.19 or 502.5. Download from https://dotnet.microsoft.com/download/dotnet/8.0 and
  restart IIS afterwards with `iisreset`.

## IIS site

Run as administrator:

```powershell
Import-Module WebAdministration
New-WebAppPool -Name Kanban
# "No Managed Code" is correct: ASP.NET Core runs out-of-process from the CLR the pool would load.
Set-ItemProperty IIS:\AppPools\Kanban -Name managedRuntimeVersion -Value ''
New-Item -ItemType Directory -Force -Path C:\inetpub\kanban
New-Website -Name Kanban -Port 8080 -PhysicalPath C:\inetpub\kanban -ApplicationPool Kanban
New-NetFirewallRule -DisplayName "Kanban 8080" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow
```

## Database access for the app pool

The app pool identity, not your account, connects to SQL Server. Grant it access:

```sql
CREATE LOGIN [IIS APPPOOL\Kanban] FROM WINDOWS;
USE KanbanBoard;
CREATE USER [IIS APPPOOL\Kanban] FOR LOGIN [IIS APPPOOL\Kanban];
ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\Kanban];
ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\Kanban];
```

## Deploying

```powershell
.\build\build.ps1 -Task All
```

Then browse from any LAN device to `http://<host-name>:8080`.