using System.Net;
using Selcuk.Otomasyon.Ortak;
using Selcuk.TestYardimcilari;

namespace Selcuk.Otomasyon.Staj.Tests;

/// <summary>
/// Staj Otomasyonu'nda fakülte sitesinden esinlenen yerleşim (gezinti yolu, ikonlu menü, koyu lacivert üst çubuk, başlık
/// çizgisi). Staj kendi fabrikasını kullandığı için bu testler OtomasyonTemelTestleri'ndekilerin eşdeğeridir; denetimler
/// ortak <see cref="YerlesimDenetimi"/> işlevleridir.
/// </summary>
public sealed class StajYerlesimTests : IDisposable
{
    private readonly StajFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Ogrenci() => _factory.Istemci("203011001", roller: Roller.Ogrenci);

    private async Task<string> CssAsync(string adres) =>
        await (await _factory.Istemci().GetAsync(adres)).Content.ReadAsStringAsync();

    [Fact]
    public async Task Her_menu_sayfasinda_gezinti_yolu_h1in_altinda_ilk_baglanti_Authority_koku_son_oge_aria_current()
    {
        foreach (var oge in StajOtomasyonu.Menu)
        {
            var html = await StajFactory.MetinAsync(await _factory.Istemci("rol.test", roller: oge.Roller).GetAsync(oge.Sayfa));

            YerlesimDenetimi.GeziniYoluDogrula(html, StajFactory.Authority, StajOtomasyonu.Ad, oge.Baslik, oge.Sayfa);
        }
    }

    [Fact]
    public async Task Ana_sayfa_ve_Profilim_gezinti_yolu()
    {
        YerlesimDenetimi.GeziniYoluDogrula(await StajFactory.MetinAsync(await Ogrenci().GetAsync("/")),
            StajFactory.Authority, StajOtomasyonu.Ad, null, "/");
        YerlesimDenetimi.GeziniYoluDogrula(await StajFactory.MetinAsync(await Ogrenci().GetAsync("/Profilim")),
            StajFactory.Authority, StajOtomasyonu.Ad, "Profilim", "/Profilim");
    }

    [Fact]
    public async Task Giris_cikis_hata_erisim_reddedildi_ve_girissiz_sayfalarda_gezinti_yolu_yok()
    {
        var client = Ogrenci();
        foreach (var sayfa in new[] { "/Hata", "/GirisHatasi", "/Cikis" })
        {
            Assert.DoesNotContain("gezinti-yolu", await StajFactory.MetinAsync(await client.GetAsync(sayfa)));
        }

        var yonlendirme = await client.GetAsync("/Komisyon/BasvuruInceleme");
        var red = await client.GetAsync(yonlendirme.Headers.Location);
        Assert.Equal(HttpStatusCode.Forbidden, red.StatusCode);
        Assert.DoesNotContain("gezinti-yolu", await StajFactory.MetinAsync(red));

        var anonim = _factory.Istemci();
        Assert.DoesNotContain("gezinti-yolu", await StajFactory.MetinAsync(await anonim.GetAsync("/")));
        Assert.DoesNotContain("gezinti-yolu", await StajFactory.MetinAsync(await anonim.GetAsync("/Giris")));
    }

    [Fact]
    public async Task Menu_simgeleri_anlamli_aria_hidden_sprite_tanimli_menu_metinleri_degismemis()
    {
        Assert.All(StajOtomasyonu.Menu, o => Assert.True(MenuSimgeleri.Var(o.Simge) && o.Simge != MenuSimgeleri.Varsayilan, o.Sayfa));

        foreach (var rol in new[] { Roller.Ogrenci, Roller.StajKomisyonu, Roller.BolumSekreteri, Roller.BolumBaskani })
        {
            var html = await StajFactory.MetinAsync(await _factory.Istemci("rol.test", roller: rol).GetAsync("/"));
            IReadOnlyList<(string, string?)> beklenen =
                [("Ana sayfa", "ana-sayfa"), .. StajOtomasyonu.Menu.Where(o => o.Roller.Contains(rol)).Select(o => (o.Baslik, o.Simge))];

            YerlesimDenetimi.MenuSimgeleriniDogrula(html, beklenen, rol);
            Assert.DoesNotContain(" style=", html);
        }
    }

    [Fact]
    public async Task Ust_cubuk_renk_ciftlerinin_kontrasti_acik_ve_koyu_tema()
    {
        YerlesimDenetimi.UstCubukKontrastiniDogrula(
            await CssAsync("/_content/Selcuk.Arayuz/css/selcuk.css"),
            await CssAsync("/_content/Selcuk.Otomasyon.Ortak/css/otomasyon.css"));
    }

    [Fact]
    public async Task Vurgu_cizgisi_cubugun_altinda_baslik_cizgisi_CSS_ile_cizilir()
    {
        var css = await CssAsync("/_content/Selcuk.Otomasyon.Ortak/css/otomasyon.css");

        Assert.Matches(@"\.uygulama-cubugu\s*\{[^}]*border-top:\s*0;[^}]*border-bottom:\s*3px solid var\(--uygulama-vurgu\)", css);
        Assert.Matches(@"main h1::after\s*\{[^}]*content:\s*"""";[^}]*width:\s*3\.5rem;[^}]*height:\s*4px;[^}]*background:\s*var\(--uygulama-vurgu\)", css);
        Assert.DoesNotMatch(@"box-shadow:\s+(?!inset\b|none\b)\S", css);
    }
}
