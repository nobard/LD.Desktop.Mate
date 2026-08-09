param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$')]
    [string]$Version = '1.0.0',

    [ValidatePattern('^[0-9A-Za-z-]+$')]
    [string]$Channel = 'win',

    [string]$RepositoryUrl,

    [switch]$Prerelease
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$velopackVersion = '1.2.0'
$projectRoot = $PSScriptRoot
$projectPath = Join-Path $projectRoot 'Mate\Mate.csproj'
$publishProfile = Join-Path $projectRoot 'Mate\Properties\PublishProfiles\win-x64.pubxml'
$publishDirectory = Join-Path $projectRoot 'artifacts\publish'
$releaseDirectory = Join-Path $projectRoot 'artifacts\velopack'
$iconPath = Join-Path $projectRoot 'Mate\Assets\MateTray.ico'

foreach ($directory in @($publishDirectory, $releaseDirectory)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

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

if (-not [string]::IsNullOrWhiteSpace($RepositoryUrl)) {
    Write-Host "Downloading the previous $Channel release for delta generation..."
    $downloadArguments = @(
        'vpk', '--version', $velopackVersion,
        'download', 'github',
        '--repoUrl', $RepositoryUrl,
        '--outputDir', $releaseDirectory,
        '--channel', $Channel
    )
    if ($Prerelease) {
        $downloadArguments += '--pre'
    }

    & dnx @downloadArguments
    if ($LASTEXITCODE -ne 0) {
        Write-Warning 'No previous release was downloaded. A full update package will be created.'
    }
}

Write-Host "Packing Velopack channel $Channel..."
& dnx vpk --version $velopackVersion pack `
    --packId LD.Desktop.Mate `
    --packVersion $Version `
    --packDir $publishDirectory `
    --outputDir $releaseDirectory `
    --channel $Channel `
    --runtime win-x64 `
    --mainExe Mate.exe `
    --packTitle Mate `
    --packAuthors 'LD Desktop' `
    --icon $iconPath `
    --framework net10-x64-desktop `
    --shortcuts StartMenuRoot `
    --noPortable

if ($LASTEXITCODE -ne 0) {
    throw "Velopack packaging failed with exit code $LASTEXITCODE."
}

Write-Host "Done: $releaseDirectory"
