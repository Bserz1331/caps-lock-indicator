# Caps Lock 右下角指示器

這是一個免安裝的 Windows `.exe` 小工具，不需要 PowerShell 或 .NET SDK。程式支援繁體中文與英文，預設會依照 Windows 顯示語言選擇介面。

## 先開哪個？

- 要使用工具：雙擊根目錄的 `CapsLockIndicator.exe`
- 英文檔名啟動方式：雙擊 `Start Caps Lock Indicator.bat`
- 原本的啟動方式仍保留：雙擊 `啟動 Caps Lock 指示器.bat`
- 其他檔案是原始碼、啟動腳本與圖示，平常不需要開啟

## 使用方式

雙擊 `CapsLockIndicator.exe`，或雙擊任一個啟動批次檔。

- 平時只顯示在 Windows 工作列右下角的通知區；按下 Caps Lock 時會在目前螢幕中央顯示狀態提示
- 通知區圖示顯示大寫 `A`（綠底）代表 Caps Lock 開啟，小寫 `a`（灰底）代表關閉
- 中央狀態提示預設為 80% 透明（視窗不透明度約 20%），固定在最前面，預設 3 秒後自動關閉，而且滑鼠可以穿透
- 將滑鼠移到圖示上，可看到 `Caps Lock: On` 或 `Caps Lock: Off`
- 在通知區圖示上按右鍵，可使用 `Refresh status` 或 `Exit Caps Lock Indicator`
- 開啟 `Overlay settings...` 可調整開啟／關閉顏色、透明度與顯示時間（1～5 秒）；設定會保存到目前 Windows 使用者
- 開啟右鍵選單的 `Language`，可選擇 `System default`、`Traditional Chinese` 或 `English`；選擇會保存，下次啟動仍會沿用
- 選擇 `Check for updates` 可從 GitHub 取得版本資訊；程式不會自動下載或安裝
- 勾選 `Start with Windows` 可讓工具在登入 Windows 後自動執行；再次點擊可取消
- 選擇 `Support development...` 可開啟自願支持選項
- 如果圖示被 Windows 收進 `^` 隱藏圖示區，可將它拖曳到工作列上

`Start with Windows` 只會設定目前 Windows 使用者，不需要管理員權限。
如果 Windows 顯示 SmartScreen 提示，請先選擇 `More info`，再選擇 `Run anyway`。這是未經數位簽章的本機工具常見提示。
