$ErrorActionPreference = 'Stop'

$appPath = Join-Path $PSScriptRoot 'DesktopGrowth.exe'
if (-not (Test-Path -LiteralPath $appPath -PathType Leaf)) {
    throw "Application not found: $appPath"
}

$command = '"' + $appPath + '"'
$runKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
if ($null -eq $runKey) { throw 'Unable to open the current-user Run key.' }
try {
    $runKey.SetValue('DesktopGrowth', $command, [Microsoft.Win32.RegistryValueKind]::String)
} finally {
    $runKey.Dispose()
}

Write-Host "DesktopGrowth autostart enabled: $command"
