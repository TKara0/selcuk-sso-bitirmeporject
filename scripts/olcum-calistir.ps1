# Otomatik SSO ölçümü: çözümü derler, uygulamaları (kimlik sunucusu, Demo OBİS, Demo Yemekhane, Demo API)
# run-all.ps1 -TarayiciAcma ile ölçüm modunda başlatır, tools/Selcuk.Olcum'u çalıştırır ve sonunda yalnızca bu betiğin
# başlattığı uygulama pencerelerini kapatır. Sonuçlar docs/olcumler/sso-olcumler-<tarih>.csv/.json/.md.
# Kullanım (proje kökünden):
#   powershell -ExecutionPolicy Bypass -File scripts\olcum-calistir.ps1
#   powershell -ExecutionPolicy Bypass -File scripts\olcum-calistir.ps1 -Tekrar 20 -Eszamanlilik "1,10"
# Önkoşul (bir kez): kimlik sunucusunun user-secrets'ında OlcumKullanicilari:Sifre ve Istemciler:DemoObis:ClientSecret
# (README > Ölçüm ve değerlendirme). Ölçüm modu: Olcum:HizSiniriGevset=true yalnızca bu betiğin başlattığı süreçlere
# ortam değişkeniyle verilir; giriş sayfasının istek sınırı yalnızca Development'ta ve olcum.kullanici.* için gevşer.

param(
    [int] $Tekrar = 100,
    [int] $Isinma = 5,
    # Virgüllü metin: -File ile verilen "1,10" bir dizi değil tek bir metin olarak gelir.
    [string] $Eszamanlilik = '1,10,25,50',
    [int] $KullaniciSayisi = 50
)

$ErrorActionPreference = 'Stop'
$kok = Split-Path -Parent $PSScriptRoot
$Eszamanlilik = ($Eszamanlilik -split '[,\s]+' | Where-Object { $_ }) -join ','
$portlar = 5001, 5101, 5102, 5201, 5301, 5302, 5303, 5304, 5305, 5306, 5307, 5308, 5309, 5310, 5311

# Kullanıcının çalışan uygulamalarına dokunulmaz: portlardan biri doluysa ölçüm başlamaz.
$dolu = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in $portlar }
if ($dolu) {
    $liste = ($dolu | Select-Object -ExpandProperty LocalPort -Unique) -join ', '
    Write-Host "Port(lar) kullanımda: $liste. Çalışan uygulamaları kapatıp tekrar deneyin; bu betik onları kapatmaz." -ForegroundColor Red
    exit 1
}

Write-Host 'Çözüm derleniyor (dotnet build SelcukSSO.sln)...'
& dotnet build (Join-Path $kok 'SelcukSSO.sln') --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "Derleme başarısız (çıkış kodu $LASTEXITCODE). Ölçüm başlatılmadı." -ForegroundColor Red
    exit $LASTEXITCODE
}

$baslangic = Get-Date
$cikisKodu = 1
try {
    # Bu betiğin başlattığı pencereler ortam değişkenlerini devralır.
    $env:Olcum__HizSiniriGevset = 'true'
    & (Join-Path $PSScriptRoot 'run-all.ps1') -TarayiciAcma

    # Port açık olsa da açılıştaki seed (ölçüm kullanıcıları) bitmeden discovery yanıt vermeyebilir.
    $hazir = $false
    $bitis = (Get-Date).AddSeconds(90)
    while (-not $hazir -and (Get-Date) -lt $bitis) {
        try {
            $yanit = Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 'https://localhost:5001/.well-known/openid-configuration'
            $hazir = $yanit.StatusCode -eq 200
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    if (-not $hazir) {
        throw 'Kimlik sunucusu 90 saniye içinde hazır olmadı.'
    }

    Write-Host "Ölçüm başlıyor: tekrar $Tekrar, ısınma $Isinma, eşzamanlılık $Eszamanlilik, kullanıcı $KullaniciSayisi"
    Push-Location $kok
    try {
        & dotnet run --no-build --project 'tools/Selcuk.Olcum' -- `
            --tekrar $Tekrar --isinma $Isinma --eszamanlilik $Eszamanlilik --kullanici-sayisi $KullaniciSayisi
        $cikisKodu = $LASTEXITCODE
    } finally {
        Pop-Location
    }
} finally {
    Remove-Item Env:\Olcum__HizSiniriGevset -ErrorAction SilentlyContinue

    # Yalnızca run-all.ps1'in bu çalıştırmada açtığı pencereler (ve içlerindeki dotnet süreçleri) kapatılır.
    $pencereler = Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe'" |
        Where-Object {
            $_.CommandLine -like '*dotnet run --no-build --project*' -and
            $_.CommandLine -like "*$kok*" -and
            $_.CreationDate -ge $baslangic
        }
    foreach ($p in $pencereler) {
        & taskkill /PID $p.ProcessId /T /F | Out-Null
    }
    Write-Host "Uygulamalar durduruldu ($(@($pencereler).Count) pencere). Süre: $([int]((Get-Date) - $baslangic).TotalMinutes) dk."
}

exit $cikisKodu
