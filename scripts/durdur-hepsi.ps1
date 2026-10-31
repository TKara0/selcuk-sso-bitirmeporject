# run-all.ps1 -ArkaPlan ile başlatılan uygulamaları kapatır.
# Yalnızca logs\run-all.pids dosyasındaki PID'leri ve onların alt işlemlerini durdurur; başka hiçbir işlemi port veya
# ad eşleştirmesiyle kapatmaz. PID kayıtta başlangıç zamanıyla tutulur: aynı numaralı ama başka zamanda başlamış bir
# işlem (PID yeniden kullanımı) bu uygulamalardan biri sayılmaz ve dokunulmaz. Tek Kapı (-TekKapi) kendi penceresinde
# açıldığı için burada kapatılmaz.
# Kullanım (proje kökünden):  powershell -ExecutionPolicy Bypass -File scripts\durdur-hepsi.ps1

$ErrorActionPreference = 'Stop'
$kok = Split-Path -Parent $PSScriptRoot
$pidDosyasi = Join-Path $kok 'logs\run-all.pids'

if (-not (Test-Path -LiteralPath $pidDosyasi)) {
    Write-Host 'logs\run-all.pids bulunamadı: run-all.ps1 -ArkaPlan ile başlatılmış uygulama yok. Hiçbir işlem kapatılmadı.'
    exit 0
}

# Alt işlem ağacı: ParentProcessId eşleşmesi, çocuğun ebeveynden sonra başlamış olması koşuluyla (eski bir ebeveyn
# numarasının yeniden kullanımına karşı).
function Get-AltSurecler([int] $KokId, $Hepsi) {
    $kokKaydi = $Hepsi | Where-Object { $_.ProcessId -eq $KokId } | Select-Object -First 1
    if (-not $kokKaydi) { return @() }
    $sonuc = New-Object System.Collections.Generic.List[int]
    $kuyruk = New-Object System.Collections.Generic.Queue[object]
    $kuyruk.Enqueue($kokKaydi)
    while ($kuyruk.Count -gt 0) {
        $ebeveyn = $kuyruk.Dequeue()
        foreach ($c in ($Hepsi | Where-Object { $_.ParentProcessId -eq $ebeveyn.ProcessId -and $_.ProcessId -ne $ebeveyn.ProcessId })) {
            if ($c.CreationDate -ge $ebeveyn.CreationDate -and -not $sonuc.Contains([int]$c.ProcessId)) {
                $sonuc.Add([int]$c.ProcessId)
                $kuyruk.Enqueue($c)
            }
        }
    }
    return $sonuc.ToArray()
}

$tumSurecler = @(Get-CimInstance -ClassName Win32_Process)
$durdurulan = 0
$sorunlu = 0

foreach ($satir in Get-Content -LiteralPath $pidDosyasi) {
    $alanlar = $satir -split "`t"
    if ($alanlar.Count -lt 3 -or $alanlar[0] -notmatch '^\d+$' -or $alanlar[1] -notmatch '^\d+$') {
        continue
    }

    $surecKimligi = [int]$alanlar[0]
    $ad = $alanlar[2]
    $surec = Get-Process -Id $surecKimligi -ErrorAction SilentlyContinue
    if (-not $surec) {
        Write-Host "$ad (PID $surecKimligi): zaten kapalı."
        continue
    }

    $fark = [math]::Abs(($surec.StartTime.ToUniversalTime().Ticks - [int64]$alanlar[1]) / [TimeSpan]::TicksPerSecond)
    if ($fark -gt 2) {
        Write-Host "$ad (PID $surecKimligi): bu numara artık başka bir işleme ait görünüyor, dokunulmadı."
        continue
    }

    # Önce en derindeki alt işlemler, sonra kök işlem.
    $kapatilacaklar = @(Get-AltSurecler $surecKimligi $tumSurecler)
    [array]::Reverse($kapatilacaklar)
    $kapatilacaklar += $surecKimligi
    foreach ($id in $kapatilacaklar) {
        try {
            Stop-Process -Id $id -Force -ErrorAction Stop
        } catch {
            # İşlem bu arada kendiliğinden kapanmış olabilir; aşağıda gerçekten kalıp kalmadığı denetlenir.
        }
    }

    Start-Sleep -Milliseconds 300
    $kalan = @($kapatilacaklar | Where-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue })
    if ($kalan.Count -gt 0) {
        Write-Host "$ad (PID $surecKimligi): kapatılamadı (kalan PID: $($kalan -join ', '))." -ForegroundColor Red
        $sorunlu++
    } else {
        Write-Host "$ad (PID $surecKimligi): durduruldu."
        $durdurulan++
    }
}

if ($sorunlu -eq 0) {
    Remove-Item -LiteralPath $pidDosyasi -Force
    Write-Host "Tamam: $durdurulan uygulama durduruldu, logs\run-all.pids silindi."
} else {
    Write-Host "$sorunlu uygulama kapatılamadı; logs\run-all.pids silinmedi, betiği yeniden çalıştırabilirsin." -ForegroundColor Red
    exit 1
}
