using System.Net;

namespace Selcuk.Demo.Yemekhane.Tests;

/// <summary>
/// Ortak kütüphanenin güvenlik başlıkları: giriş gerektiren sayfalar Cache-Control: no-store ile döner; herkese açık
/// haftalık menü (ana sayfa) ve statik dosyalar normal önbelleklenir.
/// </summary>
public sealed class OnbellekTests
{
    [Fact]
    public async Task Korumali_sayfalarda_no_store_var_haftalik_menude_yok()
    {
        using var factory = new YemekhaneFactory(sahteKimlik: true);

        foreach (var (rol, sayfa) in new[] { ("ogrenci", "/Rezervasyonlarim"), ("ogrenci", "/Profilim"), ("personel", "/YemekhaneYonetimi") })
        {
            var yanit = await factory.HttpsIstemcisi(rol).GetAsync(sayfa);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.True(yanit.Headers.CacheControl?.NoStore, sayfa);
        }

        // Girişsiz korumalı sayfa: kimlik sunucusuna yönlendirme de önbelleğe alınmaz (formsuz yanıt; başlık ortak
        // kütüphaneden gelir, antiforgery'den değil).
        using var girissiz = new YemekhaneFactory();
        var yonlendirme = await girissiz.HttpsIstemcisi().GetAsync("/Rezervasyonlarim");
        Assert.Equal(HttpStatusCode.Redirect, yonlendirme.StatusCode);
        Assert.True(yonlendirme.Headers.CacheControl?.NoStore);

        foreach (var acik in new[] { "/", "/css/site.css" })
        {
            var yanit = await factory.HttpsIstemcisi().GetAsync(acik);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.False(yanit.Headers.CacheControl?.NoStore ?? false, acik);
        }
    }
}
