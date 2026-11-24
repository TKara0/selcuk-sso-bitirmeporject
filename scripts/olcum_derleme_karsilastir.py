#!/usr/bin/env python3
"""Ölçüm turlarını (docs/olcumler/*.json) yan yana koyan karşılaştırma tablosu üretir.

Yalnızca Python 3 standart kütüphanesi. Sayılar elle yazılmaz: her hücre ilgili ölçüm dosyasındaki p50/p95
değeridir. Çıktı: docs/olcumler/derleme-karsilastirmasi-<tarih>.md (elle düzenlenmez).

Kullanım (proje kökünden):
  python scripts/olcum_derleme_karsilastir.py
"""
import json
import sys
from pathlib import Path

KOK = Path(__file__).resolve().parent.parent
OLC = KOK / "docs" / "olcumler"
TARIH = "2026-11-24"

# (sütun başlığı, json yolu, açıklama)
TURLAR = [
    ("Eski ölçüm<br>26 Ekim, Debug, o günkü kod", OLC / "sso-olcumler-2026-10-26-0037.json"),
    ("Eski kod bugün<br>Debug, `07121a6`", OLC / "eski-kod-2026-11-24" / "sso-olcumler-2026-11-24-1959.json"),
    ("Güncel kod<br>Debug", OLC / "sso-olcumler-2026-11-24-1954.json"),
    ("Güncel kod<br>Release, tur 1", OLC / "sso-olcumler-2026-11-24-1951.json"),
    ("Güncel kod<br>Release, tur 2", OLC / "sso-olcumler-2026-11-24-1956.json"),
]

def yukle(yol):
    return json.loads(yol.read_text(encoding="utf-8"))


def sayi(x):
    return "—" if x is None else f"{x:.1f}".replace(".", ",")


def senaryo_bul(veri, onek, eszamanlilik=None):
    for s in veri["senaryolar"]:
        if s["senaryo"].startswith(onek[0]) and (eszamanlilik is None or s["eszamanlilik"] == eszamanlilik):
            if eszamanlilik is None and s["senaryo"].startswith("f"):
                continue
            return s
    return None


def main():
    veriler = [(b, yukle(y)) for b, y in TURLAR]
    ortam = veriler[-1][1]["ortam"]
    satirlar = []
    satirlar.append(f"# Release ve Debug ölçümü ({TARIH})\n")
    satirlar.append(
        "`scripts/olcum_derleme_karsilastir.py` ile ölçüm dosyalarından üretildi; elle düzenlemeyin. Süreler milisaniye "
        "(p50 / p95), her tur 100 ölçülen deneme ve 5 ısınma, hata yok. Ölçülmemiş değer yazılmaz.\n")

    satirlar.append("## Ölçüm ortamı\n")
    satirlar.append("| Alan | Değer |\n|---|---|")
    satirlar.append(f"| İşletim sistemi | {ortam['isletim_sistemi']} |")
    satirlar.append(f"| .NET | {ortam['dotnet']} |")
    satirlar.append(f"| İşlemci | {ortam['islemci']} ({ortam['mantiksal_cekirdek']} mantıksal çekirdek) |")
    satirlar.append(f"| RAM | {ortam['ram_gb']:.1f} GB |")
    satirlar.append("| Derleme türü | Debug ve Release (sütun başlıklarında) |")
    satirlar.append("| Ortam | Development, LocalDB; kimlik sunucusu, iki demo, Demo API, 11 otomasyon ve ölçüm aracı aynı makinede; ölçüm kullanıcıları ve ölçüm hız sınırı gevşetmesi `docs/olcumler/sso-olcumler-*.md`'deki kurallarla |")
    satirlar.append("")

    satirlar.append("## Sıralı senaryolar (p50 / p95, ms)\n")
    satirlar.append("| Senaryo | " + " | ".join(b for b, _ in veriler) + " |")
    satirlar.append("|---|" + "---:|" * len(veriler))
    for onek, ad in [("a", "İlk giriş (OBİS)"), ("b", "Tek giriş (Yemekhane)"), ("c", "Belirteç yenileme"),
                     ("e", "API çağrısı"), ("d", "Tek çıkış")]:
        hucreler = []
        for _, v in veriler:
            s = senaryo_bul(v, onek)
            hucreler.append("—" if s is None else f"{sayi(s['p50'])} / {sayi(s['p95'])}")
        satirlar.append(f"| {ad} | " + " | ".join(hucreler) + " |")
    satirlar.append("")

    satirlar.append("## Eşzamanlı soğuk giriş (saniyede başarılı giriş; p50 / p95, ms)\n")
    satirlar.append("| Eşzamanlı kullanıcı | " + " | ".join(b for b, _ in veriler) + " |")
    satirlar.append("|---:|" + "---:|" * len(veriler))
    for n in (1, 10, 25, 50):
        hucreler = []
        for _, v in veriler:
            s = next((x for x in v["senaryolar"] if x["senaryo"].startswith("f") and x["eszamanlilik"] == n), None)
            hucreler.append("—" if s is None else
                            f"**{sayi(s['saniyede_basarili_giris'])}/sn**<br>{sayi(s['p50'])} / {sayi(s['p95'])}")
        satirlar.append(f"| {n} | " + " | ".join(hucreler) + " |")
    satirlar.append("")

    # Doyum noktası: verimin (sn'de giriş) bir üst düzeye geçerken %10'dan az arttığı ilk düzey.
    satirlar.append("## Doyum noktası\n")
    for baslik, v in veriler:
        f = {x["eszamanlilik"]: x for x in v["senaryolar"] if x["senaryo"].startswith("f")}
        duzeyler = sorted(f)
        doyum = None
        for alt, ust in zip(duzeyler, duzeyler[1:]):
            if f[ust]["saniyede_basarili_giris"] < f[alt]["saniyede_basarili_giris"] * 1.10:
                doyum = alt
                break
        en_iyi = max(f[d]["saniyede_basarili_giris"] for d in duzeyler)
        etiket = baslik.replace("<br>", " ")
        artislar = ", ".join(
            f"{alt}→{ust}: %{100 * (f[ust]['saniyede_basarili_giris'] / f[alt]['saniyede_basarili_giris'] - 1):+.0f}".replace(".", ",")
            for alt, ust in zip(duzeyler, duzeyler[1:]))
        sonuc = ("ölçülen en yüksek düzeyde (50) verim hâlâ artıyor" if doyum is None
                 else f"doyum {doyum} eşzamanlı kullanıcıda")
        satirlar.append(f"- {etiket}: verim artışı {artislar}; {sonuc}; en yüksek {sayi(en_iyi)} giriş/sn.")
    satirlar.append("")
    satirlar.append("Doyum ölçütü: bir üst eşzamanlılık düzeyine geçerken saniyede başarılı giriş %10'dan az artıyorsa, "
                    "alttaki düzey doyum noktasıdır (ölçülen düzeyler 1, 10, 25, 50).\n")

    # Bulgular: cümleler sabit, sayılar ölçüm dosyalarından.
    def a(i, anahtar="p50"):
        return senaryo_bul(veriler[i][1], "a")[anahtar]

    satirlar.append("## Bulgular\n")
    satirlar.append(
        f"- **Release ile Debug arasında ölçülebilir bir fark yok.** İlk giriş p50: Debug {sayi(a(2))} ms, Release "
        f"{sayi(a(3))} ve {sayi(a(4))} ms; iki Release turu arasındaki fark ({sayi(abs(a(3)-a(4)))} ms) Debug ile Release "
        "arasındaki farkla aynı büyüklükte. Diğer senaryolarda da aynı; eşzamanlı verim turlar arasında ±%5 dalgalanıyor.")
    satirlar.append(
        f"- **26 Ekim'deki düşük tek kullanıcı süresi ({sayi(a(0))} ms) bugün yeniden elde edilmedi ve kodla açıklanmıyor.** "
        f"26 Ekim'deki kod (`07121a6`) bugünkü makinede {sayi(a(1))} ms verdi, güncel kod {sayi(a(2))} ms. Fark kod "
        "veya derleme türünden değil, ölçüm anındaki ortamdan geliyor; nedeni (işlemci frekansı veya güç planı, arka plan "
        "yükü) ölçülmedi. Eşzamanlı verim ise bugün daha yüksek; tek kullanıcılı süre ile eşzamanlı verim aynı yönde "
        "değişmedi.")
    satirlar.append(
        "- Bu yüzden 26 Ekim ile bugünkü sayılar birbirinin yerine geçmez: aynı gün, aynı makinede alınan sütunlar "
        "(\"Eski kod bugün\", \"Güncel kod\") karşılaştırma için esas alınmalıdır.")
    satirlar.append("")

    yol = OLC / f"derleme-karsilastirmasi-{TARIH}.md"
    yol.write_text("\n".join(satirlar), encoding="utf-8")
    print(f"Yazıldı: {yol.relative_to(KOK)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
