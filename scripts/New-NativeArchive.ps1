[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version,

    [string] $OutputDirectory = 'artifacts'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $repositoryRoot $OutputDirectory
$archivePath = Join-Path $outputPath "tree-sitter-expressif-c-$Version.tar.gz"

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

Push-Location $repositoryRoot
try {
    tar -czf $archivePath CMakeLists.txt bindings/c src/parser.c src/tree_sitter
    if ($LASTEXITCODE -ne 0) { throw 'Native parser packaging failed.' }
}
finally {
    Pop-Location
}
