using static Selcuk.RolKapsami;
using static Selcuk.RolKatmani;

namespace Selcuk;

/// <summary>Rolün katmanı: her kullanıcının tam olarak bir kullanıcı türü, sıfır veya daha fazla görevi olur.</summary>
public enum RolKatmani
{
    KullaniciTuru,
    IdariGorev,
    KomisyonVeKurul,
}

/// <summary>Rolün geçerli olduğu birim: kullanıcının Bolum veya Fakulte alanı (id_token'daki bolum, fakulte).</summary>
public enum RolKapsami
{
    Yok,
    Bolum,
    Fakulte,
}

/// <summary>Katalogdaki bir rol.</summary>
/// <param name="Kimlik">role claim'inde geçen değer (Identity rol adı).</param>
/// <param name="Ad">Türkçe görünen ad.</param>
/// <param name="Katman">Kullanıcı türü, idari görev veya komisyon/kurul.</param>
/// <param name="Aciklama">Kısa açıklama.</param>
/// <param name="VerilebilecekTur">Görev rolünün verilebileceği tek kullanıcı türü; kullanıcı türlerinde null.</param>
/// <param name="Kapsam">Rol kullanıcının bölümü veya fakültesi için mi geçerli.</param>
public sealed record RolTanimi(
    string Kimlik, string Ad, RolKatmani Katman, string Aciklama,
    string? VerilebilecekTur = null, RolKapsami Kapsam = Yok);

/// <summary>Görünüm için bir katmanın rolleri (görünen adlar, kapsamlarıyla).</summary>
public sealed record RolGrubu(string Baslik, IReadOnlyList<string> Roller);

/// <summary>
/// Rol kataloğu: kimlik sunucusunun oluşturduğu, token'lara role claim'i olarak yazılan ve alt uygulamaların
/// gösterdiği rollerin tek kaynağı. Yeni rol, <see cref="Katalog"/>'a bir satır eklenerek tanımlanır (sabit yalnızca
/// [Authorize(Roles = …)] gibi kullanımlar için). Tutarlılık kuralları <see cref="RolDogrulayici"/>'dadır.
/// </summary>
public static class Roller
{
    // Kullanıcı türleri
    public const string Ogrenci = "ogrenci";
    public const string Akademisyen = "akademisyen";
    public const string Personel = "personel";

    // İdari görevler
    public const string BolumBaskani = "bolum_baskani";
    public const string BolumSekreteri = "bolum_sekreteri";
    public const string FakulteSekreteri = "fakulte_sekreteri";
    public const string Danisman = "danisman";
    public const string CapYandalKoordinatoru = "cap_yandal_koordinatoru";
    public const string IskurSorumlusu = "iskur_sorumlusu";

    // Komisyon ve kurul üyelikleri
    public const string IntibakKomisyonu = "intibak_komisyonu";
    public const string StajKomisyonu = "staj_komisyonu";
    public const string EtikKurulUyesi = "etik_kurul_uyesi";
    public const string MezuniyetKomisyonu = "mezuniyet_komisyonu";
    public const string BolumKuruluUyesi = "bolum_kurulu_uyesi";
    public const string MudekKomisyonu = "mudek_komisyonu";

    public static readonly IReadOnlyList<RolTanimi> Katalog =
    [
        new(Ogrenci, "Öğrenci", KullaniciTuru, "Lisans öğrencisi."),
        new(Akademisyen, "Akademisyen", KullaniciTuru, "Öğretim üyesi veya öğretim elemanı."),
        new(Personel, "İdari personel", KullaniciTuru, "Bölüm veya fakültede idari işleri yürüten personel."),

        new(BolumBaskani, "Bölüm başkanı", IdariGorev, "Bölümü yönetir, onay ve kurul işlerini yürütür.", Akademisyen, Bolum),
        new(BolumSekreteri, "Bölüm sekreteri", IdariGorev, "Bölümün evrak, rapor ve arşiv işlerini yürütür.", Personel, Bolum),
        new(FakulteSekreteri, "Fakülte sekreteri", IdariGorev, "Fakülteye gelen bölüm evrakını karşılar.", Personel, Fakulte),
        new(Danisman, "Danışman", IdariGorev, "Öğrencilerin akademik danışmanlığını yapar.", Akademisyen),
        new(CapYandalKoordinatoru, "ÇAP-Yandal koordinatörü", IdariGorev, "ÇAP ve yandal başvurularını yürütür.", Akademisyen),
        new(IskurSorumlusu, "İŞKUR sorumlusu", IdariGorev, "İŞKUR gençlik programı devam takibini yapar.", Personel),

        new(IntibakKomisyonu, "İntibak komisyonu üyesi", KomisyonVeKurul, "Ders eşleştirme ve muafiyet kararları.", Akademisyen),
        new(StajKomisyonu, "Staj komisyonu üyesi", KomisyonVeKurul, "Staj başvurularını ve sonuçlarını değerlendirir.", Akademisyen),
        new(EtikKurulUyesi, "Etik kurul üyesi", KomisyonVeKurul, "Etik kurul başvurularına görüş verir.", Akademisyen),
        new(MezuniyetKomisyonu, "Mezuniyet komisyonu üyesi", KomisyonVeKurul, "Mezuniyet başvurularını onaylar.", Akademisyen),
        new(BolumKuruluUyesi, "Bölüm kurulu üyesi", KomisyonVeKurul, "Bölüm kurulu gündemini görür ve oy kullanır.", Akademisyen),
        new(MudekKomisyonu, "MÜDEK komisyonu üyesi", KomisyonVeKurul, "Program çıktıları ve akreditasyon raporları.", Akademisyen),
    ];

    private static readonly Dictionary<string, RolTanimi> KimligeGore =
        Katalog.ToDictionary(r => r.Kimlik, StringComparer.Ordinal);

    /// <summary>Katalogdaki bütün rol kimlikleri (kimlik sunucusu açılışta oluşturur).</summary>
    public static readonly IReadOnlyList<string> Tumu = [.. Katalog.Select(r => r.Kimlik)];

    public static RolTanimi? Bul(string kimlik) => KimligeGore.GetValueOrDefault(kimlik);

    public static string KatmanAdi(RolKatmani katman) => katman switch
    {
        KullaniciTuru => "Kullanıcı türü",
        IdariGorev => "İdari görevler",
        KomisyonVeKurul => "Komisyon ve kurul üyelikleri",
        _ => throw new ArgumentOutOfRangeException(nameof(katman)),
    };

    /// <summary>Kullanıcının rollerinden kullanıcı türü (tutarlı kullanıcıda tam olarak bir tane).</summary>
    public static RolTanimi? KullaniciTuruBul(IEnumerable<string> roller) =>
        Katalog.FirstOrDefault(r => r.Katman == KullaniciTuru && roller.Contains(r.Kimlik, StringComparer.Ordinal));

    /// <summary>Görünen ad; kapsamlı rollerde birimiyle: "Bölüm başkanı (Bilgisayar Mühendisliği)".</summary>
    public static string GorunenAd(RolTanimi rol, string? fakulte, string? bolum)
    {
        var birim = rol.Kapsam switch
        {
            Bolum => bolum,
            Fakulte => fakulte,
            _ => null,
        };
        return string.IsNullOrWhiteSpace(birim) ? rol.Ad : $"{rol.Ad} ({birim})";
    }

    /// <summary>
    /// Rolleri katmana göre, katalog sırasıyla gruplar (boş katman yok). Katalogda olmayan rol kimliği "Diğer roller"
    /// grubunda olduğu gibi gösterilir.
    /// </summary>
    public static IReadOnlyList<RolGrubu> Grupla(IEnumerable<string> roller, string? fakulte, string? bolum)
    {
        var kumeler = roller.ToHashSet(StringComparer.Ordinal);
        var gruplar = new List<RolGrubu>();
        foreach (var katman in Enum.GetValues<RolKatmani>())
        {
            var adlar = Katalog.Where(r => r.Katman == katman && kumeler.Contains(r.Kimlik))
                .Select(r => GorunenAd(r, fakulte, bolum)).ToList();
            if (adlar.Count > 0)
            {
                gruplar.Add(new(KatmanAdi(katman), adlar));
            }
        }

        var bilinmeyen = kumeler.Where(r => !KimligeGore.ContainsKey(r)).Order(StringComparer.Ordinal).ToList();
        if (bilinmeyen.Count > 0)
        {
            gruplar.Add(new("Diğer roller", bilinmeyen));
        }

        return gruplar;
    }
}
