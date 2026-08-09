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
$packageJsonPath = Join-Path $repositoryRoot 'package.json'
$packageLockPath = Join-Path $repositoryRoot 'package-lock.json'
$pyprojectPath = Join-Path $repositoryRoot 'pyproject.toml'
$originalPackageJson = Get-Content -LiteralPath $packageJsonPath -Raw
$originalPackageLock = Get-Content -LiteralPath $packageLockPath -Raw
$originalPyproject = Get-Content -LiteralPath $pyprojectPath -Raw

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

try {
    Push-Location $repositoryRoot

    npm version $Version --no-git-tag-version --allow-same-version
    if ($LASTEXITCODE -ne 0) { throw 'npm version failed.' }
    npm pack --pack-destination $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'npm pack failed.' }

    $versionedPyproject = $originalPyproject -replace '(?m)^version = "[^"]+"\r?$', "version = `"$Version`""
    if ($versionedPyproject -eq $originalPyproject) {
        throw 'Could not inject the GitVersion value into pyproject.toml.'
    }
    Set-Content -LiteralPath $pyprojectPath -Value $versionedPyproject -NoNewline
    python -m build --sdist --wheel --outdir $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'Python packaging failed.' }

    dotnet pack bindings/csharp/Expressif.Syntax/Expressif.Syntax.csproj `
        --configuration Release `
        --output $outputPath `
        /p:Version=$Version `
        /p:PackageVersion=$Version
    if ($LASTEXITCODE -ne 0) { throw 'NuGet packaging failed.' }

    $nativeArchive = Join-Path $outputPath "tree-sitter-expressif-c-$Version.tar.gz"
    tar -czf $nativeArchive CMakeLists.txt bindings/c src/parser.c src/tree_sitter
    if ($LASTEXITCODE -ne 0) { throw 'Native parser packaging failed.' }
}
finally {
    Pop-Location
    Set-Content -LiteralPath $packageJsonPath -Value $originalPackageJson -NoNewline
    Set-Content -LiteralPath $packageLockPath -Value $originalPackageLock -NoNewline
    Set-Content -LiteralPath $pyprojectPath -Value $originalPyproject -NoNewline
}
