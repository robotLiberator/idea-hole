param([switch]$WithoutDebugUi)
$ErrorActionPreference = 'Stop'
$frameworkPath = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$webViewVersion = '1.0.4191.47'
$webViewPackage = Join-Path $PSScriptRoot ('.packages\Microsoft.Web.WebView2.' + $webViewVersion)
if (-not (Test-Path -LiteralPath $webViewPackage)) {
    $packagesFolder = Join-Path $PSScriptRoot '.packages'
    $packageArchive = Join-Path $packagesFolder ('Microsoft.Web.WebView2.' + $webViewVersion + '.zip')
    New-Item -ItemType Directory -Path $packagesFolder -Force | Out-Null
    Write-Host "Restoring Microsoft.Web.WebView2 $webViewVersion..."
    Invoke-WebRequest -Uri ('https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/' + $webViewVersion) -OutFile $packageArchive
    Expand-Archive -LiteralPath $packageArchive -DestinationPath $webViewPackage -Force
    Remove-Item -LiteralPath $packageArchive -Force
}
$references = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Web.Extensions.dll', 'System.Xml.dll', 'System.Xml.Linq.dll', 'System.Xaml.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkPath $_) }
$references += '/reference:' + (Join-Path $webViewPackage 'lib\net462\Microsoft.Web.WebView2.Core.dll')
$references += '/reference:' + (Join-Path $webViewPackage 'lib\net462\Microsoft.Web.WebView2.Wpf.dll')
$sources = @("$PSScriptRoot\DesktopAppHost.cs", "$PSScriptRoot\WebWorkspace.cs", "$PSScriptRoot\PinnedNoteWindow.cs", "$PSScriptRoot\NotesStore.cs", "$PSScriptRoot\TaskData.cs", "$PSScriptRoot\TaskController.cs", "$PSScriptRoot\MonthlyArchive.cs")
$sources += "$PSScriptRoot\NoteImages.cs"
$sources += "$PSScriptRoot\DailyJournal.cs"
& (Join-Path $frameworkPath 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ /out:"$PSScriptRoot\DesktopGrowth.exe" $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item -LiteralPath (Join-Path $webViewPackage 'lib\net462\Microsoft.Web.WebView2.Core.dll') -Destination $PSScriptRoot -Force
Copy-Item -LiteralPath (Join-Path $webViewPackage 'lib\net462\Microsoft.Web.WebView2.Wpf.dll') -Destination $PSScriptRoot -Force
Copy-Item -LiteralPath (Join-Path $webViewPackage 'runtimes\win-x64\native\WebView2Loader.dll') -Destination $PSScriptRoot -Force
