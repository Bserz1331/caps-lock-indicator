# Compatibility launcher for CapsLockIndicator.exe.
$exePath = Join-Path $PSScriptRoot '..\CapsLockIndicator.exe'

if (-not (Test-Path -LiteralPath $exePath)) {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show(
        'CapsLockIndicator.exe was not found in the same folder.',
        'Caps Lock Indicator',
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Error
    ) | Out-Null
    exit 1
}

Start-Process -FilePath $exePath

