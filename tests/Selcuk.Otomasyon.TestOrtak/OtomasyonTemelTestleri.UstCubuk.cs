using Selcuk.Otomasyon.Ortak;
using Selcuk.TestYardimcilari;

namespace Selcuk.Otomasyon.TestOrtak;

/// <summary>
/// Bütün otomasyonlarda aynı olan R12 testleri: dar ekranda tek satırlık üst çubuk (A1) ve ana sayfa kısayol listesinde
/// menü simgeleri (A2). Ayrıntılı denetimler <see cref="YerlesimDenetimi"/>'ndedir.
/// </summary>
public abstract partial class OtomasyonTemelTestleri<TProgram>
{
    [Fact]
    public async Task Dar_ekranda_ust_cubuk_tek_satir_Uygulamalarim_yalniz_simge_tam_ad_ve_metin_DOMda_44px_hedef()
    {
        var html = await OtomasyonFactory.MetinAsync(await Istemci(AnaRol).GetAsync("/"));

        YerlesimDenetimi.DarEkranUstCubuguDogrula(html,
            await CssAsync("/_content/Selcuk.Arayuz/css/selcuk.css"),
            await CssAsync("/_content/Selcuk.Otomasyon.Ortak/css/otomasyon.css"),
            Ad, KimlikSunucusuKoku());
    }

    [Fact]
    public async Task Ana_sayfa_kisayollarinin_simgeleri_aria_hidden_menu_simgesiyle_ayni_metin_ve_baglantilar_degismemis()
    {
        foreach (var (rol, _) in Kullanicilar)
        {
            var html = await OtomasyonFactory.MetinAsync(await Istemci(rol).GetAsync("/"));
            var beklenen = Menu.Where(o => o.Roller.Contains(rol)).Select(o => (o.Baslik, o.Sayfa, o.Aciklama, o.Simge)).ToList();

            YerlesimDenetimi.KisayolSimgeleriniDogrula(html, beklenen, rol);
        }
    }

    [Fact]
    public async Task Kisayol_satirlarinin_CSS_simge_solda_baglanti_satiri_kaplar()
    {
        var css = await CssAsync("/_content/Selcuk.Otomasyon.Ortak/css/otomasyon.css");

        Assert.Matches(@"\.kisayollar > li\s*\{[^}]*position:\s*relative;[^}]*display:\s*flex;[^}]*align-items:\s*center", css);
        Assert.Matches(@"\.kisayol\s*\{[^}]*flex:\s*1;[^}]*min-width:\s*0", css);
        Assert.Matches(@"\.kisayol::after\s*\{[^}]*position:\s*absolute;[^}]*inset:\s*0", css);
    }
}
