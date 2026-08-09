[CmdletBinding()]
Param(
    [Parameter(Position = 0, Mandatory = $false, ValueFromRemainingArguments = $true)]
    [string[]]$BuildArguments
)

Write-Output "PowerShell $($PSVersionTable.PSEdition) version $($PSVersionTable.PSVersion)"

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"
$ConfirmPreference = "None"
trap {
    Write-Error $_ -ErrorAction Continue
    exit 1
}

$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent
$BuildProjectFile = "$PSScriptRoot\build\_build.csproj"
$TempDirectory = "$PSScriptRoot\.nuke\temp"
$DotNetGlobalFile = "$PSScriptRoot\global.json"
$DotNetInstallUrl = "https://dot.net/v1/dotnet-install.ps1"
$DotNetChannel = "STS"

$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:DOTNET_MULTILEVEL_LOOKUP = 0
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 1
$env:NUKE_TELEMETRY_OPTOUT = 1

function Invoke-Checked([scriptblock] $Command) {
    & $Command
    if ($LASTEXITCODE) {
        exit $LASTEXITCODE
    }
}

if ($null -ne (Get-Command "dotnet" -ErrorAction SilentlyContinue) -and
    $(dotnet --version) -and
    $LASTEXITCODE -eq 0) {
    $env:DOTNET_EXE = (Get-Command "dotnet").Path
}
else {
    $DotNetInstallFile = "$TempDirectory\dotnet-install.ps1"
    New-Item -ItemType Directory -Path $TempDirectory -Force | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    (New-Object System.Net.WebClient).DownloadFile($DotNetInstallUrl, $DotNetInstallFile)

    if (Test-Path $DotNetGlobalFile) {
        $DotNetGlobal = Get-Content $DotNetGlobalFile | Out-String | ConvertFrom-Json
        if ($DotNetGlobal.PSObject.Properties["sdk"] -and
            $DotNetGlobal.sdk.PSObject.Properties["version"]) {
            $DotNetVersion = $DotNetGlobal.sdk.version
        }
    }

    $DotNetDirectory = "$TempDirectory\dotnet-win"
    if (Test-Path variable:DotNetVersion) {
        Invoke-Checked {
            & powershell $DotNetInstallFile -InstallDir $DotNetDirectory -Version $DotNetVersion -NoPath
        }
    }
    else {
        Invoke-Checked {
            & powershell $DotNetInstallFile -InstallDir $DotNetDirectory -Channel $DotNetChannel -NoPath
        }
    }

    $env:DOTNET_EXE = "$DotNetDirectory\dotnet.exe"
}

Write-Output "Microsoft (R) .NET SDK version $(& $env:DOTNET_EXE --version)"

Invoke-Checked {
    & $env:DOTNET_EXE build $BuildProjectFile --disable-build-servers --property:UseSharedCompilation=false --nologo --verbosity quiet
}
Invoke-Checked {
    & $env:DOTNET_EXE run --project $BuildProjectFile --no-build -- $BuildArguments
}
