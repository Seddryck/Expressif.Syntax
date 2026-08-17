[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version,

    [Parameter(Mandatory)]
    [string] $NativeAssetsDirectory,

    [string] $OutputDirectory = 'artifacts'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'bindings/csharp/Expressif.Syntax/Expressif.Syntax.csproj'
$nativeAssetsPath = Join-Path $repositoryRoot $NativeAssetsDirectory
$outputPath = Join-Path $repositoryRoot $OutputDirectory

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$properties = @(
    "/p:Version=$Version"
    "/p:PackageVersion=$Version"
    "/p:NativeAssetsDirectory=$nativeAssetsPath"
)

dotnet restore $projectPath @properties
if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }

dotnet build $projectPath --configuration Release --no-restore @properties
if ($LASTEXITCODE -ne 0) { throw 'Managed assembly build failed.' }

dotnet pack $projectPath `
    --configuration Release `
    --output $outputPath `
    --no-build `
    --no-restore `
    @properties
if ($LASTEXITCODE -ne 0) { throw 'NuGet packaging failed.' }
