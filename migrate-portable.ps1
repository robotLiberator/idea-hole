$ErrorActionPreference = 'Stop'
if (Get-Process DesktopGrowth -ErrorAction SilentlyContinue) { throw '请先关闭成长桌面，保证记录已保存。' }
$legacyRoot = Join-Path $env:LOCALAPPDATA 'DesktopGrowth'
$portableRoot = Join-Path $PSScriptRoot '数据'
# Explicit one-time migration. Never used by application startup or overwrite newer data.
function Copy-VerifiedFile($source, $target) {
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { return }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    if (Test-Path -LiteralPath $target) {
        if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw "目标已存在且内容不同，未覆盖：$target" }
    } else { Copy-Item -LiteralPath $source -Destination $target }
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw "校验失败：$target" }
    Write-Output "已校验：$target"
}
Copy-VerifiedFile (Join-Path $legacyRoot '灵感便签.md') (Join-Path $portableRoot '灵感池\灵感便签.md')
Copy-VerifiedFile (Join-Path $legacyRoot 'state.json') (Join-Path $portableRoot '日程\state.json')
foreach ($mapping in @(@('便签图片','灵感池\便签图片'), @('月度记录','日程\月度记录'))) {
    $sourceDir = Join-Path $legacyRoot $mapping[0]
    $targetDir = Join-Path $portableRoot $mapping[1]
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    if (Test-Path -LiteralPath $sourceDir) {
        Get-ChildItem -LiteralPath $sourceDir -File -Recurse | ForEach-Object {
            Copy-VerifiedFile $_.FullName (Join-Path $targetDir $_.FullName.Substring($sourceDir.Length + 1))
        }
    }
}
Write-Output '迁移完成。旧目录未删除，仅保留作备份；新版不再读取它。'
