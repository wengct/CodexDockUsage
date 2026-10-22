param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projPath = Join-Path $repoRoot "CodexDockUsage\CodexDockUsage.csproj"
$rid = if ($Platform -eq "ARM64") { "win-arm64" } else { "win-x64" }

Write-Host "=== [1/4] 正在停止舊版 CodexDockUsage 背景行程（若有執行中） ===" -ForegroundColor Cyan
Get-Process -Name "CodexDockUsage" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

Write-Host "`n=== [2/4] 正在建置 CodexDockUsage ($Configuration | $Platform) ===" -ForegroundColor Cyan
dotnet build $projPath -c $Configuration -p:Platform=$Platform
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet build 失敗，請檢查編譯錯誤。"
    exit 1
}

$manifestPath = Join-Path $repoRoot "CodexDockUsage\bin\$Platform\$Configuration\net10.0-windows10.0.26100.0\$rid\AppxManifest.xml"
if (-not (Test-Path $manifestPath)) {
    Write-Error "找不到產生的 AppxManifest.xml：$manifestPath"
    exit 1
}

Write-Host "`n=== [3/4] 正在向 Windows 註冊 Command Palette 擴充套件 ===" -ForegroundColor Cyan
try {
    Add-AppxPackage -Register $manifestPath -ForceApplicationShutdown
}
catch {
    if ($_.Exception.Message -match "0x80073CFF") {
        Write-Host "`n[需要開啟 Windows 開發人員模式]" -ForegroundColor Yellow
        Write-Host "Windows 預設需要開啟「開發人員模式」才能直接註冊本機開發的未簽署 AppxManifest.xml。" -ForegroundColor Yellow
        Write-Host "請在開啟的 Windows 設定視窗中，將「開發人員模式 (Developer Mode)」切換為開啟，然後重新執行本腳本：`n  .\install-extension.ps1" -ForegroundColor Yellow
        Start-Process "ms-settings:developers"
        exit 1
    }
    throw
}

Write-Host "`n=== [4/4] 正在重新啟動 Command Palette 以清除快取並載入最新圖示 ===" -ForegroundColor Cyan
$cmdPalProc = Get-Process -Name "Microsoft.CmdPal.UI" -ErrorAction SilentlyContinue
if ($null -ne $cmdPalProc) {
    $cmdPalProc | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Start-Process "explorer.exe" "shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App"
}

$pkg = Get-AppxPackage -Name "CodexDockUsage"
Write-Host "`n[OK] 安裝、註冊與重新載入成功！" -ForegroundColor Green
Write-Host "  套件名稱 : $($pkg.Name)"
Write-Host "  版本     : $($pkg.Version)"
Write-Host "  安裝路徑 : $($pkg.InstallLocation)"
