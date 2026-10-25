using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using Selcuk.Identity.Oidc;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// OpenIddict budaması (<see cref="TokenBudamaHizmeti"/>): süresi dolmuş, iptal edilmiş token'lar ve yetim
/// yetkilendirmeler silinir; geçerliler, kalıcı onaylar ve en az yaştan genç kayıtlar kalır.
/// </summary>
public sealed class TokenBudamaTests : IDisposable
{
    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void Aralik_ve_en_az_yas_yapilandirilabilir_en_az_yas_mutlak_oturum_suresinden_kisa_olamaz()
    {
        var varsayilan = _factory.Services.GetRequiredService<TokenBudamaHizmeti>();
        Assert.Equal(TimeSpan.FromDays(1), varsayilan.Aralik);
        Assert.Equal(TimeSpan.FromDays(14), varsayilan.EnAzYas);

        using var factory = new SelcukIdentityFactory(ayarlar: new Dictionary<string, string?>
        {
            [TokenBudamaHizmeti.AralikAyari] = "06:00:00",
            [TokenBudamaHizmeti.EnAzYasAyari] = "01:00:00",
        });
        var hizmet = factory.Services.GetRequiredService<TokenBudamaHizmeti>();
        Assert.Equal(TimeSpan.FromHours(6), hizmet.Aralik);
        // Kullanılmış refresh token'lar zincir yaşadığı sürece tekrar kullanım tespiti için saklanmalı.
        Assert.Equal(YenilemeUstSiniri.VarsayilanMutlakSure, hizmet.EnAzYas);
    }

    [Fact]
    public async Task Budama_suresi_dolmus_ve_iptal_edilmis_kayitlari_siliyor_gecerlileri_silmiyor()
    {
        var hizmet = _factory.Services.GetRequiredService<TokenBudamaHizmeti>();
        // Açılıştaki ilk budama bu kayıtlardan önce bitmiş olsun (sonucu etkilemez, yalnızca sayıları sabitler).
        await hizmet.BudaAsync();

        var simdi = DateTimeOffset.UtcNow;
        var eski = simdi.AddDays(-20);
        await using var scope = _factory.Services.CreateAsyncScope();
        var uygulamalar = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var yetkiler = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokenlar = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var uygulamaId = (await uygulamalar.GetIdAsync(await uygulamalar.CreateAsync(Istemciler.DemoObis("budama-test-secret"))))!;

        async Task<string> YetkiAsync(string tur, string durum)
        {
            var yetki = await yetkiler.CreateAsync(new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = uygulamaId,
                CreationDate = eski,
                Status = durum,
                Subject = "kullanici-1",
                Type = tur,
            });
            return (await yetkiler.GetIdAsync(yetki))!;
        }

        async Task<string> TokenAsync(string? yetkiId, DateTimeOffset olusturma, DateTimeOffset bitis, string durum)
        {
            var token = await tokenlar.CreateAsync(new OpenIddictTokenDescriptor
            {
                ApplicationId = uygulamaId,
                AuthorizationId = yetkiId,
                CreationDate = olusturma,
                ExpirationDate = bitis,
                Status = durum,
                Subject = "kullanici-1",
                Type = TokenTypeIdentifiers.RefreshToken,
            });
            return (await tokenlar.GetIdAsync(token))!;
        }

        var oturum = await YetkiAsync(AuthorizationTypes.AdHoc, Statuses.Valid);
        var gecerli = await TokenAsync(oturum, eski, simdi.AddHours(1), Statuses.Valid);
        var suresiDolmus = await TokenAsync(oturum, eski, eski.AddHours(8), Statuses.Valid);
        var iptal = await TokenAsync(oturum, eski, simdi.AddHours(1), Statuses.Revoked);
        var kullanilmis = await TokenAsync(oturum, eski, eski.AddHours(8), Statuses.Redeemed);
        // Süresi dolmuş ama en az yaştan (14 gün) genç: tekrar kullanım tespiti için kalır.
        var yeniSuresiDolmus = await TokenAsync(oturum, simdi.AddHours(-2), simdi.AddHours(-1), Statuses.Redeemed);

        var yetim = await YetkiAsync(AuthorizationTypes.AdHoc, Statuses.Valid);
        var iptalYetki = await YetkiAsync(AuthorizationTypes.AdHoc, Statuses.Revoked);
        var iptalYetkininTokeni = await TokenAsync(iptalYetki, eski, simdi.AddHours(1), Statuses.Valid);
        var kaliciOnay = await YetkiAsync(AuthorizationTypes.Permanent, Statuses.Valid);

        var (silinenToken, silinenYetki) = await hizmet.BudaAsync();

        // Yeni kapsam: OpenIddict'in kapsam içi önbelleği silinen kayıtları göstermesin.
        await using var kontrol = _factory.Services.CreateAsyncScope();
        tokenlar = kontrol.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        yetkiler = kontrol.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        Assert.Equal(4, silinenToken);
        Assert.Equal(2, silinenYetki);
        foreach (var id in new[] { gecerli, yeniSuresiDolmus })
        {
            Assert.NotNull(await tokenlar.FindByIdAsync(id));
        }

        foreach (var id in new[] { suresiDolmus, iptal, kullanilmis, iptalYetkininTokeni })
        {
            Assert.Null(await tokenlar.FindByIdAsync(id));
        }

        Assert.NotNull(await yetkiler.FindByIdAsync(oturum));
        Assert.NotNull(await yetkiler.FindByIdAsync(kaliciOnay));
        Assert.Null(await yetkiler.FindByIdAsync(yetim));
        Assert.Null(await yetkiler.FindByIdAsync(iptalYetki));
    }
}
