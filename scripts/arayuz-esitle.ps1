# Ortak tasarım sisteminin (src/Selcuk.Arayuz/wwwroot) Tek Kapı'nın kullandığı dosyalarını
# araclar/tek-kapi/public/arayuz/ altına kopyalar: css/selcuk.css, fonts/ (yazı tipleri ve OFL lisansları) ve img/
# (kapı işaretlerinin SVG'leri). Tasarımın kaynağı tektir: Tek Kapı'daki kopyalar elle değiştirilmez; kaynak değişince
# bu betik çalıştırılır. Tek Kapı testi (npm test) kopyaların kaynakla birebir aynı olduğunu SHA-256 ile denetler.
# Kullanım (proje kökünden):  powershell -ExecutionPolicy Bypass -File scripts\arayuz-esitle.ps1

$ErrorActionPreference = 'Stop'
$kok = Split-Path -Parent $PSScriptRoot
$kaynak = Join-Path $kok 'src\Selcuk.Arayuz\wwwroot'
$hedef = Join-Path $kok 'araclar\tek-kapi\public\arayuz'

$dosyalar = @(Get-Item (Join-Path $kaynak 'css\selcuk.css')) +
    @(Get-ChildItem -File (Join-Path $kaynak 'fonts')) +
    @(Get-ChildItem -File (Join-Path $kaynak 'img'))

# Hedef her seferinde baştan kurulur: kaynakta silinen bir dosya Tek Kapı'da kalmaz.
if (Test-Path -LiteralPath $hedef) {
    Remove-Item -LiteralPath $hedef -Recurse -Force
}

foreach ($dosya in $dosyalar) {
    $goreli = $dosya.FullName.Substring($kaynak.Length + 1)
    $yol = Join-Path $hedef $goreli
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $yol) | Out-Null
    Copy-Item -LiteralPath $dosya.FullName -Destination $yol
}

Write-Host "$($dosyalar.Count) dosya kopyalandı: $hedef"
