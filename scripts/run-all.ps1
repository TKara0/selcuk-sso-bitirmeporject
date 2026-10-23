# Kimlik sunucusunu (https://localhost:5001), Demo OBİS'i (https://localhost:5101) ve Demo Yemekhane'yi
# (https://localhost:5102) ayrı PowerShell pencerelerinde başlatır, ardından tarayıcıda kimlik sunucusunun
# "Uygulamalarım" sayfasını açar.
# Kullanım (proje kökünden):  powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1

$ErrorActionPreference = 'Stop'
$kok = Split-Path -Parent $PSScriptRoot

$uygulamalar = @(
    @{ Baslik = 'Selcuk.Identity :5001';        Proje = 'src/Selcuk.Identity';        Port = 5001 },
    @{ Baslik = 'Selcuk.Demo.Obis :5101';       Proje = 'src/Selcuk.Demo.Obis';       Port = 5101 },
    @{ Baslik = 'Selcuk.Demo.Yemekhane :5102';  Proje = 'src/Selcuk.Demo.Yemekhane';  Port = 5102 }
)

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

foreach ($u in $uygulamalar) {
    Write-Host "$($u.Baslik) başlatılıyor..."
    Start-Uygulama $u.Baslik $u.Proje
}

$hazir = $true
foreach ($u in $uygulamalar) {
    if (-not (Wait-Port $u.Port)) {
        Write-Warning "$($u.Baslik) 120 saniye içinde açılmadı. İlgili PowerShell penceresindeki hata mesajına bakın."
        $hazir = $false
    }
}

if ($hazir) {
    Write-Host 'Üç uygulama da hazır. Tarayıcı açılıyor.'
    Start-Process 'https://localhost:5001'
}
