# Kimlik sunucusunu (https://localhost:5001), Demo OBİS'i (https://localhost:5101), Demo Yemekhane'yi
# (https://localhost:5102), Demo API'yi (https://localhost:5201) ve bölüm otomasyonlarını (İntibak :5301, Staj :5302,
# Etik Kurul :5303, Mezuniyet :5304, Bölüm Kurul Karar :5305, Öğrenci Danışmanlık :5306, İŞKUR :5307, MÜDEK :5308,
# Lisans Projeleri :5309, Sınav Başarısı Analizi :5310, ÇAP-Yandal :5311) ayrı
# PowerShell pencerelerinde başlatır, ardından tarayıcıda kimlik sunucusunun "Uygulamalarım" sayfasını açar.
# Kullanım (proje kökünden):  powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1
# -TekKapi verilirse Tek Kapı da (araclar/tek-kapi, http://127.0.0.1:4545) SSO modunda ayrı bir pencerede başlar:
#   powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1 -TekKapi
# -TarayiciAcma verilirse sonda varsayılan tarayıcı açılmaz (ör. otomatik denemelerde).
# -Sadece verilirse yalnızca o uygulamalar başlar (virgülle ayrılmış adlar; kimlik sunucusu her zaman başlar):
#   staj, intibak, mezuniyet, cap-yandal, etik-kurul, bolum-kurul, lisans-projeleri, danismanlik, iskur, mudek,
#   sinav-analizi, obis, yemekhane, demo-api
#   powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1 -Sadece staj,intibak
# -ArkaPlan verilirse uygulamalar pencere açmadan (gizli) başlar; her birinin çıktısı logs\<uygulama>.log dosyasına,
# başlatılan işlemlerin PID'leri (başlangıç zamanıyla) logs\run-all.pids dosyasına yazılır. Kapatmak için:
#   powershell -ExecutionPolicy Bypass -File scripts\durdur-hepsi.ps1
# Tek Kapı (-TekKapi) her durumda kendi penceresinde açılır ve durdur-hepsi.ps1 onu kapatmaz.

param(
    [switch] $TekKapi,
    [switch] $TarayiciAcma,
    [string[]] $Sadece,
    [switch] $ArkaPlan
)

$ErrorActionPreference = 'Stop'
$kok = Split-Path -Parent $PSScriptRoot

# Ad: -Sadece ve log dosyası adı (ekran-goruntuleri.mjs'deki --sadece adlarıyla aynı).
$uygulamalar = @(
    @{ Ad = 'kimlik';           Baslik = 'Selcuk.Identity :5001';        Proje = 'src/Selcuk.Identity';        Port = 5001 },
    @{ Ad = 'obis';             Baslik = 'Selcuk.Demo.Obis :5101';       Proje = 'src/Selcuk.Demo.Obis';       Port = 5101 },
    @{ Ad = 'yemekhane';        Baslik = 'Selcuk.Demo.Yemekhane :5102';  Proje = 'src/Selcuk.Demo.Yemekhane';  Port = 5102 },
    @{ Ad = 'demo-api';         Baslik = 'Selcuk.Demo.Api :5201';        Proje = 'src/Selcuk.Demo.Api';        Port = 5201 },
    @{ Ad = 'intibak';          Baslik = 'Selcuk.Otomasyon.Intibak :5301';    Proje = 'src/Selcuk.Otomasyon.Intibak';    Port = 5301 },
    @{ Ad = 'staj';             Baslik = 'Selcuk.Otomasyon.Staj :5302';  Proje = 'src/Selcuk.Otomasyon.Staj';  Port = 5302 },
    @{ Ad = 'etik-kurul';       Baslik = 'Selcuk.Otomasyon.EtikKurul :5303';  Proje = 'src/Selcuk.Otomasyon.EtikKurul';  Port = 5303 },
    @{ Ad = 'mezuniyet';        Baslik = 'Selcuk.Otomasyon.Mezuniyet :5304';  Proje = 'src/Selcuk.Otomasyon.Mezuniyet';  Port = 5304 },
    @{ Ad = 'bolum-kurul';      Baslik = 'Selcuk.Otomasyon.BolumKurul :5305';  Proje = 'src/Selcuk.Otomasyon.BolumKurul';  Port = 5305 },
    @{ Ad = 'danismanlik';      Baslik = 'Selcuk.Otomasyon.Danismanlik :5306';  Proje = 'src/Selcuk.Otomasyon.Danismanlik';  Port = 5306 },
    @{ Ad = 'iskur';            Baslik = 'Selcuk.Otomasyon.Iskur :5307';  Proje = 'src/Selcuk.Otomasyon.Iskur';  Port = 5307 },
    @{ Ad = 'mudek';            Baslik = 'Selcuk.Otomasyon.Mudek :5308';  Proje = 'src/Selcuk.Otomasyon.Mudek';  Port = 5308 },
    @{ Ad = 'lisans-projeleri'; Baslik = 'Selcuk.Otomasyon.LisansProjeleri :5309';  Proje = 'src/Selcuk.Otomasyon.LisansProjeleri';  Port = 5309 },
    @{ Ad = 'sinav-analizi';    Baslik = 'Selcuk.Otomasyon.SinavAnalizi :5310';  Proje = 'src/Selcuk.Otomasyon.SinavAnalizi';  Port = 5310 },
    @{ Ad = 'cap-yandal';       Baslik = 'Selcuk.Otomasyon.CapYandal :5311';  Proje = 'src/Selcuk.Otomasyon.CapYandal';  Port = 5311 }
)

# -Sadece: önce doğrulanır; geçersiz ad varsa hiçbir şey başlatılmaz (derleme dahil). -File ile gelen "a,b" tek metin
# olabileceği için virgüllerden ayrıca bölünür. Kimlik sunucusu her zaman başlar.
if ($PSBoundParameters.ContainsKey('Sadece')) {
    $secilenAdlar = @($Sadece | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
    $gecerliAdlar = @($uygulamalar | Where-Object { $_.Ad -ne 'kimlik' } | ForEach-Object { $_.Ad })
    $gecersizAdlar = @($secilenAdlar | Where-Object { $gecerliAdlar -notcontains $_ })
    if ($secilenAdlar.Count -eq 0 -or $gecersizAdlar.Count -gt 0) {
        if ($gecersizAdlar.Count -gt 0) {
            Write-Host "Geçersiz uygulama adı: $($gecersizAdlar -join ', ')" -ForegroundColor Red
        } else {
            Write-Host '-Sadece için en az bir uygulama adı ver.' -ForegroundColor Red
        }
        Write-Host "Geçerli adlar: $($gecerliAdlar -join ', ')"
        Write-Host 'Kimlik sunucusu (5001) her zaman başlar. Hiçbir uygulama başlatılmadı.'
        exit 2
    }
    $uygulamalar = @($uygulamalar | Where-Object { $_.Ad -eq 'kimlik' -or $secilenAdlar -contains $_.Ad })
    if ($secilenAdlar -contains 'obis' -and $secilenAdlar -notcontains 'demo-api') {
        Write-Host "Not: Demo OBİS'in Notlarım ekranı Demo API'ye (demo-api) ihtiyaç duyar; eklemediğin için hata bandı görünür." -ForegroundColor Yellow
    }
}

$logKlasoru = Join-Path $kok 'logs'
$pidDosyasi = Join-Path $logKlasoru 'run-all.pids'

# -ArkaPlan: önceki arka plan çalıştırması hâlâ açıksa üstüne yazılmaz (PID kaydı kaybolmasın).
if ($ArkaPlan -and (Test-Path -LiteralPath $pidDosyasi)) {
    $acik = @(Get-Content -LiteralPath $pidDosyasi | Where-Object { $_ -match '^\d+\t' } | ForEach-Object {
        $surecKimligi = [int]($_ -split "`t")[0]
        if (Get-Process -Id $surecKimligi -ErrorAction SilentlyContinue) { $surecKimligi }
    })
    if ($acik.Count -gt 0) {
        Write-Host 'logs\run-all.pids içindeki işlemler hâlâ çalışıyor görünüyor. Önce scripts\durdur-hepsi.ps1 çalıştır.' -ForegroundColor Red
        exit 1
    }
}

# Uygulamalar önceden derlenmiş çıktıdan başlar (--no-build): üç pencerenin aynı anda derlemesi ortak projelerin
# (Selcuk.Istemci.Ortak) obj/bin dosyalarında kilit hatasına yol açıyordu.
function Start-Uygulama([string] $Baslik, [string] $Proje) {
    $komut = "`$Host.UI.RawUI.WindowTitle = '$Baslik'; Set-Location -LiteralPath '$kok'; dotnet run --no-build --project '$Proje'"
    Start-Process powershell -ArgumentList '-NoExit', '-NoProfile', '-Command', $komut | Out-Null
}

# Pencere açmadan başlatır: cmd.exe gizli çalışır, çıktıyı (stdout + stderr) logs\<ad>.log dosyasına yazar. Yollar
# proje köküne göre ve boşluksuzdur (WorkingDirectory = kök). PID (ve başlangıç zamanı: durdur-hepsi.ps1 PID'in
# yeniden kullanılıp kullanılmadığını denetler) hemen kaydedilir. Betik ortam değişkeni veya parola yazmaz.
function Start-ArkaPlanUygulama($Uygulama) {
    $log = "logs\$($Uygulama.Ad).log"
    $surec = Start-Process -FilePath $env:ComSpec -WindowStyle Hidden -PassThru -WorkingDirectory $kok `
        -ArgumentList "/d /c dotnet run --no-build --project $($Uygulama.Proje) > $log 2>&1"
    $baslangic = $surec.StartTime.ToUniversalTime().Ticks
    Add-Content -LiteralPath $pidDosyasi -Value "$($surec.Id)`t$baslangic`t$($Uygulama.Ad)" -Encoding ASCII
    return $surec
}

function Wait-Port([int] $Port, [int] $SaniyeSiniri = 120, $Surec = $null) {
    $bitis = (Get-Date).AddSeconds($SaniyeSiniri)
    while ((Get-Date) -lt $bitis) {
        if ($Surec -and $Surec.HasExited) {
            return $false
        }
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

$arkaPlanSurecleri = @{}
if ($ArkaPlan) {
    New-Item -ItemType Directory -Force -Path $logKlasoru | Out-Null
    [System.IO.File]::WriteAllText($pidDosyasi, '')
}

foreach ($u in $uygulamalar) {
    if ($ArkaPlan) {
        Write-Host "$($u.Baslik) arka planda başlatılıyor (logs\$($u.Ad).log)..."
        $arkaPlanSurecleri[$u.Port] = Start-ArkaPlanUygulama $u
    } else {
        Write-Host "$($u.Baslik) başlatılıyor..."
        Start-Uygulama $u.Baslik $u.Proje
    }
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
    $uygulamalar += @{ Ad = 'tek-kapi'; Baslik = 'Tek Kapı :4545'; Proje = 'araclar/tek-kapi'; Port = 4545 }
}

$hazir = $true
foreach ($u in $uygulamalar) {
    if (-not (Wait-Port $u.Port 120 $arkaPlanSurecleri[$u.Port])) {
        if ($ArkaPlan -and $arkaPlanSurecleri.ContainsKey($u.Port)) {
            Write-Warning "$($u.Baslik) açılmadı. Ayrıntı için logs\$($u.Ad).log dosyasına bak."
        } else {
            Write-Warning "$($u.Baslik) 120 saniye içinde açılmadı. İlgili PowerShell penceresindeki hata mesajına bakın."
        }
        $hazir = $false
    }
}

if ($ArkaPlan) {
    Write-Host 'Kapatmak için: powershell -ExecutionPolicy Bypass -File scripts\durdur-hepsi.ps1'
}

if ($hazir -and -not $TarayiciAcma) {
    Write-Host 'Uygulamalar hazır. Tarayıcı açılıyor.'
    Start-Process 'https://localhost:5001'
} elseif ($hazir) {
    Write-Host 'Uygulamalar hazır (-TarayiciAcma: tarayıcı açılmadı).'
}
