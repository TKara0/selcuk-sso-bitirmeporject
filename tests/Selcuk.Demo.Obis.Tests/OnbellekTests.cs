using System.Net;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>
/// Ortak kütüphanenin güvenlik başlıkları: giriş gerektiren sayfalar Cache-Control: no-store ile döner (çıkıştan sonra
/// geri tuşu önbellekten kişisel sayfa göstermesin); herkese açık ana sayfa ve statik dosyalar normal önbelleklenir.
/// </summary>
public sealed class OnbellekTests
{
    [Fact]
    public async Task Korumali_sayfalarda_no_store_var_ana_sayfa_ve_statik_dosyada_yok()
    {
        using var factory = new ObisFactory(sahteKimlik: true);

        foreach (var (rol, sayfa) in new[] { ("ogrenci", "/Profilim"), ("ogrenci", "/Notlarim"), ("akademisyen", "/DersYonetimi") })
        {
            var yanit = await factory.HttpsIstemcisi(rol).GetAsync(sayfa);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.True(yanit.Headers.CacheControl?.NoStore, sayfa);
        }

        // Girişsiz korumalı sayfa: kimlik sunucusuna yönlendirme de önbelleğe alınmaz. (Oturum açıkken düzendeki çıkış
        // formu yüzünden antiforgery de no-store ekler; formsuz bu yanıt başlığın ortak kütüphaneden geldiğini gösterir.)
        using var girissiz = new ObisFactory();
        var yonlendirme = await girissiz.HttpsIstemcisi().GetAsync("/Notlarim");
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
