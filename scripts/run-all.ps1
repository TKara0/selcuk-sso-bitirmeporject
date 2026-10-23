# Kimlik sunucusunu (https://localhost:5001) ve Demo OBİS'i (https://localhost:5101) ayrı PowerShell
# pencerelerinde başlatır, ardından tarayıcıda Demo OBİS'i açar.
# Kullanım (proje kökünden):  powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1

$ErrorActionPreference = 'Stop'
$kok = Split-Path -Parent $PSScriptRoot

function Start-Uygulama([string] $Baslik, [string] $Proje) {
    $komut = "`$Host.UI.RawUI.WindowTitle = '$Baslik'; Set-Location -LiteralPath '$kok'; dotnet run --project '$Proje'"
    Start-Process powershell -ArgumentList '-NoExit', '-NoProfile', '-Command', $komut | Out-Null
}

function Wait-Port([int] $Port, [int] $SaniyeSiniri = 120) {
    $bitis = (Get-Date).AddSeconds($SaniyeSiniri)
    while ((Get-Date) -lt $bitis) {
        $tcp = New-Object System.Net.Sockets.TcpClient
        try {
            $tcp.Connect('localhost', $Port)
            return $true
        } catch {
            Start-Sleep -Milliseconds 500
        } finally {
            $tcp.Dispose()
        }
    }
    return $false
}

Write-Host 'Kimlik sunucusu başlatılıyor (https://localhost:5001)...'
Start-Uygulama 'Selcuk.Identity :5001' 'src/Selcuk.Identity'

Write-Host 'Demo OBİS başlatılıyor (https://localhost:5101)...'
Start-Uygulama 'Selcuk.Demo.Obis :5101' 'src/Selcuk.Demo.Obis'

if ((Wait-Port 5001) -and (Wait-Port 5101)) {
    Write-Host 'İki uygulama da hazır. Tarayıcı açılıyor.'
    Start-Process 'https://localhost:5101'
} else {
    Write-Warning 'Uygulamalardan biri 120 saniye içinde açılmadı. Açılan PowerShell pencerelerindeki hata mesajlarına bakın.'
}
