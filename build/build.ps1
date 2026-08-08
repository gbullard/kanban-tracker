[CmdletBinding()]
param(
    [ValidateSet('Clean','Restore','Build','Test','Publish','Deploy','All')]
    [string] $Task = 'All',

    [string] $Configuration = 'Release',
    [string] $ArtifactsPath = '',
    [string] $SitePath      = '',
    [string] $AppPoolName   = 'Kanban',
    [string] $RunnerPath    = '',
    [string] $RunnerService = 'KanbanRunner'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

if (-not $PSBoundParameters.ContainsKey('ArtifactsPath')) { $ArtifactsPath = Join-Path $repo 'artifacts' }
$publishWeb = Join-Path $ArtifactsPath 'publish\Kanban.Web'
$publishRunner = Join-Path $ArtifactsPath 'publish\Kanban.Runner'

function Invoke-Step([string] $name, [scriptblock] $body) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $body
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
        throw "$name failed with exit code $LASTEXITCODE"
    }
}

function Step-Clean {
    Invoke-Step 'Clean' {
        if (Test-Path $ArtifactsPath) { Remove-Item $ArtifactsPath -Recurse -Force }
        $global:LASTEXITCODE = 0
    }
}

function Step-Restore { Invoke-Step 'Restore' { dotnet restore (Join-Path $repo 'Kanban.sln') } }

function Step-Build {
    Invoke-Step 'Build' { dotnet build (Join-Path $repo 'Kanban.sln') -c $Configuration --no-restore }
}

function Step-Test {
    Invoke-Step 'Test' { dotnet test (Join-Path $repo 'Kanban.sln') -c $Configuration --no-build }
}

function Step-Publish {
    Invoke-Step 'Publish Kanban.Web' {
        dotnet publish (Join-Path $repo 'src\Kanban.Web\Kanban.Web.csproj') `
            -c $Configuration --no-build -o $publishWeb
    }
    Invoke-Step 'Publish Kanban.Runner' {
        dotnet publish (Join-Path $repo 'src\Kanban.Runner\Kanban.Runner.csproj') `
            -c $Configuration --no-build -o $publishRunner
    }
}

function Step-Deploy {
    Invoke-Step 'Deploy Kanban.Web' {
        Import-Module WebAdministration

        if (Test-Path "IIS:\AppPools\$AppPoolName") {
            if ((Get-WebAppPoolState $AppPoolName).Value -ne 'Stopped') {
                Stop-WebAppPool $AppPoolName
                # The worker process holds a lock on the DLLs until it actually exits.
                while ((Get-WebAppPoolState $AppPoolName).Value -ne 'Stopped') { Start-Sleep -Milliseconds 250 }
            }
        }

        New-Item -ItemType Directory -Force -Path $SitePath | Out-Null
        robocopy $publishWeb $SitePath /MIR /NJH /NJS /NP /NDL | Out-Null
        # robocopy uses exit codes 0-7 for success.
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
        $global:LASTEXITCODE = 0

        if (Test-Path "IIS:\AppPools\$AppPoolName") { Start-WebAppPool $AppPoolName }
    }
}

function Step-DeployRunner {
    Invoke-Step 'Deploy Kanban.Runner' {
        $service = Get-Service -Name $RunnerService -ErrorAction SilentlyContinue

        if ($service -and $service.Status -ne 'Stopped') {
            Stop-Service -Name $RunnerService
            # The service holds its DLLs open until the process actually exits.
            (Get-Service $RunnerService).WaitForStatus('Stopped', '00:00:30')
        }

        New-Item -ItemType Directory -Force -Path $RunnerPath | Out-Null
        robocopy $publishRunner $RunnerPath /MIR /NJH /NJS /NP /NDL | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
        $global:LASTEXITCODE = 0

        if ($service) { Start-Service -Name $RunnerService }
        else { Write-Warning "Service '$RunnerService' is not installed. See docs/deployment.md." }
    }
}

switch ($Task) {
    'Clean'   { Step-Clean }
    'Restore' { Step-Restore }
    'Build'   { Step-Restore; Step-Build }
    'Test'    { Step-Restore; Step-Build; Step-Test }
    'Publish' { Step-Restore; Step-Build; Step-Test; Step-Publish }
    'Deploy'  { Step-Deploy; Step-DeployRunner }
    'All'     { Step-Clean; Step-Restore; Step-Build; Step-Test; Step-Publish; Step-Deploy; Step-DeployRunner }
}

Write-Host "Done: $Task" -ForegroundColor Green