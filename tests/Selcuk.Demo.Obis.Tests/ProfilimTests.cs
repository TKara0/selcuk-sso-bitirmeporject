using System.Net;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>Profilim: roller katmana göre gruplu, Selcuk.Roller'daki Türkçe adlarla ve kapsamıyla.</summary>
public sealed class ProfilimTests
{
    [Fact]
    public async Task Profilim_cok_rollu_kullanicinin_rollerini_gruplu_gosteriyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var client = factory.HttpsIstemcisi(string.Join(",",
            Roller.Akademisyen, Roller.BolumBaskani, Roller.Danisman, Roller.StajKomisyonu, Roller.BolumKuruluUyesi));
        client.DefaultRequestHeaders.Add(ObisFactory.BolumBasligi, "Bilgisayar Mühendisliği");

        var yanit = await client.GetAsync("/Profilim");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var html = WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
        Assert.Contains("<dt>Kullanıcı türü</dt>", html);
        Assert.Contains("<dt>İdari görevler</dt>", html);
        Assert.Contains("<dt>Komisyon ve kurul üyelikleri</dt>", html);
        Assert.Contains("Akademisyen", html);
        Assert.Contains("Bölüm başkanı (Bilgisayar Mühendisliği)", html);
        Assert.Contains("Danışman", html);
        Assert.Contains("Staj komisyonu üyesi", html);
        Assert.Contains("Bölüm kurulu üyesi", html);
        Assert.DoesNotContain("bolum_baskani", html);
        Assert.True(html.IndexOf("Kullanıcı türü", StringComparison.Ordinal) < html.IndexOf("İdari görevler", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Profilim_tek_rolu_ve_idari_personeli_gorunen_adiyla_gosteriyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);

        var html = WebUtility.HtmlDecode(await (await factory.HttpsIstemcisi(Roller.Personel).GetAsync("/Profilim"))
            .Content.ReadAsStringAsync());

        Assert.Contains("<dt>Kullanıcı türü</dt>", html);
        Assert.Contains("İdari personel", html);
        Assert.DoesNotContain("<dt>İdari görevler</dt>", html);
    }
}
