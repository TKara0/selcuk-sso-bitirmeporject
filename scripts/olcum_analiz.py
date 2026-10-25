#!/usr/bin/env python3
"""Ölçüm analizi: SSO otomatik ölçümleri, elle giriş ölçümleri, Tek Kapı kayıtları ve SUS anketi.

Yalnızca Python 3 standart kütüphanesi. matplotlib kuruluysa PNG grafikler de üretilir; kurulu değilse grafik adımı
atlanır ve uyarı verilir (betik paket kurmaz).

Girdiler (proje kökünden; dosya yoksa ilgili bölüm "henüz ölçülmedi" olarak işaretlenir):
  docs/olcumler/sso-olcumler-*.csv          tools/Selcuk.Olcum ham çıktısı (en yenisi özetlenir)
  docs/olcumler/manuel-giris*.csv           elle ölçüm (manuel-giris-sablonu.csv biçimi; şablonun kendisi hariç)
  docs/olcumler/tek-kapi-olcumler*.csv      Tek Kapı "CSV indir" çıktısı (kullanıcı kopyalar)
  docs/kullanilabilirlik/sus-yanitlar.csv   SUS yanıtları (katilimci, m1..m10; 1-5)
"ornek-" önekli dosyalar gerçek veri değildir, hiçbir zaman okunmaz; yalnızca --self-test kullanır.

Çıktı: docs/olcumler/karsilastirma.md ve (matplotlib varsa) karsilastirma-giris-suresi.png,
karsilastirma-eszamanlilik.png.

Kullanım:
  python scripts/olcum_analiz.py
  python scripts/olcum_analiz.py --self-test      örnek veriyle geçici klasörde çalışır ve sonucu doğrular
  python scripts/olcum_analiz.py --grafik-yok     PNG üretmez
"""

from __future__ import annotations

import argparse
import csv
import io
import shutil
import statistics
import sys
import tempfile
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

KOK = Path(__file__).resolve().parent.parent

SISTEMLER = ["OBİS", "e-posta", "UZEM", "Uzaktan Sınav", "Yemekhane"]
YONTEMLER = ["elle", "Tek Kapı", "kendi SSO demo"]
# Tek Kapı siteId -> sistem adı (araclar/tek-kapi/src/sites.js)
TEKKAPI_SISTEM = {"obis": "OBİS", "posta": "e-posta", "uzem": "UZEM", "sinav": "Uzaktan Sınav", "yemek": "Yemekhane"}
TEKKAPI_SONUC = {"giris": "giriş", "oturum-hatirlandi": "oturum hatırlandı", "basarisiz": "başarısız"}
SUS_MADDE = [f"m{i}" for i in range(1, 11)]
EVET = {"evet", "e", "1", "true", "yes", "var", "x"}
HAYIR = {"hayır", "hayir", "h", "0", "false", "no", "yok", ""}

# Grafik renkleri (tek seri, açık zemin): dataviz referans paletinin 1. kategorik yuvası ve metin/zemin tokenları.
ZEMIN = "#fcfcfb"
SERI = "#2a78d6"
METIN = "#0b0b0b"
IKINCIL = "#52514e"
IZGARA = "#e4e3df"


def uyar(mesaj: str) -> None:
    print("UYARI: " + mesaj, file=sys.stderr)


def oku_csv(yol: Path) -> list[dict[str, str]]:
    # Tek Kapı ve Excel UTF-8 BOM'lu yazar; Excel'in Türkçe ayarı ";" ayırıcı kullanabilir.
    metin = yol.read_text(encoding="utf-8-sig")
    ilk = metin.splitlines()[0] if metin else ""
    ayirici = ";" if ilk.count(";") > ilk.count(",") else ","
    return [{(k or "").strip(): (v or "").strip() for k, v in satir.items()}
            for satir in csv.DictReader(io.StringIO(metin), delimiter=ayirici)]


def sayi(deger: str) -> float | None:
    deger = (deger or "").strip().replace(",", ".")
    try:
        return float(deger)
    except ValueError:
        return None


def mantiksal(deger: str) -> bool | None:
    d = (deger or "").strip().lower()
    return True if d in EVET else False if d in HAYIR else None


def gercek_dosyalar(klasor: Path, desen: str, haric: tuple[str, ...] = ()) -> list[Path]:
    return sorted(p for p in klasor.glob(desen) if not p.name.startswith("ornek-") and p.name not in haric)


@dataclass
class Giris:
    """Karşılaştırma tablosunun bir satırına giren tek giriş denemesi."""
    sistem: str
    yontem: str
    kaynak: str
    sure_sn: float | None
    tik: float | None = None
    tus: float | None = None
    sifre: bool | None = None
    captcha: bool | None = None
    hata: bool = False


def manuel_girisler(klasor: Path) -> tuple[list[Giris], list[str]]:
    girisler, dosyalar = [], []
    for yol in gercek_dosyalar(klasor, "manuel-giris*.csv", haric=("manuel-giris-sablonu.csv",)):
        dosyalar.append(yol.name)
        for no, s in enumerate(oku_csv(yol), start=2):
            if not any(s.values()):
                continue
            sistem, yontem = s.get("sistem", ""), s.get("yontem", "")
            if sistem not in SISTEMLER or yontem not in YONTEMLER:
                uyar(f"{yol.name} satır {no}: bilinmeyen sistem/yöntem ({sistem!r}, {yontem!r}), atlandı")
                continue
            girisler.append(Giris(sistem, yontem, "elle ölçüm", sayi(s.get("sure_sn", "")),
                                  sayi(s.get("tik_sayisi", "")), sayi(s.get("tus_vurusu_tahmini", "")),
                                  mantiksal(s.get("sifre_girildi_mi", "")), mantiksal(s.get("captcha_var_mi", "")),
                                  mantiksal(s.get("hata_oldu_mu", "")) is True))
    return girisler, dosyalar


def tekkapi_girisler(klasor: Path) -> tuple[list[Giris], list[str]]:
    girisler, dosyalar = [], []
    yollar = gercek_dosyalar(klasor, "tek-kapi-olcumler*.csv") + gercek_dosyalar(klasor, "tekkapi-olcumler*.csv")
    for yol in yollar:
        dosyalar.append(yol.name)
        for s in oku_csv(yol):
            sistem = TEKKAPI_SISTEM.get(s.get("siteId", ""))
            if sistem is None:
                continue
            sonuc = s.get("sonuc", "")
            ms = sayi(s.get("toplamMs", ""))
            # Tek Kapı'da kullanıcı üniversite şifresini girmez (kasadan doldurulur); kasa ana şifresi oturum başına bir kez.
            girisler.append(Giris(sistem, "Tek Kapı", f"Tek Kapı kaydı ({TEKKAPI_SONUC.get(sonuc, sonuc)})",
                                  None if ms is None else ms / 1000, sayi(s.get("kullaniciAdimi", "")), None, False,
                                  None, sonuc == "basarisiz"))
    return girisler, dosyalar


def sso_olcumleri(klasor: Path) -> tuple[list[dict[str, str]], str | None, int]:
    yollar = gercek_dosyalar(klasor, "sso-olcumler-*.csv")
    if not yollar:
        return [], None, 0
    return oku_csv(yollar[-1]), yollar[-1].name, len(yollar)


def sso_girisler(kayitlar: list[dict[str, str]]) -> list[Giris]:
    girisler = []
    for k in kayitlar:
        if k.get("isinma") == "1" or k.get("eszamanlilik") != "1":
            continue
        senaryo = k.get("senaryo")
        if senaryo not in ("a-soguk-giris", "b-tek-giris"):
            continue
        basarili = k.get("basarili") == "1"
        sure = sayi(k.get("sure_ms", ""))
        soguk = senaryo == "a-soguk-giris"
        # Araç tek girişte şifre formu gelirse turu hata sayar; soğuk girişte şifre bir kez girilir.
        girisler.append(Giris("OBİS" if soguk else "Yemekhane", "kendi SSO demo",
                              "otomatik ölçüm (soğuk giriş)" if soguk else "otomatik ölçüm (tek giriş)",
                              sure / 1000 if basarili and sure is not None else None, None, None, soguk, False,
                              not basarili))
    return girisler


def yuzdelik(sirali: list[float], p: float) -> float:
    """En yakın sıra yöntemi (ölçüm aracıyla aynı)."""
    import math
    sira = max(1, min(len(sirali), math.ceil(p / 100 * len(sirali))))
    return sirali[sira - 1]


def sus_puanlari(yol: Path) -> tuple[list[float], int]:
    puanlar, atlanan = [], 0
    for no, s in enumerate(oku_csv(yol), start=2):
        degerler = [sayi(s.get(m, "")) for m in SUS_MADDE]
        if any(d is None or d not in (1, 2, 3, 4, 5) for d in degerler):
            if any(s.values()):
                uyar(f"{yol.name} satır {no}: eksik veya 1-5 dışında yanıt, atlandı")
                atlanan += 1
            continue
        # Tek maddeler (olumlu): yanıt - 1; çift maddeler (olumsuz): 5 - yanıt; toplam × 2,5 -> 0-100.
        toplam = sum(d - 1 if i % 2 == 0 else 5 - d for i, d in enumerate(degerler))
        puanlar.append(toplam * 2.5)
    return puanlar, atlanan


def bicim(d: float | None, basamak: int = 2) -> str:
    return "–" if d is None else f"{d:.{basamak}f}".replace(".", ",")


def giris_tablosu(girisler: list[Giris]) -> list[str]:
    satirlar = ["| Sistem | Yöntem | Kaynak | Deneme | Ortalama süre (sn) | Medyan süre (sn) | Ort. tık / adım | "
                "Ort. tuş vuruşu | Şifre girilen deneme | CAPTCHA çıkan | Hata |",
                "|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|"]
    gruplar: dict[tuple[str, str, str], list[Giris]] = {}
    for g in girisler:
        gruplar.setdefault((g.sistem, g.yontem, g.kaynak), []).append(g)

    def sira(anahtar):
        sistem, yontem, kaynak = anahtar
        return (SISTEMLER.index(sistem), YONTEMLER.index(yontem), kaynak)

    for anahtar in sorted(gruplar, key=sira):
        grup = gruplar[anahtar]
        sureler = [g.sure_sn for g in grup if g.sure_sn is not None and not g.hata]
        tik = [g.tik for g in grup if g.tik is not None]
        tus = [g.tus for g in grup if g.tus is not None]
        sifre = [g.sifre for g in grup if g.sifre is not None]
        captcha = [g.captcha for g in grup if g.captcha is not None]
        satirlar.append(
            f"| {anahtar[0]} | {anahtar[1]} | {anahtar[2]} | {len(grup)} | "
            f"{bicim(statistics.fmean(sureler)) if sureler else '–'} | "
            f"{bicim(statistics.median(sureler)) if sureler else '–'} | "
            f"{bicim(statistics.fmean(tik), 1) if tik else '–'} | {bicim(statistics.fmean(tus), 1) if tus else '–'} | "
            f"{f'{sum(sifre)} / {len(sifre)}' if sifre else '–'} | "
            f"{f'{sum(captcha)} / {len(captcha)}' if captcha else '–'} | {sum(g.hata for g in grup)} |")
    return satirlar


def eksik_hucreler(girisler: list[Giris]) -> list[str]:
    var = {(g.sistem, g.yontem) for g in girisler}
    eksik = [f"{s} / {y}" for s in SISTEMLER for y in YONTEMLER if (s, y) not in var]
    return eksik


def sso_ozet(kayitlar: list[dict[str, str]]) -> tuple[list[dict], list[dict]]:
    gruplar: dict[tuple[str, int], list[dict[str, str]]] = {}
    for k in kayitlar:
        if k.get("isinma") == "1":
            continue
        gruplar.setdefault((k["senaryo"], int(k["eszamanlilik"])), []).append(k)
    ozetler = []
    for (senaryo, c), grup in sorted(gruplar.items()):
        sureler = sorted(float(k["sure_ms"]) for k in grup if k["basarili"] == "1")
        ozetler.append({"senaryo": senaryo, "c": c, "n": len(grup), "hata": sum(k["basarili"] != "1" for k in grup),
                        "p50": yuzdelik(sureler, 50) if sureler else None,
                        "p95": yuzdelik(sureler, 95) if sureler else None,
                        "ort": statistics.fmean(sureler) if sureler else None})
    return [o for o in ozetler if o["senaryo"] != "f-eszamanli-giris"], \
        [o for o in ozetler if o["senaryo"] == "f-eszamanli-giris"]


def throughput_oku(klasor: Path, csv_adi: str | None) -> dict[int, float]:
    """Özet JSON'daki saniyede başarılı giriş (duvar saati süresi ham CSV'de yok)."""
    import json
    if csv_adi is None:
        return {}
    yol = klasor / csv_adi.replace(".csv", ".json")
    if not yol.exists():
        return {}
    belge = json.loads(yol.read_text(encoding="utf-8"))
    return {s["eszamanlilik"]: s["saniyede_basarili_giris"] for s in belge.get("senaryolar", [])
            if s.get("senaryo") == "f-eszamanli-giris" and s.get("saniyede_basarili_giris") is not None}


SENARYO_AD = {
    "a-soguk-giris": "(a) Soğuk giriş (OBİS)",
    "b-tek-giris": "(b) Tek giriş (Yemekhane)",
    "c-token-yenileme": "(c) Token yenileme",
    "d-tek-cikis": "(d) Tek çıkış",
    "e-api-cagrisi": "(e) API çağrısı",
}


def grafikler(cikti: Path, girisler: list[Giris], eszamanli: list[dict], throughput: dict[int, float]) -> list[str]:
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        uyar("matplotlib kurulu değil; grafikler atlandı (betik paket kurmaz).")
        return []

    plt.rcParams.update({"font.size": 10, "axes.edgecolor": IKINCIL, "axes.labelcolor": IKINCIL,
                         "xtick.color": IKINCIL, "ytick.color": IKINCIL, "text.color": METIN,
                         "figure.facecolor": ZEMIN, "axes.facecolor": ZEMIN, "savefig.facecolor": ZEMIN})
    uretilen = []

    gruplar: dict[str, list[float]] = {}
    for g in girisler:
        if g.sure_sn is not None and not g.hata:
            gruplar.setdefault(f"{g.yontem}\n{g.kaynak}", []).append(g.sure_sn)
    if gruplar:
        # Yatay kutular: uzun yöntem/kaynak adları y ekseninde çakışmadan okunur; ilk yöntem en üstte.
        adlar = sorted(gruplar, key=lambda a: (YONTEMLER.index(a.split("\n")[0]), a), reverse=True)
        etiketler = [a.replace("\n", " · ") for a in adlar]
        fig, ax = plt.subplots(figsize=(7.5, 1.0 + 0.6 * len(adlar)))
        cizim = {"widths": 0.5, "patch_artist": True,
                 "boxprops": {"facecolor": SERI + "33", "edgecolor": SERI, "linewidth": 1.5},
                 "medianprops": {"color": SERI, "linewidth": 2}, "whiskerprops": {"color": IKINCIL},
                 "capprops": {"color": IKINCIL},
                 "flierprops": {"marker": "o", "markersize": 4, "markerfacecolor": IKINCIL, "markeredgecolor": ZEMIN}}
        try:  # matplotlib >= 3.10
            ax.boxplot([gruplar[a] for a in adlar], tick_labels=etiketler, orientation="horizontal", **cizim)
        except TypeError:
            ax.boxplot([gruplar[a] for a in adlar], labels=etiketler, vert=False, **cizim)
        tum = [d for a in adlar for d in gruplar[a] if d > 0]
        # Otomatik ölçüm (saniyenin altı) ile elle ölçüm (onlarca saniye) aynı eksende: büyük farkta log ölçek.
        if tum and max(tum) / min(tum) > 50:
            ax.set_xscale("log")
            ax.set_xlabel("Giriş süresi (sn, log ölçek)")
        else:
            ax.set_xlim(left=0)
            ax.set_xlabel("Giriş süresi (sn)")
        from matplotlib.ticker import FuncFormatter
        ax.xaxis.set_major_formatter(FuncFormatter(lambda v, _: f"{v:g}".replace(".", ",")))
        ax.set_title("Yöntemlere göre giriş süresi", loc="left", color=METIN)
        ax.grid(axis="x", color=IZGARA, linewidth=0.8)
        ax.set_axisbelow(True)
        for kenar in ("top", "right"):
            ax.spines[kenar].set_visible(False)
        ax.tick_params(axis="y", labelsize=8)
        fig.tight_layout()
        fig.savefig(cikti / "karsilastirma-giris-suresi.png", dpi=150)
        plt.close(fig)
        uretilen.append("karsilastirma-giris-suresi.png")

    if throughput:
        x = sorted(throughput)
        y = [throughput[c] for c in x]
        fig, ax = plt.subplots(figsize=(6.0, 4.0))
        ax.plot(x, y, color=SERI, linewidth=2, marker="o", markersize=7, markeredgecolor=ZEMIN, markeredgewidth=2)
        for xi, yi in zip(x, y):
            ax.annotate(bicim(yi, 1), (xi, yi), textcoords="offset points", xytext=(0, 8), ha="center",
                        fontsize=9, color=IKINCIL)
        ax.set_xlabel("Eşzamanlı kullanıcı")
        ax.set_ylabel("Saniyede başarılı giriş")
        ax.set_title("Eşzamanlılık ve throughput (soğuk giriş)", loc="left", color=METIN)
        ax.set_xticks(x)
        ax.set_ylim(bottom=0, top=max(y) * 1.2)
        ax.grid(axis="y", color=IZGARA, linewidth=0.8)
        ax.set_axisbelow(True)
        for kenar in ("top", "right"):
            ax.spines[kenar].set_visible(False)
        fig.tight_layout()
        fig.savefig(cikti / "karsilastirma-eszamanlilik.png", dpi=150)
        plt.close(fig)
        uretilen.append("karsilastirma-eszamanlilik.png")
    return uretilen


def analiz(kok: Path, grafik: bool = True) -> Path:
    olcumler = kok / "docs" / "olcumler"
    olcumler.mkdir(parents=True, exist_ok=True)
    sso, sso_dosya, sso_sayi = sso_olcumleri(olcumler)
    manuel, manuel_dosyalar = manuel_girisler(olcumler)
    tekkapi, tekkapi_dosyalar = tekkapi_girisler(olcumler)
    sus_yol = kok / "docs" / "kullanilabilirlik" / "sus-yanitlar.csv"
    girisler = manuel + tekkapi + sso_girisler(sso)
    sirali, eszamanli = sso_ozet(sso)
    throughput = throughput_oku(olcumler, sso_dosya)

    md = ["# Karşılaştırma", "",
          f"`scripts/olcum_analiz.py` ile {datetime.now():%Y-%m-%d %H:%M} tarihinde üretildi; elle düzenlemeyin. "
          "Eksik veri \"henüz ölçülmedi\" olarak işaretlidir; ölçülmemiş hiçbir değer tahmin edilmez.", "",
          "## Girdiler", "",
          f"- SSO otomatik ölçüm: {sso_dosya or 'henüz ölçülmedi'}"
          + (f" (toplam {sso_sayi} çalıştırma; en yenisi kullanıldı)" if sso_sayi > 1 else ""),
          f"- Elle giriş ölçümü: {', '.join(manuel_dosyalar) or 'henüz ölçülmedi'}",
          f"- Tek Kapı kayıtları: {', '.join(tekkapi_dosyalar) or 'henüz ölçülmedi'}",
          f"- SUS yanıtları: {'sus-yanitlar.csv' if sus_yol.exists() else 'henüz ölçülmedi'}", "",
          "## Sistem ve yöntem bazında giriş", "",
          "Süreler yalnızca hatasız denemelerden. \"Şifre girilen deneme\": kullanıcının o sistemin şifresini yazdığı "
          "deneme sayısı / bilgisi olan deneme sayısı. Otomatik ölçümde tık/tuş sayılmaz (–); tek girişte şifre "
          "sorulursa tur hatalı sayılır.", ""]
    md += giris_tablosu(girisler) if girisler else ["Henüz ölçülmedi."]
    eksik = eksik_hucreler(girisler)
    if eksik:
        md += ["", "Henüz ölçülmeyen sistem / yöntem çiftleri: " + ", ".join(eksik) + "."]

    md += ["", "## Kendi SSO: otomatik ölçüm özeti", ""]
    if sirali:
        md += ["| Senaryo | n | Hata | p50 (ms) | p95 (ms) | Ortalama (ms) |", "|---|---:|---:|---:|---:|---:|"]
        md += [f"| {SENARYO_AD.get(o['senaryo'], o['senaryo'])} | {o['n']} | {o['hata']} | {bicim(o['p50'], 1)} | "
               f"{bicim(o['p95'], 1)} | {bicim(o['ort'], 1)} |" for o in sirali]
    else:
        md.append("Henüz ölçülmedi.")

    md += ["", "## Eşzamanlılık (soğuk giriş)", ""]
    if eszamanli:
        md += ["| Eşzamanlı kullanıcı | Giriş | Hata | Hata oranı | Saniyede başarılı giriş | p50 (ms) | p95 (ms) |",
               "|---:|---:|---:|---:|---:|---:|---:|"]
        md += [f"| {o['c']} | {o['n']} | {o['hata']} | {bicim(100 * o['hata'] / o['n'], 1)} % | "
               f"{bicim(throughput.get(o['c']), 2)} | {bicim(o['p50'], 1)} | {bicim(o['p95'], 1)} |" for o in eszamanli]
    else:
        md.append("Henüz ölçülmedi.")

    md += ["", "## Kullanılabilirlik (SUS)", ""]
    if sus_yol.exists():
        puanlar, atlanan = sus_puanlari(sus_yol)
        if puanlar:
            md += ["| Katılımcı | Ortalama SUS | Medyan | Min | Max |", "|---:|---:|---:|---:|---:|",
                   f"| {len(puanlar)} | {bicim(statistics.fmean(puanlar), 1)} | "
                   f"{bicim(statistics.median(puanlar), 1)} | {bicim(min(puanlar), 1)} | {bicim(max(puanlar), 1)} |"]
            if atlanan:
                md.append(f"\n{atlanan} eksik yanıt satırı hesaba katılmadı.")
            md.append("\nPuanlama: docs/kullanilabilirlik/sus-anketi.md (0-100; 68 genel ortalama kabul edilir).")
        else:
            md.append("Henüz ölçülmedi (geçerli yanıt yok).")
    else:
        md.append("Henüz ölçülmedi.")

    png = grafikler(olcumler, girisler, eszamanli, throughput) if grafik else []
    md += ["", "## Grafikler", ""]
    md += [f"![{p}]({p})" for p in png] if png else ["Grafik üretilmedi (matplotlib yok, --grafik-yok veya veri yok)."]

    cikti = olcumler / "karsilastirma.md"
    cikti.write_text("\n".join(md) + "\n", encoding="utf-8", newline="\n")
    return cikti


def self_test() -> int:
    """ornek- dosyalarını geçici bir proje kökünde gerçek adlarıyla kurar, analizi çalıştırır ve sonucu doğrular."""
    ornek = KOK / "docs" / "olcumler"
    with tempfile.TemporaryDirectory(prefix="olcum-analiz-") as gecici:
        kok = Path(gecici)
        hedef = kok / "docs" / "olcumler"
        hedef.mkdir(parents=True)
        (kok / "docs" / "kullanilabilirlik").mkdir(parents=True)
        shutil.copy(ornek / "ornek-sso-olcumler.csv", hedef / "sso-olcumler-2000-01-01-0000.csv")
        shutil.copy(ornek / "ornek-sso-olcumler.json", hedef / "sso-olcumler-2000-01-01-0000.json")
        shutil.copy(ornek / "ornek-manuel-giris.csv", hedef / "manuel-giris.csv")
        shutil.copy(ornek / "ornek-tek-kapi-olcumler.csv", hedef / "tek-kapi-olcumler.csv")
        shutil.copy(KOK / "docs" / "kullanilabilirlik" / "ornek-sus-yanitlar.csv",
                    kok / "docs" / "kullanilabilirlik" / "sus-yanitlar.csv")
        # Şablon ve ornek- dosyaları veri sayılmamalı.
        shutil.copy(ornek / "manuel-giris-sablonu.csv", hedef / "manuel-giris-sablonu.csv")
        shutil.copy(ornek / "ornek-manuel-giris.csv", hedef / "ornek-manuel-giris.csv")

        cikti = analiz(kok)
        md = cikti.read_text(encoding="utf-8")
        beklenen = [
            "| OBİS | elle | elle ölçüm | 5 | 31,00 | 30,00 | 9,0 | 28,0 | 5 / 5 | 2 / 5 | 0 |",
            "| OBİS | Tek Kapı | Tek Kapı kaydı (giriş) | 3 | 12,00 | 12,00 | 1,0 | – | 0 / 3 | – | 0 |",
            "| OBİS | kendi SSO demo | otomatik ölçüm (soğuk giriş) | 3 | 0,30 | 0,30 | – | – | 3 / 3 | 0 / 3 | 0 |",
            "| Yemekhane | kendi SSO demo | otomatik ölçüm (tek giriş) | 3 | 0,10 | 0,10 | – | – | 0 / 3 | 0 / 3 | 0 |",
            "| (c) Token yenileme | 4 | 1 | 50,0 | 60,0 | 50,0 |",
            "| (a) Soğuk giriş (OBİS) | 3 | 0 | 300,0 | 310,0 | 300,0 |",
            "| 10 | 4 | 0 | 0,0 % | 20,00 | 400,0 | 500,0 |",
            "| 3 | 77,5 | 75,0 | 70,0 | 87,5 |",
            "Henüz ölçülmeyen sistem / yöntem çiftleri:",
        ]
        hatalar = [b for b in beklenen if b not in md]
        if "ornek-manuel-giris.csv" in md or "manuel-giris-sablonu.csv" in md:
            hatalar.append("ornek- veya şablon dosyası veri olarak okundu")
        try:
            import matplotlib  # noqa: F401
            for png in ("karsilastirma-giris-suresi.png", "karsilastirma-eszamanlilik.png"):
                if not (hedef / png).exists():
                    hatalar.append(f"{png} üretilmedi")
        except ImportError:
            uyar("matplotlib yok: grafik kontrolü atlandı.")

        if hatalar:
            print("self-test BAŞARISIZ:\n  " + "\n  ".join(hatalar) + "\n\n" + md, file=sys.stderr)
            return 1
        print(md)
        print("self-test geçti: örnek veriyle karsilastirma.md üretildi ve doğrulandı (geçici klasörde; repoya yazılmadı).")
        return 0


def main() -> int:
    # Windows konsolu/borusu UTF-8 olmayabilir; yazdırılamayan karakter betiği durdurmasın.
    for akis in (sys.stdout, sys.stderr):
        if hasattr(akis, "reconfigure"):
            akis.reconfigure(errors="replace")
    ayrac = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ayrac.add_argument("--self-test", action="store_true", help="örnek veriyle geçici klasörde çalış ve doğrula")
    ayrac.add_argument("--grafik-yok", action="store_true", help="PNG grafik üretme")
    argumanlar = ayrac.parse_args()
    if argumanlar.self_test:
        return self_test()
    cikti = analiz(KOK, grafik=not argumanlar.grafik_yok)
    print(f"Yazıldı: {cikti.relative_to(KOK)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
