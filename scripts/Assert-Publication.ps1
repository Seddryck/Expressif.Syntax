[CmdletBinding()]
param(
    [string] $GitRef = $env:GITHUB_REF,
    [string] $Version = $env:VERSION
)

$ErrorActionPreference = 'Stop'

switch ($GitRef) {
    'refs/heads/main' {
        Write-Output 'Main-branch publication policy satisfied.'
        exit 0
    }
    'refs/heads/feat/v1.0' {
        if ($Version -notmatch '^1\.0\.0-beta\.[1-9][0-9]*$') {
            throw "Beta publication requires a 1.0.0-beta.N version; received '$Version'."
        }
        Write-Output "V1 beta publication policy satisfied for version '$Version'."
        exit 0
    }
    default {
        throw "Package publication is restricted to main and feat/v1.0; received Git ref '$GitRef'."
    }
}
