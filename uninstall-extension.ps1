$ErrorActionPreference = "Stop"

Write-Host "正在停止執行中的 CodexDockUsage 行程..." -ForegroundColor Cyan
Get-Process -Name "CodexDockUsage" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$pkg = Get-AppxPackage -Name "CodexDockUsage" -ErrorAction SilentlyContinue
if ($null -eq $pkg) {
    Write-Host "目前系統並未安裝 CodexDockUsage 擴充套件。" -ForegroundColor Yellow
    exit 0
}

foreach ($item in $pkg) {
    Write-Host "正在移除套件: $($item.PackageFullName) ..." -ForegroundColor Cyan
    Remove-AppxPackage -Package $item.PackageFullName
}

Write-Host "✅ 已成功解除安裝 CodexDockUsage！請在 Command Palette 中執行 Reload 以更新清單。" -ForegroundColor Green
