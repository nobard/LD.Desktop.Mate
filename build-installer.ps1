param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = $PSScriptRoot
$projectPath = Join-Path $projectRoot 'Mate\Mate.csproj'
$publishProfile = Join-Path $projectRoot 'Mate\Properties\PublishProfiles\win-x64.pubxml'
$publishDirectory = Join-Path $projectRoot 'artifacts\publish'
$installerScript = Join-Path $projectRoot 'installer\Mate.iss'
$installerDirectory = Join-Path $projectRoot 'artifacts\installer'

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null

Write-Host "Publishing Mate $Version..."
& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $publishDirectory `
    -p:PublishProfile=$publishProfile `
    -p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    throw "Publishing failed with exit code $LASTEXITCODE."
}

$innoSetupCandidates = @(
    (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Inno Setup 6\ISCC.exe'),
    (Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Inno Setup 6\ISCC.exe')
)

$isccPath = $innoSetupCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

if (-not $isccPath) {
    $isccCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($isccCommand) {
        $isccPath = $isccCommand.Source
    }
}

if (-not $isccPath) {
    throw @"
Inno Setup was not found. Install it from https://jrsoftware.org/isdl.php,
then run .\build-installer.ps1 again.
"@
}

Write-Host 'Building installer...'
& $isccPath "/DMyAppVersion=$Version" $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Installer build failed with exit code $LASTEXITCODE."
}

$setupPath = Join-Path $installerDirectory "Mate-Setup-$Version.exe"
Write-Host "Done: $setupPath"
