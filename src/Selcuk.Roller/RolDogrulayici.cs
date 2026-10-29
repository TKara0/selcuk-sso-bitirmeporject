namespace Selcuk;

/// <summary>
/// Kullanıcının rol kümesinin katalog kurallarına uygunluğu:
/// tam olarak bir kullanıcı türü; görev ve komisyon rolleri yalnızca katalogda belirtilen kullanıcı türüne
/// (<see cref="RolTanimi.VerilebilecekTur"/>); öğrenciye hiçbir görev veya komisyon rolü; bölüm kapsamlı rol yalnızca
/// bölümü, fakülte kapsamlı rol yalnızca fakültesi dolu kullanıcıya. Katalogda olmayan rol reddedilir.
/// </summary>
public static class RolDogrulayici
{
    /// <summary>Kural ihlallerinin Türkçe açıklamaları; boşsa rol kümesi geçerlidir.</summary>
    public static IReadOnlyList<string> Hatalar(IEnumerable<string> roller, string? fakulte, string? bolum)
    {
        var hatalar = new List<string>();
        var tanimlar = new List<RolTanimi>();
        foreach (var kimlik in roller.Distinct(StringComparer.Ordinal))
        {
            if (Roller.Bul(kimlik) is { } tanim)
            {
                tanimlar.Add(tanim);
            }
            else
            {
                hatalar.Add($"'{kimlik}' rol kataloğunda yok.");
            }
        }

        var turler = tanimlar.Where(r => r.Katman == RolKatmani.KullaniciTuru).ToList();
        if (turler.Count != 1)
        {
            hatalar.Add(turler.Count == 0
                ? "Kullanıcı türü yok; tam olarak bir kullanıcı türü (öğrenci, akademisyen, idari personel) olmalı."
                : $"Birden fazla kullanıcı türü var ({string.Join(", ", turler.Select(r => r.Kimlik))}); tam olarak bir tane olmalı.");
        }

        var tur = turler.Count == 1 ? turler[0].Kimlik : null;
        foreach (var rol in tanimlar.Where(r => r.Katman != RolKatmani.KullaniciTuru))
        {
            if (tur == Roller.Ogrenci)
            {
                hatalar.Add($"'{rol.Kimlik}' öğrenciye verilemez: öğrencinin idari görevi veya komisyon üyeliği olmaz.");
            }
            else if (tur is not null && rol.VerilebilecekTur != tur)
            {
                hatalar.Add($"'{rol.Kimlik}' yalnızca '{rol.VerilebilecekTur}' kullanıcı türüne verilebilir, bu kullanıcı '{tur}'.");
            }

            if (rol.Kapsam == RolKapsami.Bolum && string.IsNullOrWhiteSpace(bolum))
            {
                hatalar.Add($"'{rol.Kimlik}' bölüm için geçerli; bölümü boş kullanıcıya verilemez.");
            }
            else if (rol.Kapsam == RolKapsami.Fakulte && string.IsNullOrWhiteSpace(fakulte))
            {
                hatalar.Add($"'{rol.Kimlik}' fakülte için geçerli; fakültesi boş kullanıcıya verilemez.");
            }
        }

        return hatalar;
    }

    /// <summary>Kurallara uymayan rol kümesinde, bütün ihlalleri sayan bir hatayla durur.</summary>
    public static void Dogrula(string kullaniciAdi, IEnumerable<string> roller, string? fakulte, string? bolum)
    {
        var hatalar = Hatalar(roller, fakulte, bolum);
        if (hatalar.Count > 0)
        {
            throw new InvalidOperationException(
                $"'{kullaniciAdi}' kullanıcısının rolleri tutarsız: {string.Join(" ", hatalar)}");
        }
    }
}
