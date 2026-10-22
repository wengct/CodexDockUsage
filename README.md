# Codex Dock Usage Monitor — Windows C# (.NET 10) 專案

本專案採用 **Windows 原生 Codex CLI + C# (.NET 10) + PowerToys Command Palette Dock + VS Code**，不需要 WSL、Microsoft Store，也不需要額外建立 OpenAI API Key。

透過本機既有的 `codex` 登入狀態，直接以 `stdio` 啟動並常駐連線 `codex app-server`，即時呈現 **5 小時額度**、**每週額度**、**Credits 餘額** 與 **Token 使用統計**。若 Codex 未回傳某項欄位，將明確顯示 `N/A`，不會自行推算點數。

---

## 官方介面支援對應

| 功能 | 官方介面 |
| :--- | :--- |
| 取得 Codex 額度 | `account/rateLimits/read` |
| 取得 Token 統計 | `account/usage/read` |
| 取得額度變更通知 | `account/rateLimits/updated` |
| 建立 Dock Band | `CommandProvider.GetDockBands()` |
| 點擊 Dock 展開詳細資訊 | `IContentPage` (`UsagePage`) |

---

## 專案結構

- `CodexDockUsage.sln`：完整 Visual Studio / VS Code 解決方案（目標框架：**`.NET 10`**）
- `CodexDockUsage/`：完整的 **PowerToys Command Palette Dock Extension** 專案（含 MSIX 資訊清單、COM Server 註冊、常駐背景輪詢與通知監聽服務）
  - `Services/CodexAppServerClient.cs`：自動解析本機原生 `codex.exe` / `codex.cmd`，維護 `codex app-server --listen stdio://` 連線與 `account/rateLimits/updated` 即時通知
  - `Services/CodexUsageService.cs`：背景定時輪詢（預設 60 秒）、即時事件通知與快取管理
  - `Models/UsageSnapshot.cs`：完整解析 5 小時額度、每週額度、Credits 餘額、免費重置額度（Reset Credits）與 Token 統計
  - `Pages/UsagePage.cs`：點擊 Dock 後展開的 `IContentPage` Markdown 詳細資訊面板，內建「立即重新整理額度」與「複製 Markdown 報告」指令
  - `CodexDockUsageCommandsProvider.cs`：實作 `TopLevelCommands()` 與 `GetDockBands()`，動態更新 Dock 上的剩餘百分比與 Credits 摘要
- `Probe/`：獨立的 `.NET 10` 命令列測試與診斷工具（共用相同的 `CodexAppServerClient` 與 `UsageParser`）
- `ExtensionOverlay/`：與 `CodexDockUsage` 同步的擴充功能核心程式碼（方便套用至其他自訂 Command Palette 範本專案）

---

## 1. 驗證本機 Codex App Server 與資料解析 (`Probe`)

在 VS Code 的 PowerShell 終端機執行：

```powershell
# 直接執行 Probe（會自動尋找本機的 codex.exe / codex.cmd）
dotnet run --project Probe

# 若只需檢視解析後的 Dock 標題與 Markdown 面板預覽（不印出原始長篇 JSON）：
dotnet run --project Probe -- --no-raw
```

`Probe` 會：
1. 自動解析並啟動本機 Windows 原生 `codex app-server --listen stdio://`。
2. 完成 `initialize` / `initialized` 交握。
3. 查詢 `account/rateLimits/read`（Codex 額度與 Credits）與 `account/usage/read`（Token 統計）。
4. 輸出 Dock Band 標題預覽與展開後的 Markdown 面板內容。

> **注意：** 請勿公開分享 `Probe` 輸出的原始 JSON（內含 `accountId` 等帳號資訊）。

---

## 2. 建置與安裝 Command Palette Dock Extension

詳細圖文步驟請參考 [**教學手冊.md**](./教學手冊.md)。

### 快速一鍵安裝（需開啟 Windows 開發人員模式）

1. 確認已開啟 Windows **開發人員模式**（`設定 > 系統 > 開發人員專用 > 開發人員模式`）。
2. 在專案根目錄執行一鍵安裝腳本：
   ```powershell
   .\install-extension.ps1
   ```
3. 開啟 **PowerToys Command Palette**（`Win + Alt + Space`），輸入 `Reload` 並執行 **Reload Command Palette extensions**。
4. 在 Command Palette 設定中開啟 **Enable Dock**，並將 **Codex Usage** 釘選（Pin to Dock）至螢幕邊緣的 Dock 工具列。
5. 若日後欲移除擴充套件，可執行：
   ```powershell
   .\uninstall-extension.ps1
   ```

---

## 3. 除錯方式

- **資料與協定除錯**：直接執行 `dotnet run --project Probe`。
- **擴充功能除錯**：由於 Command Palette 擴充功能是以 MSIX 封裝的 Out-of-process COM Server 執行（透過 `-RegisterProcessAsComServer` 啟動），請先部署 MSIX 並由 Command Palette 觸發啟動 `CodexDockUsage.exe`，再使用 VS Code 或 Visual Studio 將 C# 除錯器附加（Attach）至 `CodexDockUsage.exe` 行程。

---

## 參考資料

- https://learn.chatgpt.com/docs/app-server
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/adding-dock-support
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
