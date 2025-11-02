param(
  [string]$Configuration = 'Release',
  [string]$Runtime = 'win-x64',
  [string]$Version
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..' 'src' 'ComingUpNextTray' 'ComingUpNextTray.csproj'
if (!(Test-Path $project)) { Write-Error "Project file not found: $project"; exit 1 }

# Determine version if not supplied
if (-not $Version) {
  Write-Host 'Reading version from csproj...'
  [xml]$csprojXml = Get-Content $project
  $Version = $csprojXml.Project.PropertyGroup.Version
  if (-not $Version) { Write-Error 'Version not specified in csproj and not passed as parameter.'; exit 1 }
  Write-Host "Detected version: $Version"
}

# Trim the version string to remove any trailing spaces
$Version = $Version.Trim()

$publishDir = Join-Path $PSScriptRoot 'publish'
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

# Log the publish directory for debugging
Write-Host "Publish directory: $publishDir"

Write-Host "Publishing .NET app..."
# Log the output of dotnet publish
Write-Host "Running dotnet publish..."
$publishOutput = dotnet publish $project -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true /p:AssemblyVersion=$Version /p:FileVersion=$Version /p:Version=$Version -o $publishDir 2>&1
Write-Host "dotnet publish output: $publishOutput"

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
  Write-Host 'Installing WiX toolset (v5)...'
  dotnet tool install --global wix --version 5.*
  $env:PATH += ";$env:USERPROFILE\.dotnet\tools"
}

$exePath = Join-Path $publishDir 'ComingUpNextTray.exe'
if (!(Test-Path $exePath)) { Write-Error "Executable not found: $exePath"; exit 1 }

Write-Host "Writing Harvest.wxs..."
$harvestFragment = Join-Path $PSScriptRoot 'Harvest.wxs'
@"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <ComponentGroup Id="MainComponents">
      <Component Id="Cmp_Exe" Guid="*" Directory="INSTALLFOLDER">
        <File Id="ComingUpNextExe" Source="$exePath" KeyPath="yes" />
      </Component>
    </ComponentGroup>
  </Fragment>
</Wix>
"@ | Set-Content -Path $harvestFragment -Encoding UTF8

Write-Host "Building MSI..."
# Log the output of wix build
Write-Host "Running wix build..."
$wixOutput = wix build -d Version=$Version "$PSScriptRoot\Product.wxs" "$harvestFragment" -o "$publishDir\ComingUpNextTray-$Version.msi" 2>&1
Write-Host "wix build output: $wixOutput"
Write-Host "Done: $publishDir\ComingUpNextTray-$Version.msi"
