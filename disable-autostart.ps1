$ErrorActionPreference = 'Stop'

$runKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run', $true)
if ($null -ne $runKey) {
    try {
        $runKey.DeleteValue('DesktopGrowth', $false)
    } finally {
        $runKey.Dispose()
    }
}

Write-Host 'DesktopGrowth autostart disabled.'
