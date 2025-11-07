param(
  [Parameter(Mandatory=$true)][string]$MsiPath,
  [switch]$SkipProcessKill
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $MsiPath)) { Write-Error "MSI path not found: $MsiPath"; exit 1 }

$displayName = 'ComingUpNext Tray'
Write-Host "Looking for existing installation of '$displayName'..."
$productCode = $null
$uninstallRoots = @(
  'HKCU:SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
  'HKLM:SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
  'HKLM:SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
)
foreach ($root in $uninstallRoots) {
  Get-ChildItem $root -ErrorAction SilentlyContinue | ForEach-Object {
    try { $p = Get-ItemProperty $_.PsPath } catch { $p = $null }
    if ($p -and $p.DisplayName -eq $displayName -and $p.PSChildName) {
  $productCode = $p.PSChildName
  if ($productCode) { Write-Host "Discovered product code: $productCode" }
    }
  }
}

if ($productCode) {
  Write-Host "Found existing product code: $productCode. Preparing uninstall..."
  if (-not $SkipProcessKill) {
    Write-Host 'Attempting to terminate running ComingUpNextTray processes...'
    Get-Process -Name 'ComingUpNextTray' -ErrorAction SilentlyContinue | ForEach-Object {
      try {
        $_.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 400
        if (-not $_.HasExited) { $_.Kill() }
        Write-Host "Stopped process Id=$($_.Id)"
      } catch {
        Write-Warning "Could not stop process Id=$($_.Id): $($_.Exception.Message)"
      }
    }
  } else {
    Write-Host 'SkipProcessKill set: not terminating existing processes.'
  }

  # Do not create uninstall log files. Run msiexec without verbose logging to avoid temporary log files.
  $uninstallParams = @('/x', $productCode, '/qn')
  Write-Host "Using product code for uninstall: $productCode"
  Write-Host "Running: msiexec.exe $($uninstallParams -join ' ')"
  $proc = Start-Process msiexec.exe -Wait -PassThru -ArgumentList $uninstallParams
  if ($proc.ExitCode -ne 0) {
    if ($proc.ExitCode -eq 1603) {
      Write-Warning 'Uninstall returned 1603. Continuing with install (old per-machine install may require manual removal or elevation).'
    } else {
      Write-Error "Uninstall failed with exit code $($proc.ExitCode). No uninstall log is created by this script. To capture detailed MSI logs, run the uninstall manually with msiexec /l*v <path>"
      exit $proc.ExitCode
    }
  } else {
    Write-Host 'Previous version uninstalled.'
  }
  Write-Host 'Previous version uninstalled.'
} else {
  Write-Host 'No previous installation found.'
}

Write-Host "Installing new MSI: $MsiPath (verbose log enabled)"
$msiLog = Join-Path ([IO.Path]::GetDirectoryName($MsiPath)) 'install.log'
$installArgs = @('/i', $MsiPath, '/qn', '/l*v', $msiLog)
$proc2 = Start-Process msiexec.exe -Wait -PassThru -ArgumentList $installArgs
if ($proc2.ExitCode -ne 0) {
  Write-Error "Install failed with exit code $($proc2.ExitCode)"
  if ($proc2.ExitCode -eq 1603) {
    Write-Warning 'MSI install error 1603: verify no running instances, adequate permissions, and check the log for detailed failure.'
    Write-Host "Install log: $msiLog"
  }
  exit $proc2.ExitCode
}

Write-Host 'MSI installed successfully.'

# Attempt to start the installed application (retry if immediately unavailable).
function Invoke-LaunchApp {
  param([string]$Path)
  if (Test-Path $Path) {
    Write-Host "Launching application: $Path"
    Start-Process -FilePath $Path | Out-Null
    return $true
  }
  return $false
}

$candidateExePaths = @()
$candidateExePaths += (Join-Path $Env:LocalAppData 'ComingUpNext\ComingUpNextTray.exe')
$candidateExePaths += (Join-Path $Env:ProgramFiles 'ComingUpNext\ComingUpNextTray.exe')
if ($Env:ProgramFiles -and $Env:ProgramFiles -ne $Env:ProgramFiles) { }
# Safely access ProgramFiles(x86) with ${} syntax
$pf86 = ${Env:ProgramFiles(x86)}
if ($pf86) { $candidateExePaths += (Join-Path $pf86 'ComingUpNext\ComingUpNextTray.exe') }

# Probe registry InstallLocation
$installedKey = Get-ChildItem 'HKCU:SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue | ForEach-Object { try { Get-ItemProperty $_.PsPath } catch { $null } } | Where-Object { $_.DisplayName -eq $displayName }
if ($installedKey -and $installedKey.InstallLocation) {
  $candidateExePaths += (Join-Path $installedKey.InstallLocation 'ComingUpNextTray.exe')
}

Write-Host 'Probing for installed executable...'
$launched = $false
foreach ($path in ($candidateExePaths | Select-Object -Unique)) {
  Write-Host "Checking: $path"
  if (Invoke-LaunchApp -Path $path) { $launched = $true; break }
}
if (-not $launched) {
  Start-Sleep -Seconds 2
  foreach ($path in ($candidateExePaths | Select-Object -Unique)) {
    if (Invoke-LaunchApp -Path $path) { $launched = $true; break }
  }
}
if (-not $launched) {
  Write-Warning 'Could not locate executable to launch.'
  Write-Host "Consult verbose MSI log at: $msiLog"
}
exit 0
