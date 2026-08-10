# Quick Start (Local Development)

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (any edition) or SQLite

## Steps

```powershell
$env:KANBAN_CONNECTION_STRING = "Server=localhost;Database=KanbanBoard;Trusted_Connection=True;TrustServerCertificate=True"
dotnet restore Kanban.sln
dotnet build Kanban.sln
dotnet ef database update --project src/Kanban.Core --startup-project src/Kanban.Web
dotnet run --project src/Kanban.Web
```

Open `http://localhost:28763` (the default dev port; change in `launchSettings.json` or pass `--urls` to override).

To run the AI runner:

```powershell
dotnet run --project src/Kanban.Runner
```

The runner needs [Crush](https://github.com/crush-ai/crush) installed and configured. See the full deployment guide below for production setup.

---

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

## Runner service

The Runner runs as a Windows Service. Publish first, then install once, as administrator:

```powershell
.\build\build.ps1 -Task Publish
New-Item -ItemType Directory -Force -Path C:\Services\KanbanRunner
robocopy .\artifacts\publish\Kanban.Runner C:\Services\KanbanRunner /MIR
New-Service -Name KanbanRunner `
            -DisplayName "Kanban Runner" `
            -BinaryPathName "C:\Services\KanbanRunner\Kanban.Runner.exe" `
            -StartupType Automatic
Start-Service KanbanRunner
```

### Service account

The service runs as LocalSystem by default. It needs two things LocalSystem may not have:

1. **Access to your project directories and their git repositories.** LocalSystem has broad local
   rights but no user profile, so `git` may not find a global identity. Give the service its own:

   ```powershell
   git config --system user.name  "Kanban Runner"
   git config --system user.email "runner@localhost"
   ```

2. **Crush's OpenRouter configuration.** Crush reads its key from the *service account's* environment
   or profile, not yours. Either set the key as a machine-level environment variable, or run the
   service as your own account:

   ```powershell
   # Machine-level so LocalSystem sees it. Restart the service afterwards.
   [Environment]::SetEnvironmentVariable('OPENROUTER_API_KEY', '<key>', 'Machine')
   ```

   Running as your own user is usually simpler, because Crush's config file lives in your profile.
   Set it in `services.msc` → Kanban Runner → Log On, or:

   ```powershell
   sc.exe config KanbanRunner obj= ".\YourUserName" password= "YourPassword"
   ```

3. **The `crush` executable.** LocalSystem does not have your user PATH. The `AgentCommand` setting
   in `appsettings.json` must be an absolute path:

   ```json
   "AgentCommand": "C:\\Users\\Admin\\AppData\\Roaming\\npm\\crush.cmd"
   ```

   Find your crush path with `where crush` in your own terminal, then update the config before
   deploying. If you run the service as your own user (step 2 above), just `"crush"` works.

### Logs

The service writes to the Windows Application event log under source `Kanban Runner`:

```powershell
Get-EventLog -LogName Application -Source "Kanban Runner" -Newest 20
```

Per-card logs live in the database and are visible on each card.