[CmdletBinding()]
param(
    [ValidateSet('Clean','Restore','Build','Test','Publish','Deploy','All')]
    [string] $Task = 'All',

    [string] $Configuration = 'Release',
    [string] $ArtifactsPath = 'C:\repos\_artifacts\kanban',
    [string] $SitePath      = 'C:\inetpub\kanban',
    [string] $AppPoolName   = 'Kanban'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$publishWeb = Join-Path $ArtifactsPath 'publish\Kanban.Web'

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

switch ($Task) {
    'Clean'   { Step-Clean }
    'Restore' { Step-Restore }
    'Build'   { Step-Restore; Step-Build }
    'Test'    { Step-Restore; Step-Build; Step-Test }
    'Publish' { Step-Restore; Step-Build; Step-Test; Step-Publish }
    'Deploy'  { Step-Deploy }
    'All'     { Step-Clean; Step-Restore; Step-Build; Step-Test; Step-Publish; Step-Deploy }
}

Write-Host "Done: $Task" -ForegroundColor Green