# Setup

End-to-end guide from a fresh clone to a running kanban board on your LAN.

## Prerequisites

| Component | Required | Check |
|-----------|----------|-------|
| Windows 11 | Yes | — |
| .NET 8 SDK | 8.0.x | `dotnet --version` |
| SQL Server | Any edition, local instance | `sqlcmd -S localhost -Q "SELECT 1"` |
| IIS | With ASP.NET Core Hosting Bundle for .NET 8 | `Get-WindowsFeature Web-Server` |
| Crush CLI | v0.68.0+ | `crush --version` |
| Git | Any recent version | `git --version` |

### Install missing pieces

**.NET 8 SDK** — download from https://dotnet.microsoft.com/download/dotnet/8.0.

**ASP.NET Core Hosting Bundle** — same download page, under "ASP.NET Core Runtime". After installing, restart IIS:

```powershell
iisreset
```

**SQL Server** — Express is sufficient. Download from https://www.microsoft.com/sql-server.

**Crush** — follow the install instructions for your environment. The Runner expects `crush` on the system PATH.

---

## 1. Clone and build

```powershell
git clone <repo-url> C:\repos\kanban-tracker
cd C:\repos\kanban-tracker
```

The repository redirects build output to a repo-relative `artifacts` directory via `Directory.Build.props`:

```xml
<ArtifactsPath>$(MSBuildThisFileDirectory)artifacts</ArtifactsPath>
```

```powershell
dotnet restore
dotnet build -c Release
```

---

## 2. Create the database

The connection string in `src/Kanban.Web/appsettings.json` uses Windows authentication:

```
Server=localhost;Database=KanbanBoard;Trusted_Connection=True;TrustServerCertificate=True
```

Ensure your account can create databases on the local SQL Server instance, then run the EF migration:

```powershell
dotnet ef database update --project src/Kanban.Core --startup-project src/Kanban.Web
```

This creates the `KanbanBoard` database with all tables (`Projects`, `Cards`, `CardNotes`, `Runs`, `RunLogLines`).

---

## 3. IIS site

Run all of this as administrator. See [deployment.md](deployment.md) for the full script.

Summary:

```powershell
Import-Module WebAdministration
New-WebAppPool -Name Kanban
Set-ItemProperty IIS:\AppPools\Kanban -Name managedRuntimeVersion -Value ''
New-Item -ItemType Directory -Force -Path C:\inetpub\kanban
New-Website -Name Kanban -Port 8080 -PhysicalPath C:\inetpub\kanban -ApplicationPool Kanban
```

### Grant the app pool database access

The app pool identity (`IIS APPPOOL\Kanban`) needs read/write to `KanbanBoard`:

```sql
CREATE LOGIN [IIS APPPOOL\Kanban] FROM WINDOWS;
USE KanbanBoard;
CREATE USER [IIS APPPOOL\Kanban] FOR LOGIN [IIS APPPOOL\Kanban];
ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\Kanban];
ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\Kanban];
```

### Open the firewall

```powershell
New-NetFirewallRule -DisplayName "Kanban 8080" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow
```

---

## 4. Runner service

The Runner is a Windows Service that polls the database for `Ready` cards and invokes Crush.

### Publish

```powershell
.\build\build.ps1 -Task Publish
```

### Install the service

```powershell
New-Item -ItemType Directory -Force -Path C:\Services\KanbanRunner
robocopy C:\repos\_artifacts\kanban\publish\Kanban.Runner C:\Services\KanbanRunner /MIR
New-Service -Name KanbanRunner `
            -DisplayName "Kanban Runner" `
            -BinaryPathName "C:\Services\KanbanRunner\Kanban.Runner.exe" `
            -StartupType Automatic
```

### Service account

The service runs as LocalSystem by default. It needs:

1. **A git identity** so commits are attributed:

   ```powershell
   git config --system user.name  "Kanban Runner"
   git config --system user.email "runner@localhost"
   ```

2. **Crush's OpenRouter key.** Crush reads its config from the service account's profile. Either:

   **Option A** — set the key at machine level so LocalSystem sees it:

   ```powershell
   [Environment]::SetEnvironmentVariable('OPENROUTER_API_KEY', '<key>', 'Machine')
   ```

   **Option B** — run the service as your own user (simpler, since Crush's config lives in your profile):

   ```powershell
   sc.exe config KanbanRunner obj= ".\YourUserName" password= "YourPassword"
   ```

   Restart the service after changing accounts or environment variables.

### Database access for the service

```sql
CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS;
USE KanbanBoard;
CREATE USER [NT AUTHORITY\SYSTEM] FOR LOGIN [NT AUTHORITY\SYSTEM];
ALTER ROLE db_datareader ADD MEMBER [NT AUTHORITY\SYSTEM];
ALTER ROLE db_datawriter ADD MEMBER [NT AUTHORITY\SYSTEM];
```

If you configured the service to run as your own user, grant that account instead.

### Start the service

```powershell
Start-Service KanbanRunner
```

---

## 5. Configure Crush

Crush's OpenRouter key and model live in `C:\Users\<user>\AppData\Local\crush\crush.json`. The Runner's `appsettings.json` only specifies the command and argument template:

```json
{
  "Runner": {
    "AgentCommand": "crush",
    "AgentArgumentTemplate": "run"
  }
}
```

The Runner invokes `crush run` and pipes the prompt via stdin. See [docs/crush-invocation.md](crush-invocation.md) for the verified command line.

---

## 6. Deploy

```powershell
.\build\build.ps1 -Task Deploy
```

This stops the IIS app pool, copies the Web output to `C:\inetpub\kanban`, restarts the pool, stops the Runner service, copies its output to `C:\Services\KanbanRunner`, and restarts the service.

---

## 7. Verify

1. Open `http://<host-name>:8080` from any LAN device.
2. Add a project: go to **Settings**, enter a name and a git repository path, save.
3. Create a card: give it a title, description, and select the project.
4. Drag the card to **Ready**.
5. Within a few seconds, the card should move to **In Progress**, then to **Review** with a summary.

### Check the Runner is working

```powershell
Get-Service KanbanRunner
# Expected: Running

Get-EventLog -LogName Application -Source "Kanban Runner" -Newest 20
```

---

## Troubleshooting

| Symptom | Check |
|---------|-------|
| HTTP 500.19 or 502.5 from IIS | ASP.NET Core Hosting Bundle not installed. Run `iisreset` after installing. |
| Cards stay in Ready | Runner service is stopped, or `crush` is not on the system PATH, or OpenRouter key is missing. |
| Runner fails to claim cards | Database access for the service account is missing. |
| Agent fails with "not a git repository" | The project path in the Projects table is wrong or not a git repo. |
| Agent fails with "working tree not clean" | Commit or stash changes in the project directory before queuing a card. |