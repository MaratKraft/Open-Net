param([string]$SdkPath = 'dotnet', [string]$OutputDirectory = '', [string]$NugetCache = '', [switch]$TestBuild, [switch]$SafePreview)
$ErrorActionPreference = 'Stop'
if ($SafePreview) { $TestBuild = [switch]::new($true) }
$appRoot = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = if ($SafePreview) { 'publish/Open Net Safe Preview' } elseif ($TestBuild) { 'publish/Open Net 0.4.0-alpha.1 Test' } else { 'publish/Open Net 0.4.0-alpha.1' } }
if (![IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $appRoot $OutputDirectory }
$env:DOTNET_CLI_HOME = Join-Path $appRoot 'tools/cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TO_PATH = 'false'
$env:DOTNET_NOLOGO = 'true'
if ($NugetCache) { $env:NUGET_PACKAGES = [IO.Path]::GetFullPath($NugetCache) }
Push-Location $appRoot
try {
    & $SdkPath publish src/NetworkHub.Desktop/NetworkHub.Desktop.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.12 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false -p:TestBuild=$($TestBuild.IsPresent.ToString().ToLowerInvariant()) -p:SafePreview=$($SafePreview.IsPresent.ToString().ToLowerInvariant()) -p:EnableCompressionInSingleFile=true -o $OutputDirectory --configfile NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
} finally { Pop-Location }
