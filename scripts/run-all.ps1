# Kimlik sunucusunu (https://localhost:5001), Demo OBİS'i (https://localhost:5101) ve Demo Yemekhane'yi
# (https://localhost:5102) ayrı PowerShell pencerelerinde başlatır, ardından tarayıcıda kimlik sunucusunun
# "Uygulamalarım" sayfasını açar.
# Kullanım (proje kökünden):  powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1
# -TekKapi verilirse Tek Kapı da (araclar/tek-kapi, http://127.0.0.1:4545) SSO modunda ayrı bir pencerede başlar:
#   powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1 -TekKapi

param(
    [switch] $TekKapi
)

$ErrorActionPreference = 'Stop'
$kok = Split-Path -Parent $PSScriptRoot

$uygulamalar = @(
    @{ Baslik = 'Selcuk.Identity :5001';        Proje = 'src/Selcuk.Identity';        Port = 5001 },
    @{ Baslik = 'Selcuk.Demo.Obis :5101';       Proje = 'src/Selcuk.Demo.Obis';       Port = 5101 },
    @{ Baslik = 'Selcuk.Demo.Yemekhane :5102';  Proje = 'src/Selcuk.Demo.Yemekhane';  Port = 5102 }
)

# Uygulamalar önceden derlenmiş çıktıdan başlar (--no-build): üç pencerenin aynı anda derlemesi ortak projelerin
# (Selcuk.Istemci.Ortak) obj/bin dosyalarında kilit hatasına yol açıyordu.
function Start-Uygulama([string] $Baslik, [string] $Proje) {
    $komut = "`$Host.UI.RawUI.WindowTitle = '$Baslik'; Set-Location -LiteralPath '$kok'; dotnet run --no-build --project '$Proje'"
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

# Önce çözüm bir kez derlenir; derleme başarısızsa hiçbir uygulama başlatılmaz.
Write-Host 'Çözüm derleniyor (dotnet build SelcukSSO.sln)...'
& dotnet build (Join-Path $kok 'SelcukSSO.sln') --nologo
if ($LASTEXITCODE -ne 0) {
    $kod = $LASTEXITCODE
    Write-Host "Derleme başarısız (çıkış kodu $kod). Uygulamalar başlatılmadı." -ForegroundColor Red
    exit $kod
}

foreach ($u in $uygulamalar) {
    Write-Host "$($u.Baslik) başlatılıyor..."
    Start-Uygulama $u.Baslik $u.Proje
}

if ($TekKapi) {
    # SSO modu: kimlik sunucusu SSO_ISSUER; izinli kullanıcılar data/sso.json'dan veya varsayılan (203011001).
    # --use-system-ca (npm start'taki gibi): .NET geliştirme sertifikasına Windows sertifika deposu üzerinden güvenilir.
    # --no-browser: portal Uygulamalarım'daki Tek Kapı kartıyla açılır; yönetilen pencere ilk site kartında açılır.
    $tekKapiKlasoru = Join-Path $kok 'araclar\tek-kapi'
    Write-Host 'Tek Kapı :4545 (SSO modu) başlatılıyor...'
    $komut = "`$Host.UI.RawUI.WindowTitle = 'Tek Kapi :4545 (SSO)'; Set-Location -LiteralPath '$tekKapiKlasoru'; " +
        "if (-not (Test-Path node_modules)) { npm ci }; `$env:SSO_ISSUER = 'https://localhost:5001/'; " +
        "`$env:SSO_CLIENT_ID = 'tek-kapi'; node --use-system-ca src/server.js --no-browser"
    Start-Process powershell -ArgumentList '-NoExit', '-NoProfile', '-Command', $komut | Out-Null
    $uygulamalar += @{ Baslik = 'Tek Kapı :4545'; Proje = 'araclar/tek-kapi'; Port = 4545 }
}

$hazir = $true
foreach ($u in $uygulamalar) {
    if (-not (Wait-Port $u.Port)) {
        Write-Warning "$($u.Baslik) 120 saniye içinde açılmadı. İlgili PowerShell penceresindeki hata mesajına bakın."
        $hazir = $false
    }
}

if ($hazir) {
    Write-Host 'Uygulamalar hazır. Tarayıcı açılıyor.'
    Start-Process 'https://localhost:5001'
}
