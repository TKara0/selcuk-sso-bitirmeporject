#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Tek Şifre Giriş Sistemi: tek komutla başlatma (Windows; yalnızca Python standart kütüphanesi).

    python start.py                    ön kontroller, hepsini başlat, https://localhost:5001'i aç; Ctrl+C ile kapat
    python start.py --durdur           (başka bir oturumdan) başlatılan her şeyi kapat ve kapandığını doğrula
    python start.py --kurulum          yeni bilgisayarda eksik user-secrets değerlerini üretip yaz
    python start.py --kurulum --deneme hiçbir şey yazmadan neyin üretileceğini (yalnızca anahtar adları) göster
    python start.py --sadece staj,obis yalnızca seçili uygulamaları başlat (kimlik sunucusu her zaman başlar)
    python start.py --tarayici-acma    tarayıcıyı açma
    python start.py --hesaplar         yalnızca TEST_KULLANICILARI.md'yi üret (gösterim hesapları; git'e girmez)

Uygulama listesi burada yinelenmez: scripts/run-all.ps1'den okunur ve uygulamalar `run-all.ps1 -ArkaPlan` ile
başlatılır (çıktı logs/<ad>.log, PID'ler logs/run-all.pids); kapatma `scripts/durdur-hepsi.ps1` iledir. Tek Kapı
başlatılmaz.

Sırlar: bu betik yalnızca user-secrets ANAHTAR ADLARINA bakar ve gösterir. Değerler ekrana, loga veya başka bir dosyaya
yazılmaz; --kurulum yeni değerleri yalnızca `dotnet user-secrets set` ile kullanıcının user-secrets deposuna yazar, var
olan hiçbir değeri değiştirmez. Sertifikayı kendiliğinden güvenilir yapmaz (komutu önerir).

Tek istisna TEST_KULLANICILARI.md'dir (her başlatmada ve --hesaplar ile üretilir): yalnızca altı gösterim hesabının
(GOSTERIM_HESAPLARI; kurgusal, yalnızca yerel Development) şifreleri user-secrets'tan bu yerel dosyaya yazılır; konsola
yazılmaz. Dosya git'te yok sayılmıyorsa (`git check-ignore`) yazılmaz.
"""
from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import re
import secrets
import socket
import ssl
import subprocess
import sys
import time
import urllib.error
import urllib.request
import webbrowser
from pathlib import Path

KOK = Path(__file__).resolve().parent
RUN_ALL = KOK / "scripts" / "run-all.ps1"
DURDUR = KOK / "scripts" / "durdur-hepsi.ps1"
LOG_KLASORU = KOK / "logs"
PID_DOSYASI = LOG_KLASORU / "run-all.pids"
HESAP_DOSYASI = KOK / "TEST_KULLANICILARI.md"
KIMLIK_ADRESI = "https://localhost:5001"
IDENTITY_PROJESI = "src/Selcuk.Identity"
KIMLIK_SUNUCUSU_AD = "kimlik"

# Test kullanıcıları (src/Selcuk.Identity/Data/BaslangicVerisi.cs TestKullanicilari ile aynı): user-secrets anahtarı,
# kullanıcı adı, roller. Şifreler gösterilmez.
TEST_KULLANICILARI = [
    ("Ogrenci", "203011001", ["ogrenci"]),
    ("Ogrenci2", "203011002", ["ogrenci"]),
    ("Ogrenci3", "203011003", ["ogrenci"]),
    ("Akademisyen", "hoca.test", ["akademisyen", "danisman"]),
    ("Personel", "personel.test", ["personel"]),
    ("BolumBaskani", "baskan.test", ["akademisyen", "bolum_baskani", "bolum_kurulu_uyesi"]),
    ("BolumSekreteri", "sekreter.test", ["personel", "bolum_sekreteri"]),
    ("FakulteSekreteri", "fakultesekreter.test", ["personel", "fakulte_sekreteri"]),
    ("Komisyon", "komisyon.test", ["akademisyen", "intibak_komisyonu", "staj_komisyonu", "mezuniyet_komisyonu", "bolum_kurulu_uyesi"]),
    ("Mudek", "mudek.test", ["akademisyen", "mudek_komisyonu", "bolum_kurulu_uyesi"]),
    ("Etik", "etik.test", ["akademisyen", "etik_kurul_uyesi"]),
    ("Koordinator", "koordinator.test", ["akademisyen", "cap_yandal_koordinatoru", "danisman"]),
    ("Iskur", "iskur.test", ["personel", "iskur_sorumlusu"]),
]

# TEST_KULLANICILARI.md'ye yazılan gösterim hesapları (yalnızca bunlar; BaslangicVerisi.GosterimHesaplari ile aynı):
# user-secrets anahtarı -> (kategori, görünen ad). Sıra dosyadaki satır sırasıdır; önerilen iki hesap en üsttedir.
GOSTERIM_HESAPLARI = {
    "Ogrenci2": ("Öğrenci", "Test Öğrenci İki"),
    "BolumBaskani": ("Bölüm başkanı", "Test Bölüm Başkanı"),
    "Akademisyen": ("Akademisyen", "Test Akademisyen"),
    "BolumSekreteri": ("Bölüm sekreteri", "Test Bölüm Sekreteri"),
    "FakulteSekreteri": ("Fakülte sekreteri", "Test Fakülte Sekreteri"),
    "Komisyon": ("Komisyon üyesi", "Test Komisyon Üyesi"),
}
ONERILEN_HESAPLAR = ("Ogrenci2", "BolumBaskani")

# Uygulama (run-all.ps1 adı) -> kimlik sunucusundaki istemci anahtarı. Kimlik sunucusunda
# `Istemciler:<anahtar>:ClientSecret`, uygulamada `Kimlik:ClientSecret` aynı değerde olmalıdır.
ISTEMCI_ANAHTARLARI = {
    "obis": "DemoObis",
    "yemekhane": "DemoYemekhane",
    "staj": "StajOtomasyonu",
    "intibak": "IntibakOtomasyonu",
    "mezuniyet": "MezuniyetOtomasyonu",
    "cap-yandal": "CapYandalOtomasyonu",
    "etik-kurul": "EtikKurulOtomasyonu",
    "bolum-kurul": "BolumKurulOtomasyonu",
    "lisans-projeleri": "LisansProjeleriOtomasyonu",
    "danismanlik": "DanismanlikOtomasyonu",
    "iskur": "IskurOtomasyonu",
    "mudek": "MudekOtomasyonu",
    "sinav-analizi": "SinavAnaliziOtomasyonu",
}

# Hangi roller hangi uygulamayı görür (src/Selcuk.Identity/Data/Istemciler.cs izinliRoller). Demo OBİS, Yemekhane ve
# Demo API herkese açıktır (içlerindeki sayfalar rol bazlıdır). Boş liste = herkes.
UYGULAMA_ROLLERI = {
    "obis": [], "yemekhane": [], "demo-api": [],
    "staj": ["ogrenci", "staj_komisyonu", "bolum_sekreteri", "bolum_baskani"],
    "intibak": ["ogrenci", "intibak_komisyonu", "bolum_baskani", "bolum_sekreteri", "fakulte_sekreteri"],
    "mezuniyet": ["ogrenci", "mezuniyet_komisyonu", "bolum_sekreteri", "fakulte_sekreteri"],
    "cap-yandal": ["ogrenci", "cap_yandal_koordinatoru", "bolum_baskani", "bolum_sekreteri"],
    "etik-kurul": ["akademisyen", "etik_kurul_uyesi", "bolum_baskani"],
    "bolum-kurul": ["bolum_baskani", "bolum_kurulu_uyesi", "bolum_sekreteri", "fakulte_sekreteri"],
    "lisans-projeleri": ["ogrenci", "akademisyen", "bolum_baskani"],
    "danismanlik": ["danisman", "ogrenci", "bolum_baskani"],
    "iskur": ["iskur_sorumlusu", "ogrenci"],
    "mudek": ["mudek_komisyonu", "akademisyen", "bolum_baskani", "bolum_sekreteri"],
    "sinav-analizi": ["akademisyen", "bolum_baskani", "mudek_komisyonu"],
}

# Konsolda görünen kısa uygulama adları.
KISA_AD = {
    "obis": "OBİS", "yemekhane": "Yemekhane", "demo-api": "API", "staj": "Staj", "intibak": "İntibak",
    "mezuniyet": "Mezuniyet", "cap-yandal": "ÇAP-Yandal", "etik-kurul": "Etik Kurul", "bolum-kurul": "Bölüm Kurul",
    "lisans-projeleri": "Lisans Projeleri", "danismanlik": "Danışmanlık", "iskur": "İŞKUR", "mudek": "MÜDEK",
    "sinav-analizi": "Sınav Analizi",
}


# ---------------------------------------------------------------------------------------------------------------
# Yardımcılar

def yaz(metin: str = "") -> None:
    print(metin, flush=True)


def tamam(metin: str) -> None:
    yaz(f"  ✓ {metin}")


def eksik(metin: str, ipucu: str | None = None) -> None:
    yaz(f"  ✗ {metin}")
    if ipucu:
        yaz(f"    → {ipucu}")


def uyari(metin: str) -> None:
    yaz(f"  ! {metin}")


def komut_calistir(args: list[str], zaman_asimi: int = 60, girdi: str | None = None,
                   ortam: dict[str, str] | None = None) -> tuple[int, str]:
    """Komutu çalıştırır; çıkış kodu ve (stdout + stderr) döner. Bulunamazsa (127, '')."""
    try:
        sonuc = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace",
                               timeout=zaman_asimi, input=girdi, cwd=KOK,
                               env={**os.environ, **ortam} if ortam else None)
    except FileNotFoundError:
        return 127, ""
    except subprocess.TimeoutExpired:
        return 124, ""
    return sonuc.returncode, (sonuc.stdout or "") + (sonuc.stderr or "")


def uygulamalari_oku() -> list[dict]:
    """Uygulama listesini scripts/run-all.ps1'den okur (yeniden yazılmaz)."""
    if not RUN_ALL.exists():
        yaz(f"scripts/run-all.ps1 bulunamadı ({RUN_ALL}).")
        sys.exit(2)
    metin = RUN_ALL.read_text(encoding="utf-8-sig")
    desen = re.compile(r"@\{\s*Ad\s*=\s*'([^']+)';\s*Baslik\s*=\s*'([^']+)';\s*Proje\s*=\s*'([^']+)';\s*Port\s*=\s*(\d+)\s*\}")
    # run-all.ps1'de -TekKapi bloğundaki "tek-kapi" kaydı da aynı biçimde yazılmıştır; Tek Kapı başlatılmaz, listeye girmez.
    uygulamalar = [{"ad": a, "baslik": b, "proje": p, "port": int(port)} for a, b, p, port in desen.findall(metin)
                   if a != "tek-kapi"]
    if not uygulamalar:
        yaz("scripts/run-all.ps1 içinde uygulama listesi okunamadı.")
        sys.exit(2)
    return uygulamalar


def secili_uygulamalar(tum: list[dict], sadece: str | None) -> list[dict]:
    if not sadece:
        return tum
    istenen = [parca.strip().lower() for parca in sadece.split(",") if parca.strip()]
    gecerli = [u["ad"] for u in tum if u["ad"] != KIMLIK_SUNUCUSU_AD]
    gecersiz = [a for a in istenen if a not in gecerli]
    if not istenen or gecersiz:
        yaz(f"Geçersiz uygulama adı: {', '.join(gecersiz) if gecersiz else '(boş)'}")
        yaz(f"Geçerli adlar: {', '.join(gecerli)}")
        sys.exit(2)
    return [u for u in tum if u["ad"] == KIMLIK_SUNUCUSU_AD or u["ad"] in istenen]


def port_dolu(port: int) -> bool:
    try:
        with socket.create_connection(("localhost", port), timeout=1):
            return True
    except OSError:
        return False


def https_yanit_veriyor(port: int) -> bool:
    """https://localhost:<port>/ herhangi bir HTTP durum koduyla cevap veriyor mu (yönlendirme izlenmez, veri gönderilmez).
    Yalnızca localhost'a bağlanıldığı için sertifika doğrulaması bu yoklamada kapalıdır; amaç uygulamanın ayakta olduğunu görmek."""
    baglam = ssl.create_default_context()
    baglam.check_hostname = False
    baglam.verify_mode = ssl.CERT_NONE

    class YonlendirmeYok(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, *args, **kwargs):  # noqa: D401
            return None

    acici = urllib.request.build_opener(urllib.request.HTTPSHandler(context=baglam), YonlendirmeYok)
    try:
        acici.open(f"https://localhost:{port}/", timeout=5).close()
        return True
    except urllib.error.HTTPError:
        return True  # 3xx/4xx/5xx de "cevap veriyor" demektir
    except (urllib.error.URLError, OSError, ssl.SSLError):
        return False


def log_sonu(ad: str, satir: int = 12) -> None:
    """logs/<ad>.log'un son satırları. Gizli olabilecek sözcük içeren satırlar gösterilmez."""
    yol = LOG_KLASORU / f"{ad}.log"
    if not yol.exists():
        yaz(f"    (logs/{ad}.log yok)")
        return
    satirlar = yol.read_text(encoding="utf-8", errors="replace").splitlines()[-satir:]
    gizli = re.compile(r"secret|sifre|şifre|password|token|authorization|bearer|cookie|çerez", re.IGNORECASE)
    yaz(f"    --- logs/{ad}.log (son {len(satirlar)} satır) ---")
    for s in satirlar:
        yaz("    " + ("[satır gizlendi]" if gizli.search(s) else s))


# ---------------------------------------------------------------------------------------------------------------
# user-secrets (yalnızca anahtar adları; --kurulum dışında değer okunmaz/saklanmaz)

def secrets_dosyasi(proje: str) -> Path | None:
    for csproj in (KOK / proje).glob("*.csproj"):
        eslesme = re.search(r"<UserSecretsId>([^<]+)</UserSecretsId>", csproj.read_text(encoding="utf-8-sig"))
        if eslesme:
            kok = os.environ.get("APPDATA") or str(Path.home())
            return Path(kok) / "Microsoft" / "UserSecrets" / eslesme.group(1).strip() / "secrets.json"
    return None


def _duzlestir(veri: dict, onek: str = ""):
    for anahtar, deger in veri.items():
        ad = f"{onek}:{anahtar}" if onek else anahtar
        if isinstance(deger, dict):
            yield from _duzlestir(deger, ad)
        else:
            yield ad, deger


def secrets_oku(proje: str) -> dict[str, str]:
    """Projenin user-secrets'ı (anahtar -> değer). Değerler yalnızca bellekte kalır, hiçbir yere yazdırılmaz."""
    yol = secrets_dosyasi(proje)
    if yol is None or not yol.exists():
        return {}
    try:
        return dict(_duzlestir(json.loads(yol.read_text(encoding="utf-8-sig") or "{}")))
    except (OSError, json.JSONDecodeError):
        return {}


def secrets_anahtarlari(proje: str) -> set[str]:
    return set(secrets_oku(proje).keys())


def gerekli_anahtarlar(uygulamalar: list[dict]) -> dict[str, list[str]]:
    """Proje -> gerekli user-secrets anahtar adları. Kimlik sunucusu her açılışta bütün istemcileri ve test
    kullanıcılarını kaydeder, bu yüzden onun anahtarları her zaman gereklidir."""
    gerekli = {IDENTITY_PROJESI: [f"TestKullanicilari:{a}:Sifre" for a, _, _ in TEST_KULLANICILARI]
               + [f"Istemciler:{k}:ClientSecret" for k in ISTEMCI_ANAHTARLARI.values()]}
    for u in uygulamalar:
        if u["ad"] in ISTEMCI_ANAHTARLARI:
            gerekli[u["proje"]] = ["Kimlik:ClientSecret"]
    return gerekli


# ---------------------------------------------------------------------------------------------------------------
# Ön kontroller

def veritabani_kontrol() -> tuple[bool, str]:
    """SelcukSSO veritabanı LocalDB'de var mı (appsettings.json'daki bağlantı dizesinden)."""
    try:
        ayar = json.loads((KOK / IDENTITY_PROJESI / "appsettings.json").read_text(encoding="utf-8-sig"))
        baglanti = ayar["ConnectionStrings"]["SelcukSSO"]
    except (OSError, KeyError, json.JSONDecodeError):
        return False, "appsettings.json okunamadı"
    ad = (re.search(r"Database=([^;]+)", baglanti, re.IGNORECASE) or [None, "SelcukSSO"])[1]
    master = re.sub(r"Database=[^;]+", "Database=master", baglanti, flags=re.IGNORECASE)
    betik = ("$c = New-Object System.Data.SqlClient.SqlConnection($env:SELCUK_BAGLANTI); $c.Open(); "
             "$k = $c.CreateCommand(); $k.CommandText = 'SELECT DB_ID(@ad)'; "
             "$null = $k.Parameters.AddWithValue('@ad', $env:SELCUK_VERITABANI); "
             "$r = $k.ExecuteScalar(); $c.Close(); if ($null -eq $r -or $r -is [System.DBNull]) { 'YOK' } else { 'VAR' }")
    kod, cikti = komut_calistir(["powershell", "-NoProfile", "-Command", betik], 90,
                                ortam={"SELCUK_BAGLANTI": master, "SELCUK_VERITABANI": ad})
    if kod != 0:
        return False, ad
    return "VAR" in cikti, ad


def on_kontrol(uygulamalar: list[dict]) -> bool:
    yaz("Ön kontrol")
    hepsi = True

    surum = sys.version_info
    if surum >= (3, 10):
        tamam(f"Python {surum.major}.{surum.minor}.{surum.micro} (3.10+ gerekli)")
    else:
        eksik(f"Python {surum.major}.{surum.minor} çok eski", "Python 3.10 veya daha yenisini kur: https://www.python.org/downloads/")
        hepsi = False

    kod, cikti = komut_calistir(["dotnet", "--list-sdks"], 30)
    sdk = next((s.split()[0] for s in cikti.splitlines() if s.strip().startswith("10.")), None)
    if kod == 0 and sdk:
        tamam(f".NET SDK {sdk}")
    else:
        eksik(".NET 10 SDK bulunamadı", "winget install --id Microsoft.DotNet.SDK.10 --exact")
        hepsi = False

    kod, cikti = komut_calistir(["sqllocaldb", "info"], 30)
    if kod == 0 and "MSSQLLocalDB" in cikti:
        tamam("SQL Server LocalDB (MSSQLLocalDB)")
        var, ad = veritabani_kontrol()
        if var:
            tamam(f"Veritabanı {ad} mevcut")
        else:
            eksik(f"Veritabanı {ad} bulunamadı", "dotnet tool restore ; dotnet ef database update --project src/Selcuk.Identity")
            hepsi = False
    else:
        eksik("SQL Server LocalDB bulunamadı", "README.md > Kurulum bölümündeki LocalDB adımlarını uygula")
        hepsi = False

    kod, cikti = komut_calistir(["dotnet", "dev-certs", "https", "--check", "--trust"], 60)
    if kod == 0:
        tamam("HTTPS geliştirme sertifikası güvenilir")
    else:
        eksik("HTTPS geliştirme sertifikası yok veya güvenilir değil",
              "dotnet dev-certs https --trust   (Windows bir onay penceresi açar; betik bunu kendiliğinden çalıştırmaz)")
        hepsi = False

    eksikler: dict[str, list[str]] = {}
    toplam = 0
    for proje, anahtarlar in gerekli_anahtarlar(uygulamalar).items():
        mevcut = secrets_anahtarlari(proje)
        toplam += len(anahtarlar)
        yok = [a for a in anahtarlar if a not in mevcut]
        if yok:
            eksikler[proje] = yok
    if not eksikler:
        tamam(f"user-secrets anahtarları tam ({toplam} anahtar, {len(gerekli_anahtarlar(uygulamalar))} proje)")
    else:
        eksik_sayi = sum(len(v) for v in eksikler.values())
        eksik(f"user-secrets: {eksik_sayi} anahtar eksik", "python start.py --kurulum   (var olan değerlere dokunmaz)")
        for proje, yok in eksikler.items():
            yaz(f"      {proje}: {', '.join(yok[:4])}{' …' if len(yok) > 4 else ''}")
        hepsi = False

    dolu = [u for u in uygulamalar if port_dolu(u["port"])]
    if not dolu:
        tamam(f"Portlar boş ({len(uygulamalar)} uygulama: {', '.join(str(u['port']) for u in uygulamalar)})")
    else:
        eksik("Şu portlar dolu: " + ", ".join(f"{u['port']} ({u['ad']})" for u in dolu),
              "Çalışan uygulamayı sen kapat (start.py başkasının işlemlerini kapatmaz); start.py ile başlattıysan: python start.py --durdur")
        hepsi = False
    yaz()
    return hepsi


# ---------------------------------------------------------------------------------------------------------------
# TEST_KULLANICILARI.md

def hesap_listesi_metni(sirlar: dict[str, str]) -> str:
    """Dosyanın içeriği. Kullanıcı ve roller TEST_KULLANICILARI'ndan, şifreler kimlik sunucusunun user-secrets'ından."""
    kullanicilar = {anahtar: (ad, roller) for anahtar, ad, roller in TEST_KULLANICILARI}
    satirlar = [
        "# Test hesapları",
        "",
        "> **Bu dosya yalnızca bu bilgisayardadır, git'e girmez. Hesaplar kurgusaldır ve yalnızca yerel geliştirme",
        "> ortamı içindir. Paylaşma.**",
        "",
        "★ gösterim için önerilen iki hesap.",
        "",
        "| Kategori | Kullanıcı adı | Görünen ad | Roller | Uygulamalarım'da gördüğü uygulamalar | Şifre |",
        "|---|---|---|---|---|---|",
    ]
    for anahtar, (kategori, gorunen_ad) in GOSTERIM_HESAPLARI.items():
        kullanici, roller = kullanicilar[anahtar]
        # Demo API'nin Uygulamalarım'da kartı yoktur.
        gorulen = [KISA_AD[ad] for ad, izinli in UYGULAMA_ROLLERI.items()
                   if ad != "demo-api" and (not izinli or set(roller) & set(izinli))]
        deger = sirlar.get(f"TestKullanicilari:{anahtar}:Sifre")
        # Tablo hücresi: "|" kaçışlanır.
        sifre = "`" + str(deger).replace("|", "\\|") + "`" if deger else "yok (`python start.py --kurulum`)"
        isaret = "★ " if anahtar in ONERILEN_HESAPLAR else ""
        satirlar.append(f"| {isaret}{kategori} | `{kullanici}` | {gorunen_ad} | {', '.join(roller)} "
                        f"| {', '.join(gorulen)} | {sifre} |")
    satirlar += [
        "",
        f"**Giriş adresi:** {KIMLIK_ADRESI} (önce `python start.py`).",
        "",
        "## Kullanım",
        "",
        "- Hesap değiştirmek için çıkış yap (bir uygulamadan çıkınca hepsinden çıkılır) veya ayrı bir gizli pencere kullan.",
        "- 5 hatalı denemede hesap 15 dakika kilitlenir.",
        "- Şifreler bu bilgisayarın user-secrets'ındaki `TestKullanicilari:<Anahtar>:Sifre` değerleridir. Kimlik sunucusu",
        "  Development'ta açılırken bu hesapların veritabanındaki şifresi farklıysa o değere eşitler (uygulamadan değiştirdiğin",
        "  şifre bir sonraki açılışta geri döner).",
        "- Bu dosya her `python start.py` çalıştırmasında yeniden üretilir (`python start.py --hesaplar` yalnızca dosyayı",
        "  üretir); elle yaptığın değişiklikler silinir.",
        "",
    ]
    return "\n".join(satirlar)


def hesaplari_yaz() -> bool:
    """TEST_KULLANICILARI.md'yi üretir. Git'in dosyayı yok saydığı doğrulanamazsa yazmaz. Konsola şifre yazılmaz."""
    kod, _ = komut_calistir(["git", "check-ignore", "--quiet", HESAP_DOSYASI.name], 30)
    if kod != 0:
        uyari(f"{HESAP_DOSYASI.name} yazılmadı: git'te yok sayıldığı doğrulanamadı (git check-ignore). "
              f".gitignore'da '{HESAP_DOSYASI.name}' satırı olmalı ve dosya git'e eklenmemiş olmalı.")
        return False
    try:
        HESAP_DOSYASI.write_text(hesap_listesi_metni(secrets_oku(IDENTITY_PROJESI)), encoding="utf-8")
    except OSError as hata:
        uyari(f"{HESAP_DOSYASI.name} yazılamadı ({type(hata).__name__}).")
        return False
    yaz(f"Test hesapları: {HESAP_DOSYASI.name}")
    return True


# ---------------------------------------------------------------------------------------------------------------
# --kurulum

def rastgele_sir() -> str:
    """README 'Sırlar': 32 rastgele bayt, base64."""
    return base64.b64encode(secrets.token_bytes(32)).decode("ascii")


def rastgele_sifre() -> str:
    """Test kullanıcı şifresi: 16 karakter, büyük harf + küçük harf + rakam (Identity ilkesi: en az 10, büyük, küçük,
    rakam); karıştırılan karakterler (0/O, 1/l/I) yok."""
    buyuk, kucuk, rakam = "ABCDEFGHJKLMNPQRSTUVWXYZ", "abcdefghijkmnpqrstuvwxyz", "23456789"
    karakterler = ([secrets.choice(buyuk) for _ in range(4)] + [secrets.choice(kucuk) for _ in range(6)]
                   + [secrets.choice(rakam) for _ in range(4)]
                   + [secrets.choice(buyuk + kucuk + rakam) for _ in range(2)])
    secrets.SystemRandom().shuffle(karakterler)
    return "".join(karakterler)


def secrets_ozeti() -> str:
    """Tüm projelerin secrets.json içeriğinin özeti: 'değişmedi' kanıtı için karşılaştırılır, hiçbir yere yazılmaz."""
    h = hashlib.sha256()
    for u in sorted({u["proje"] for u in uygulamalari_oku()}):
        yol = secrets_dosyasi(u)
        h.update(u.encode())
        if yol is not None and yol.exists():
            h.update(yol.read_bytes())
    return h.hexdigest()


def secrets_yaz(proje: str, degerler: dict[str, str]) -> bool:
    """Yeni anahtarları `dotnet user-secrets set` ile (JSON stdin'den; komut satırında değer yok) yazar."""
    kod, _ = komut_calistir(["dotnet", "user-secrets", "set", "--project", proje], 120, girdi=json.dumps(degerler))
    return kod == 0


def kurulum(uygulamalar: list[dict], deneme: bool) -> int:
    yaz("Kurulum: eksik user-secrets değerleri" + (" (DENEME: hiçbir şey yazılmayacak)" if deneme else ""))
    yaz("  Yalnızca anahtar adları gösterilir; değerler ekrana, loga veya dosyaya yazılmaz; var olan değerler değişmez.")
    yaz()
    onceki_ozet = secrets_ozeti()

    kimlik = secrets_oku(IDENTITY_PROJESI)
    uygulama_sirlari = {u["ad"]: secrets_oku(u["proje"]) for u in uygulamalar if u["ad"] in ISTEMCI_ANAHTARLARI}
    yazilacak: dict[str, dict[str, str]] = {}
    uyusmayan: list[str] = []
    mevcut_sayi = 0

    def ekle(proje: str, anahtar: str, deger: str) -> None:
        yazilacak.setdefault(proje, {})[anahtar] = deger

    for anahtar, _, _ in TEST_KULLANICILARI:
        ad = f"TestKullanicilari:{anahtar}:Sifre"
        if ad in kimlik:
            mevcut_sayi += 1
        else:
            ekle(IDENTITY_PROJESI, ad, rastgele_sifre())

    for u in uygulamalar:
        istemci = ISTEMCI_ANAHTARLARI.get(u["ad"])
        if istemci is None:
            continue
        kimlik_anahtari = f"Istemciler:{istemci}:ClientSecret"
        kimlik_degeri = kimlik.get(kimlik_anahtari)
        uygulama_degeri = uygulama_sirlari[u["ad"]].get("Kimlik:ClientSecret")
        if kimlik_degeri and uygulama_degeri:
            mevcut_sayi += 2
            if kimlik_degeri != uygulama_degeri:
                uyusmayan.append(f"{kimlik_anahtari} ≠ {u['proje']} Kimlik:ClientSecret")
        elif kimlik_degeri or uygulama_degeri:
            # Biri var: var olan değer korunur, eksik tarafa aynı değer yazılır.
            mevcut_sayi += 1
            ortak = kimlik_degeri or uygulama_degeri
            if not kimlik_degeri:
                ekle(IDENTITY_PROJESI, kimlik_anahtari, ortak)
            else:
                ekle(u["proje"], "Kimlik:ClientSecret", ortak)
        else:
            ortak = rastgele_sir()
            ekle(IDENTITY_PROJESI, kimlik_anahtari, ortak)
            ekle(u["proje"], "Kimlik:ClientSecret", ortak)

    # Denetim kaydı anahtarı Development'ta zorunlu değildir; README'deki tarifle yoksa üretilir.
    if "Denetim:KullaniciAdiOzetAnahtari" in kimlik:
        mevcut_sayi += 1
    else:
        ekle(IDENTITY_PROJESI, "Denetim:KullaniciAdiOzetAnahtari", rastgele_sir())

    yeni_sayi = sum(len(v) for v in yazilacak.values())
    yaz(f"  Var olan anahtar (değişmeden kalır): {mevcut_sayi}")
    yaz(f"  Yeni yazılacak anahtar: {yeni_sayi}")
    for proje, degerler in yazilacak.items():
        yaz(f"    {proje}: {', '.join(sorted(degerler))}")
    for s in uyusmayan:
        eksik(f"İstemci sırrı uyuşmuyor, DEĞİŞTİRİLMEDİ: {s}",
              "Hangisinin doğru olduğunu sen belirle; bir tarafı elle `dotnet user-secrets set` ile diğerine eşitle")

    basarili = True
    if yeni_sayi and not deneme:
        for proje, degerler in yazilacak.items():
            if secrets_yaz(proje, degerler):
                tamam(f"{proje}: {len(degerler)} anahtar yazıldı")
            else:
                eksik(f"{proje}: yazılamadı", f"dotnet user-secrets list --project {proje} ile durumu gör")
                basarili = False
    elif yeni_sayi:
        yaz("  (deneme: hiçbir şey yazılmadı)")

    sonraki_ozet = secrets_ozeti()
    if deneme:
        if onceki_ozet == sonraki_ozet:
            tamam("Deneme: user-secrets dosyaları aynen kaldı (hiçbir şey yazılmadı)")
        else:
            eksik("Deneme sırasında user-secrets dosyaları değişmiş görünüyor (beklenmiyordu)")
            basarili = False
    else:
        # Var olan değerlerin değişmediğini doğrula (değerler yalnızca bellekte karşılaştırılır).
        simdi_kimlik = secrets_oku(IDENTITY_PROJESI)
        degisen = [a for a, d in kimlik.items() if simdi_kimlik.get(a) != d]
        for u in uygulamalar:
            if u["ad"] in uygulama_sirlari:
                simdi = secrets_oku(u["proje"])
                degisen += [a for a, d in uygulama_sirlari[u["ad"]].items() if simdi.get(a) != d]
        if degisen:
            eksik(f"Var olan {len(degisen)} anahtarın değeri değişmiş görünüyor (beklenmiyordu)")
            basarili = False
        else:
            tamam("Var olan hiçbir değer değişmedi")

    kod, _ = komut_calistir(["dotnet", "dev-certs", "https", "--check", "--trust"], 60)
    if kod != 0:
        yaz()
        uyari("HTTPS geliştirme sertifikası güvenilir değil. Şunu kendin çalıştır (Windows bir onay penceresi açar):")
        yaz("      dotnet dev-certs https --trust")
    yaz()
    if basarili and not uyusmayan:
        yaz("Kurulum tamam. Şifreleri görmek için: dotnet user-secrets list --project src/Selcuk.Identity")
        return 0
    return 1


# ---------------------------------------------------------------------------------------------------------------
# Başlatma ve kapatma

def durdur_calistir() -> int:
    kod = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(DURDUR)], cwd=KOK).returncode
    return kod


def kapat_ve_dogrula(uygulamalar: list[dict]) -> bool:
    yaz()
    yaz("Kapatılıyor (scripts/durdur-hepsi.ps1)...")
    kod = durdur_calistir()
    bitis = time.time() + 15
    dinleyen = [u for u in uygulamalar if port_dolu(u["port"])]
    while dinleyen and time.time() < bitis:
        time.sleep(0.5)
        dinleyen = [u for u in dinleyen if port_dolu(u["port"])]
    if not dinleyen and kod == 0:
        tamam(f"Doğrulandı: {len(uygulamalar)} uygulamanın portunda dinleyen işlem kalmadı")
        return True
    if dinleyen:
        eksik("Hâlâ dinleyen port var: " + ", ".join(f"{u['port']} ({u['ad']})" for u in dinleyen),
              "Bu işlemler start.py'nin başlattıkları değilse senin başlattığın uygulamalardır; elle kapat")
    return False


def tablo_yaz(uygulamalar: list[dict], durumlar: dict[str, bool]) -> None:
    yaz(f"  {'Uygulama':<18}{'Adres':<28}Durum")
    for u in uygulamalar:
        yaz(f"  {u['ad']:<18}{'https://localhost:' + str(u['port']):<28}{'✓ çalışıyor' if durumlar.get(u['ad']) else '✗ yanıt yok'}")


def kullanicilari_yaz(uygulamalar: list[dict]) -> None:
    secilen = {u["ad"] for u in uygulamalar}
    yaz("Test kullanıcıları (şifreleri görmek için: dotnet user-secrets list --project src/Selcuk.Identity)")
    for _, kullanici, roller in TEST_KULLANICILARI:
        gorulen = [KISA_AD[ad] for ad, izinli in UYGULAMA_ROLLERI.items()
                   if ad in secilen and (not izinli or set(roller) & set(izinli))]
        yaz(f"  {kullanici:<22}{', '.join(roller)}")
        yaz(f"  {'':<22}→ {', '.join(gorulen)}")
    yaz()


def baslat(uygulamalar: list[dict], sadece: str | None, tarayici_acma: bool, zaman_asimi: int) -> int:
    hazir = on_kontrol(uygulamalar)
    hesaplari_yaz()
    yaz()
    if not hazir:
        yaz("Ön kontrol başarısız; hiçbir uygulama başlatılmadı. Yukarıdaki ✗ satırlarındaki komutlarla düzelt, sonra yeniden çalıştır.")
        return 1

    yaz("Başlatılıyor: scripts/run-all.ps1 -ArkaPlan (önce çözüm derlenir, birkaç dakika sürebilir)...")
    komut = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(RUN_ALL), "-ArkaPlan", "-TarayiciAcma"]
    if sadece:
        komut += ["-Sadece", sadece]
    try:
        kod = subprocess.run(komut, cwd=KOK).returncode
    except KeyboardInterrupt:
        yaz("\nKesildi.")
        kapat_ve_dogrula(uygulamalar)
        return 130
    if kod != 0:
        yaz(f"run-all.ps1 {kod} koduyla bitti; uygulamalar başlatılmadı veya eksik.")
        kapat_ve_dogrula(uygulamalar)
        return kod or 1

    yaz()
    yaz("Uygulamaların https üzerinden cevap vermesi bekleniyor...")
    durumlar: dict[str, bool] = {}
    bitis = time.time() + zaman_asimi
    try:
        while time.time() < bitis and len(durumlar) < len(uygulamalar):
            for u in uygulamalar:
                if u["ad"] not in durumlar and https_yanit_veriyor(u["port"]):
                    durumlar[u["ad"]] = True
            if len(durumlar) < len(uygulamalar):
                time.sleep(1)
    except KeyboardInterrupt:
        yaz("\nKesildi.")
        kapat_ve_dogrula(uygulamalar)
        return 130

    yaz()
    tablo_yaz(uygulamalar, durumlar)
    yaz()
    cevapsiz = [u for u in uygulamalar if not durumlar.get(u["ad"])]
    if cevapsiz:
        for u in cevapsiz:
            eksik(f"{u['ad']} (https://localhost:{u['port']}) {zaman_asimi} sn içinde cevap vermedi")
            log_sonu(u["ad"])
        yaz()
        yaz("Başlatma başarısız; kalan uygulamalar kapatılıyor (loglar logs/ altında kalır).")
        kapat_ve_dogrula(uygulamalar)
        return 1

    kullanicilari_yaz(uygulamalar)
    yaz(f"Hazır: {KIMLIK_ADRESI}  (Uygulamalarım)")
    if tarayici_acma:
        yaz("Tarayıcı açılmadı (--tarayici-acma).")
    else:
        webbrowser.open(KIMLIK_ADRESI)
    yaz("Kapatmak için Ctrl+C (veya başka bir oturumdan: python start.py --durdur).")

    try:
        while True:
            time.sleep(2)
            if not PID_DOSYASI.exists():
                yaz("\nUygulamalar dışarıdan durdurulmuş (logs/run-all.pids yok).")
                dinleyen = [u for u in uygulamalar if port_dolu(u["port"])]
                if dinleyen:
                    eksik("Hâlâ dinleyen port var: " + ", ".join(str(u["port"]) for u in dinleyen))
                    return 1
                tamam("Hiçbir uygulama portunda dinleyen işlem kalmadı")
                return 0
    except KeyboardInterrupt:
        return 0 if kapat_ve_dogrula(uygulamalar) else 1


def durdur(uygulamalar: list[dict]) -> int:
    return 0 if kapat_ve_dogrula(uygulamalar) else 1


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    ayrisici = argparse.ArgumentParser(description="Tek Şifre Giriş Sistemi: tek komutla başlatma (bkz. dosya başı).")
    ayrisici.add_argument("--kurulum", action="store_true", help="eksik user-secrets değerlerini üretip yaz (var olana dokunmaz)")
    ayrisici.add_argument("--deneme", action="store_true", help="--kurulum ile: hiçbir şey yazma, yalnızca anahtar adlarını göster")
    ayrisici.add_argument("--durdur", action="store_true", help="başlatılan her şeyi kapat ve kapandığını doğrula")
    ayrisici.add_argument("--sadece", metavar="AD,AD", help="yalnızca seçili uygulamalar (run-all.ps1 -Sadece ile aynı adlar)")
    ayrisici.add_argument("--tarayici-acma", action="store_true", help="tarayıcıyı açma")
    ayrisici.add_argument("--zaman-asimi", type=int, default=120, metavar="SN", help="uygulama başına cevap bekleme süresi (varsayılan 120)")
    ayrisici.add_argument("--hesaplar", action="store_true", help="yalnızca TEST_KULLANICILARI.md'yi üret (git'e girmez)")
    arguman = ayrisici.parse_args()
    if arguman.deneme and not arguman.kurulum:
        ayrisici.error("--deneme yalnızca --kurulum ile kullanılır")

    if sys.platform != "win32":
        yaz("Bu betik Windows içindir (LocalDB ve PowerShell betikleri).")
        return 2
    tum = uygulamalari_oku()
    uygulamalar = secili_uygulamalar(tum, arguman.sadece)
    if arguman.durdur:
        return durdur(tum)
    if arguman.kurulum:
        return kurulum(tum, arguman.deneme)
    if arguman.hesaplar:
        return 0 if hesaplari_yaz() else 1
    return baslat(uygulamalar, arguman.sadece, arguman.tarayici_acma, arguman.zaman_asimi)


if __name__ == "__main__":
    sys.exit(main())
