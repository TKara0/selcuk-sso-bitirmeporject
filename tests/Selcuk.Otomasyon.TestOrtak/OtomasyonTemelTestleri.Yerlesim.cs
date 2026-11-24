using System.Net;
using System.Text.RegularExpressions;
using Selcuk.Otomasyon.Ortak;
using Selcuk.TestYardimcilari;

namespace Selcuk.Otomasyon.TestOrtak;

/// <summary>
/// Bütün otomasyonlarda aynı olan yerleşim testleri (fakülte sitesinden esinlenen öğeler): gezinti yolu, ikonlu menü,
/// koyu lacivert üst çubuk ve başlık çizgisi. Ayrıntılı denetimler <see cref="YerlesimDenetimi"/>'ndedir.
/// </summary>
public abstract partial class OtomasyonTemelTestleri<TProgram>
{
    private async Task<string> CssAsync(string adres) =>
        await (await Fabrika.Istemci().GetAsync(adres)).Content.ReadAsStringAsync();

    // ---------- Gezinti yolu ----------

    [Fact]
    public async Task Her_menu_sayfasinda_gezinti_yolu_h1in_altinda_ilk_baglanti_Authority_koku_son_oge_aria_current()
    {
        var kok = KimlikSunucusuKoku();
        foreach (var oge in Menu)
        {
            var html = await OtomasyonFactory.MetinAsync(await Istemci(oge.Roller.First()).GetAsync(oge.Sayfa));

            YerlesimDenetimi.GeziniYoluDogrula(html, kok, Ad, oge.Baslik, oge.Sayfa);
        }
    }

    [Fact]
    public async Task Ana_sayfada_gezinti_yolu_yalnizca_Uygulamalarim_ve_otomasyon_adi()
    {
        var html = await OtomasyonFactory.MetinAsync(await Istemci(Kullanicilar.Keys.First()).GetAsync("/"));

        YerlesimDenetimi.GeziniYoluDogrula(html, KimlikSunucusuKoku(), Ad, null, "/");
    }

    [Fact]
    public async Task Profilim_sayfasinda_gezinti_yolu_Profilim_ile_bitiyor()
    {
        var html = await OtomasyonFactory.MetinAsync(await Istemci(Kullanicilar.Keys.First()).GetAsync("/Profilim"));

        YerlesimDenetimi.GeziniYoluDogrula(html, KimlikSunucusuKoku(), Ad, "Profilim", "/Profilim");
    }

    [Fact]
    public async Task Giris_cikis_hata_erisim_reddedildi_ve_girissiz_sayfalarda_gezinti_yolu_yok()
    {
        var client = Istemci(AnaRol);
        foreach (var sayfa in new[] { "/Hata", "/GirisHatasi", "/Cikis" })
        {
            var html = await OtomasyonFactory.MetinAsync(await client.GetAsync(sayfa));
            Assert.DoesNotContain("gezinti-yolu", html);
            Assert.DoesNotContain("aria-label=\"Gezinti yolu\"", html);
        }

        var yonlendirme = await client.GetAsync(Menu.First(o => !o.Roller.Contains(AnaRol)).Sayfa);
        var red = await client.GetAsync(yonlendirme.Headers.Location);
        Assert.Equal(HttpStatusCode.Forbidden, red.StatusCode);
        Assert.DoesNotContain("gezinti-yolu", await OtomasyonFactory.MetinAsync(red));

        // Giriş yapmamış kullanıcı: tanıtım sayfası ve (girişe yönlendiren) Giris sayfası.
        var anonim = Fabrika.Istemci();
        Assert.DoesNotContain("gezinti-yolu", await OtomasyonFactory.MetinAsync(await anonim.GetAsync("/")));
        Assert.DoesNotContain("gezinti-yolu", await OtomasyonFactory.MetinAsync(await anonim.GetAsync("/Giris")));
        Assert.DoesNotContain("gezinti-yolu", await OtomasyonFactory.MetinAsync(await anonim.GetAsync("/Olmayan/Sayfa")));
    }

    // ---------- İkonlu menü ----------

    [Fact]
    public void Her_menu_ogesinin_anlamli_bir_simgesi_var()
    {
        Assert.All(Menu, o => Assert.True(MenuSimgeleri.Var(o.Simge) && o.Simge != MenuSimgeleri.Varsayilan,
            $"{o.Sayfa}: simge '{o.Simge}' katalogda yok veya varsayılan."));
    }

    [Fact]
    public async Task Menu_simgeleri_aria_hidden_sprite_tanimli_menu_metinleri_degismemis()
    {
        foreach (var (rol, _) in Kullanicilar)
        {
            var html = await OtomasyonFactory.MetinAsync(await Istemci(rol).GetAsync("/"));
            IReadOnlyList<(string, string?)> beklenen =
                [("Ana sayfa", "ana-sayfa"), .. Menu.Where(o => o.Roller.Contains(rol)).Select(o => (o.Baslik, o.Simge))];

            YerlesimDenetimi.MenuSimgeleriniDogrula(html, beklenen, rol);
            Assert.DoesNotContain(" style=", html);
        }
    }

    // ---------- Üst çubuk ve başlık çizgisi ----------

    [Fact]
    public async Task Ust_cubuk_renk_ciftlerinin_kontrasti_acik_ve_koyu_tema()
    {
        YerlesimDenetimi.UstCubukKontrastiniDogrula(
            await CssAsync("/_content/Selcuk.Arayuz/css/selcuk.css"),
            await CssAsync("/_content/Selcuk.Otomasyon.Ortak/css/otomasyon.css"));
    }

    [Fact]
    public async Task Vurgu_cizgisi_cubugun_altinda_baslik_cizgisi_CSS_ile_cizilir_golge_yok()
    {
        var css = await CssAsync("/_content/Selcuk.Otomasyon.Ortak/css/otomasyon.css");

        // Uygulamanın vurgu rengi: çubuğun altında ince çizgi (üstteki şerit kalktı).
        Assert.Matches(@"\.uygulama-cubugu\s*\{[^}]*border-top:\s*0;[^}]*border-bottom:\s*3px solid var\(--uygulama-vurgu\)", css);
        // h1'in altında kısa, kalın, dekoratif çizgi: yalnızca CSS (içerik boş, ekran okuyucuya bir şey okunmaz).
        Assert.Matches(@"main h1::after\s*\{[^}]*content:\s*"""";[^}]*width:\s*3\.5rem;[^}]*height:\s*4px;[^}]*background:\s*var\(--uygulama-vurgu\)", css);
        Assert.DoesNotMatch(@"box-shadow:\s+(?!inset\b|none\b)\S", css);

        var oge = Menu[0];
        var html = await OtomasyonFactory.MetinAsync(await Istemci(oge.Roller.First()).GetAsync(oge.Sayfa));
        Assert.Single(Regex.Matches(html, "<h1\\b"));
        Assert.DoesNotContain("<hr", html);
    }
}
