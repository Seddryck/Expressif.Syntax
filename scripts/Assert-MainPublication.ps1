[CmdletBinding()]
param(
    [string] $GitRef = $env:GITHUB_REF
)

$ErrorActionPreference = 'Stop'

if ($GitRef -ne 'refs/heads/main') {
    throw "Package publication is restricted to main; received Git ref '$GitRef'."
}

Write-Output 'Main-branch publication policy satisfied.'
