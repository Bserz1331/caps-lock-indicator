# Caps Lock 右下角指示器

這是一個免安裝的 Windows `.exe` 小工具，不需要 PowerShell 或 .NET SDK。

## 先開哪個？

- 要使用工具：雙擊根目錄的 `CapsLockIndicator.exe`
- 另一種啟動方式：雙擊 `啟動 Caps Lock 指示器.bat`
- 其他檔案是原始碼、啟動腳本與圖示，平常不需要開啟

## 資料夾結構

```text
CapsLock/
├─ CapsLockIndicator.exe              ← 主程式
├─ 啟動 Caps Lock 指示器.bat          ← 快速啟動
├─ README.md                           ← 本說明
├─ src/CapsLockIndicator.cs            ← 原始碼
├─ scripts/CapsLockIndicator.ps1       ← PowerShell 相容啟動腳本
└─ assets/                             ← 圖示與預覽圖
```

## 使用方式

雙擊 `CapsLockIndicator.exe`，或雙擊 `啟動 Caps Lock 指示器.bat`。

- 程式只顯示在 Windows 工作列右下角的通知區，不會出現浮動視窗
- 通知區圖示右下角的圓點：綠色代表 Caps Lock 開啟，灰色代表關閉
- 將滑鼠移到圖示上，可看到「Caps Lock：開啟／關閉」提示
- 在通知區圖示上按右鍵，可重新整理狀態或結束工具
- 在通知區圖示上按右鍵，勾選「開機啟動」即可讓工具在登入 Windows 後自動執行；再次點擊可取消
- 如果圖示被 Windows 收進 `^` 隱藏圖示區，可將它拖曳到工作列上

「開機啟動」只會設定目前 Windows 使用者，不需要管理員權限。

如果 Windows 顯示 SmartScreen 提示，請先選擇「其他資訊」，再選擇「仍要執行」。這是未經數位簽章的本機工具常見提示。

