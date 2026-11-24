#!/usr/bin/env python3
"""Cobertura kod kapsamı raporlarını birleştirir ve kaynak projeye göre satır/dal yüzdelerini yazar.

Yalnızca Python standart kütüphanesi kullanılır (yeni araç yok). Rapor kaynağı: coverlet.collector'ın ürettiği
coverage.cobertura.xml dosyaları, ör.

    dotnet test SelcukSSO.sln --no-build --collect:"XPlat Code Coverage" --results-directory TestResults/kapsam
    python scripts/kapsam_birlestir.py TestResults/kapsam

Birleştirme kuralı: dosya yolları raporun <source> köküyle mutlak yola çevrilir; satırlar dosya + satır numarasına
göre birleştirilir (bir satır herhangi bir raporda çalıştıysa kapsanmış sayılır); dallarda aynı satır için raporlar
arasındaki en yüksek kapsanan dal sayısı alınır (dal oranı bu yüzden bir alt sınırdır). Kaynak proje, yolun içindeki
src/<Proje>/ veya tools/<Proje>/ bölümünden bulunur. EF Core'un ürettiği Data/Migrations kodu "Migrations hariç"
satırlarında sayılmaz. --json verilirse sonuç JSON olarak da yazılır.
"""
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

KOK = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
# Windows'ta yollar küçük harfe çevrilerek karşılaştırılır; proje adları gerçek yazımıyla (klasör adından) yazılır.
ADLAR = {ad.lower(): ad for kok in ("src", "tools") if os.path.isdir(os.path.join(KOK, kok))
         for ad in os.listdir(os.path.join(KOK, kok))}
PROJE = re.compile(r"[\\/](?:src|tools)[\\/](Selcuk\.[^\\/]+)[\\/]", re.IGNORECASE)
DAL = re.compile(r"\((\d+)/(\d+)\)")


def raporlari_bul(klasor):
    for dizin, _, dosyalar in os.walk(klasor):
        for ad in dosyalar:
            if ad.endswith(".cobertura.xml") or ad == "coverage.cobertura.xml":
                yield os.path.join(dizin, ad)


def birlestir(klasor):
    # satirlar[dosya][satir] = [isabet, kapsanan_dal, toplam_dal]
    satirlar = {}
    rapor_sayisi = 0
    for yol in raporlari_bul(klasor):
        rapor_sayisi += 1
        kok = ET.parse(yol).getroot()
        kaynaklar = [s.text for s in kok.iterfind("./sources/source") if s.text]
        for sinif in kok.iterfind(".//class"):
            ad = sinif.get("filename") or ""
            if not ad:
                continue
            dosya = ad
            if not os.path.isabs(dosya):
                for kaynak in kaynaklar:
                    aday = os.path.join(kaynak, ad)
                    if os.path.exists(aday):
                        dosya = aday
                        break
                else:
                    dosya = os.path.join(kaynaklar[0], ad) if kaynaklar else ad
            dosya = os.path.normcase(os.path.normpath(dosya))
            hedef = satirlar.setdefault(dosya, {})
            for satir in sinif.iterfind("./lines/line"):
                no = int(satir.get("number"))
                isabet = int(satir.get("hits", "0"))
                kapsanan = toplam = 0
                if satir.get("branch", "False").lower() == "true":
                    eslesme = DAL.search(satir.get("condition-coverage", ""))
                    if eslesme:
                        kapsanan, toplam = int(eslesme.group(1)), int(eslesme.group(2))
                eski = hedef.get(no)
                if eski is None:
                    hedef[no] = [isabet, kapsanan, toplam]
                else:
                    eski[0] = max(eski[0], isabet)
                    eski[1] = max(eski[1], kapsanan)
                    eski[2] = max(eski[2], toplam)
    return satirlar, rapor_sayisi


def ozetle(satirlar):
    projeler = {}
    for dosya, hedef in satirlar.items():
        eslesme = PROJE.search(dosya)
        if not eslesme:
            continue
        proje = ADLAR.get(eslesme.group(1).lower(), eslesme.group(1))
        migrations = f"{os.sep}migrations{os.sep}" in dosya.lower()
        for isabet, kapsanan, toplam in hedef.values():
            kayit = projeler.setdefault(proje, {"satir": 0, "kapsanan": 0, "dal": 0, "kapsanan_dal": 0,
                                                "mig_satir": 0, "mig_kapsanan": 0})
            if migrations:
                kayit["mig_satir"] += 1
                kayit["mig_kapsanan"] += 1 if isabet > 0 else 0
                continue
            kayit["satir"] += 1
            kayit["kapsanan"] += 1 if isabet > 0 else 0
            kayit["dal"] += toplam
            kayit["kapsanan_dal"] += kapsanan
    return projeler


def yuzde(a, b):
    return "—" if b == 0 else f"{100 * a / b:.1f}".replace(".", ",") + " %"


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 1
    satirlar, rapor_sayisi = birlestir(argv[1])
    projeler = ozetle(satirlar)
    print(f"{rapor_sayisi} rapor birleştirildi.\n")
    print("| Kaynak proje | Satır | Satır % | Dal | Dal % |")
    print("|---|---:|---:|---:|---:|")
    t = {"satir": 0, "kapsanan": 0, "dal": 0, "kapsanan_dal": 0}
    for proje in sorted(projeler):
        k = projeler[proje]
        print(f"| `{proje}` | {k['kapsanan']} / {k['satir']} | {yuzde(k['kapsanan'], k['satir'])} | "
              f"{k['kapsanan_dal']} / {k['dal']} | {yuzde(k['kapsanan_dal'], k['dal'])} |")
        for ad in t:
            t[ad] += k[ad]
    print(f"| **Toplam (Migrations hariç)** | {t['kapsanan']} / {t['satir']} | **{yuzde(t['kapsanan'], t['satir'])}** | "
          f"{t['kapsanan_dal']} / {t['dal']} | **{yuzde(t['kapsanan_dal'], t['dal'])}** |")
    mig = projeler.get("Selcuk.Identity", {})
    if mig.get("mig_satir"):
        print(f"\nHariç tutulan Selcuk.Identity Migrations kodu: {mig['mig_kapsanan']} / {mig['mig_satir']} satır.")
    if "--json" in argv:
        with open(os.path.join(argv[1], "ozet.json"), "w", encoding="utf-8") as f:
            json.dump(projeler, f, ensure_ascii=False, indent=2)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
