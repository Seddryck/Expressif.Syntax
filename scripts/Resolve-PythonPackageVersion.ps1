[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version,

    [switch] $RequirePep440
)

$ErrorActionPreference = 'Stop'

# Preserve GitVersion's representation when Python accepts it. Packaging tools may
# normalize the value in the generated package metadata, as permitted by PEP 440.
python -c 'from packaging.version import Version; Version(__import__("sys").argv[1])' $Version 2>$null
if ($LASTEXITCODE -eq 0) {
    $Version
    exit 0
}

if ($RequirePep440) {
    throw "GitVersion produced '$Version', which cannot be published as a PEP 440 version."
}

# Branch labels need not be PEP 440-compatible because CI never publishes them.
# Build the Python binding with a version derived from the same GitVersion core so
# packaging validation can still run, while retaining the full version elsewhere.
$coreVersion = ([regex]::Match($Version, '^\d+\.\d+\.\d+')).Value
Write-Warning "Using '$coreVersion.dev0' for the non-publishable Python branch package derived from GitVersion '$Version'."
"$coreVersion.dev0"
