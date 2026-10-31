using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Selcuk.Otomasyon.Ortak.Tests;

/// <summary>
/// Açılış denetimi (menüde olmayan ve rol istemeyen sayfa açılışı durdurur; ortak sayfalar istisna), oturum bileti
/// deposunun süreleri ve silmeleri, ortak parçaların modelleri.
/// </summary>
public sealed class AcilisVeParcaTests
{
    // ---------- Açılış denetimi ----------

    /// <summary>Bu test projesinin sahte sayfalarıyla (Pages/Gizli) ve ortak sayfalarla bir otomasyon uygulaması.</summary>
    private static WebApplication SahteUygulama(params MenuOgesi[] menu)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(AcilisVeParcaTests).Assembly.GetName().Name,
            EnvironmentName = "Testing",
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kimlik:ClientId"] = "test-otomasyon",
            ["Kimlik:Authority"] = "https://kimlik.test.local/",
        });
        builder.Services.AddSelcukOtomasyon(builder.Configuration, s =>
        {
            s.Ad = "Sahte Otomasyon";
            s.Simge = "SA";
            s.Menu.AddRange(menu);
            s.VeriKaynagi<IOrnekKaynak, MockKaynak, ApiKaynak>();
        });
        builder.Services.AddMvcCore().ConfigureApplicationPartManager(parcalar =>
        {
            foreach (var assembly in new[] { typeof(AcilisVeParcaTests).Assembly, typeof(OtomasyonUzantilari).Assembly })
            {
                if (!parcalar.ApplicationParts.Any(p => p is CompiledRazorAssemblyPart c && c.Assembly == assembly))
                {
                    parcalar.ApplicationParts.Add(new CompiledRazorAssemblyPart(assembly));
                }
            }
        });
        return builder.Build();
    }

    private interface IOrnekKaynak;

    private sealed class MockKaynak : IOrnekKaynak;

    private sealed class ApiKaynak : IOrnekKaynak;

    [Fact]
    public async Task Menude_olmayan_ve_rol_istemeyen_sayfa_acilisi_Turkce_hatayla_durduruyor()
    {
        await using var app = SahteUygulama();

        Assert.Equal(["/Gizli/Acik", "/Gizli/Rapor"], (await SayfaYetkiDenetimi.KuralDisiSayfalarAsync(app.Services)).Order());
        var hata = Assert.Throws<InvalidOperationException>(() => app.UseSelcukOtomasyon());
        Assert.Contains("Menüde olmayan ve rol istemeyen sayfa var", hata.Message);
        Assert.Contains("/Gizli/Rapor", hata.Message);
        Assert.Contains("[Authorize(Roles = …)]", hata.Message);
        // Kendi rolünü taşıyan sayfa kural dışı sayılmaz.
        Assert.DoesNotContain("/Gizli/Ozel", hata.Message.Split("Denetim dışı")[0]);
    }

    [Fact]
    public async Task Menudeki_rol_sayfalari_ve_istisna_listesindeki_ortak_sayfalar_acilisi_durdurmuyor()
    {
        await using var app = SahteUygulama(
            new MenuOgesi("Rapor", "/Gizli/Rapor", "", Roller.BolumSekreteri),
            new MenuOgesi("Açık", "/Gizli/Acik", "", Roller.Ogrenci));

        Assert.Empty(await SayfaYetkiDenetimi.KuralDisiSayfalarAsync(app.Services));
        app.UseSelcukOtomasyon();

        // Ortak sayfalar (yalnızca girişle açılan Profilim dahil) uygulamada var ve istisna listesinde açıkça adlı.
        Assert.Equal(["/Index", "/Giris", "/GirisHatasi", "/Hata", "/ErisimReddedildi", "/Cikis", "/Profilim"], SayfaYetkiDenetimi.Istisnalar);
    }

    // ---------- Oturum bileti deposu ----------

    private sealed class SahteSaat(DateTimeOffset simdi) : TimeProvider
    {
        public DateTimeOffset Simdi { get; set; } = simdi;

        public override DateTimeOffset GetUtcNow() => Simdi;
    }

    /// <summary>Yazılan kayıtların süresini tutan önbellek (bellek içi önbelleğin üstünde).</summary>
    private sealed class KaydedenOnbellek : IDistributedCache
    {
        private readonly MemoryDistributedCache _ic = new(Options.Create(new MemoryDistributedCacheOptions()));

        public Dictionary<string, TimeSpan?> Sureler { get; } = [];

        public byte[]? Get(string key) => _ic.Get(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => _ic.GetAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => SetAsync(key, value, options).GetAwaiter().GetResult();

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Sureler[key] = options.AbsoluteExpirationRelativeToNow;
            return _ic.SetAsync(key, value, options, token);
        }

        public void Refresh(string key) => _ic.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => _ic.RefreshAsync(key, token);

        public void Remove(string key) => _ic.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => _ic.RemoveAsync(key, token);
    }

    private static readonly DateTimeOffset Baslangic = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private static (OturumBiletDeposu Depo, KaydedenOnbellek Onbellek, SahteSaat Saat, ServiceProvider Servisler) Depo()
    {
        var onbellek = new KaydedenOnbellek();
        var saat = new SahteSaat(Baslangic);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kimlik:ClientId"] = "test-otomasyon",
            ["Kimlik:Authority"] = "https://kimlik.test.local/",
            ["Kimlik:OturumSuresi"] = "02:00:00",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDistributedCache>(onbellek);
        services.AddSingleton<TimeProvider>(saat);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSelcukOtomasyon(configuration, s =>
        {
            s.Ad = "Örnek";
            s.Simge = "ÖR";
            s.VeriKaynagi<IOrnekKaynak, MockKaynak, ApiKaynak>();
        });
        var provider = services.BuildServiceProvider();
        var ayarlar = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        return (Assert.IsType<OturumBiletDeposu>(ayarlar.SessionStore), onbellek, saat, provider);
    }

    private static AuthenticationTicket Bilet(string sid, string sub = "sub-1", DateTimeOffset? bitis = null) => new(
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("sid", sid), new Claim("sub", sub)], "test")),
        new AuthenticationProperties { ExpiresUtc = bitis }, CookieAuthenticationDefaults.AuthenticationScheme);

    [Fact]
    public async Task Kaydin_suresi_cerez_omruyle_ayni()
    {
        var (depo, onbellek, _, servisler) = Depo();
        using var _ = servisler;

        var bitissiz = await depo.StoreAsync(Bilet("s1"));
        var bitisli = await depo.StoreAsync(Bilet("s2", bitis: Baslangic.AddMinutes(45)));

        // Bitişi verilmemiş bilet: çerezin ömrü (Kimlik:OturumSuresi); verilmiş bilet: çerezin bitişine kadar.
        Assert.Equal(TimeSpan.FromHours(2), onbellek.Sureler["otomasyon-oturum:" + bitissiz]);
        Assert.Equal(TimeSpan.FromMinutes(45), onbellek.Sureler["otomasyon-oturum:" + bitisli]);

        // Kayan sürede yenileme kaydın süresini de uzatır.
        await depo.RenewAsync(bitisli, Bilet("s2", bitis: Baslangic.AddHours(2)));
        Assert.Equal(TimeSpan.FromHours(2), onbellek.Sureler["otomasyon-oturum:" + bitisli]);
    }

    [Fact]
    public async Task Suresi_dolan_kayit_okununca_siliniyor_dolmus_bilet_yazilmiyor()
    {
        var (depo, onbellek, saat, servisler) = Depo();
        using var _ = servisler;
        var anahtar = await depo.StoreAsync(Bilet("s1", bitis: Baslangic.AddHours(1)));
        Assert.NotNull(await depo.RetrieveAsync(anahtar));

        saat.Simdi = Baslangic.AddHours(1).AddSeconds(1);

        Assert.Null(await depo.RetrieveAsync(anahtar));
        Assert.Null(await onbellek.GetAsync("otomasyon-oturum:" + anahtar));

        var eski = await depo.StoreAsync(Bilet("s2", bitis: Baslangic));
        Assert.Null(await onbellek.GetAsync("otomasyon-oturum:" + eski));
    }

    [Fact]
    public async Task Cikis_ve_back_channel_logout_kayitlari_hemen_siliyor()
    {
        var (depo, onbellek, _, servisler) = Depo();
        using var _ = servisler;
        var a1 = await depo.StoreAsync(Bilet("sid-a"));
        var a2 = await depo.StoreAsync(Bilet("sid-a"));
        var b = await depo.StoreAsync(Bilet("sid-b"));
        var c = await depo.StoreAsync(Bilet("sid-c", sub: "sub-2"));

        // Çıkış: çerez şeması kendi anahtarını siler.
        await depo.RemoveAsync(b);
        Assert.Null(await onbellek.GetAsync("otomasyon-oturum:" + b));

        // Back-Channel Logout (sid): o oturumun bütün biletleri; başka oturum kalır.
        await servisler.GetRequiredService<Selcuk.Istemci.Ortak.OturumIptalDeposu>().SidIptalEtAsync("sid-a");
        Assert.Null(await onbellek.GetAsync("otomasyon-oturum:" + a1));
        Assert.Null(await onbellek.GetAsync("otomasyon-oturum:" + a2));
        Assert.NotNull(await depo.RetrieveAsync(c));

        // sid'siz logout_token (sub): kullanıcının bütün biletleri.
        await servisler.GetRequiredService<Selcuk.Istemci.Ortak.OturumIptalDeposu>().SubIptalEtAsync("sub-2");
        Assert.Null(await onbellek.GetAsync("otomasyon-oturum:" + c));
    }

    // ---------- Ortak parçalar ----------

    [Fact]
    public void Durum_rozeti_turu_sinifta_ve_veride_metin_adda()
    {
        Assert.Equal("etiket etiket-uygulama durum-rozeti", new DurumRozeti("Onaylandı", DurumTuru.Olumlu).Sinif);
        Assert.Equal("etiket etiket-tehlike durum-rozeti", new DurumRozeti("Reddedildi", DurumTuru.Olumsuz).Sinif);
        Assert.Equal("etiket etiket-notr durum-rozeti", new DurumRozeti("Bekliyor", DurumTuru.Bekliyor).Sinif);
        Assert.Equal("bilgi", new DurumRozeti("Not", DurumTuru.Bilgi).Veri);
    }

    [Fact]
    public void Basvuru_adimlari_simdikiden_once_tamamlandi_sonra_bekliyor()
    {
        var adimlar = BasvuruAdimlari.Olustur(["A", "B", "C"], 1).Adimlar;
        Assert.Equal([AdimDurumu.Tamamlandi, AdimDurumu.SuAn, AdimDurumu.Bekliyor], adimlar.Select(a => a.Durum));
        Assert.Equal(["Tamamlandı", "Şu an", "Bekliyor"], adimlar.Select(a => a.DurumAdi));
        Assert.All(BasvuruAdimlari.Olustur(["A", "B"], 2).Adimlar, a => Assert.Equal(AdimDurumu.Tamamlandi, a.Durum));
    }

    [Fact]
    public void Kayit_tablosu_satirlari_basligi_ve_veri_adi()
    {
        var tablo = KayitTablosu.Olustur("Komisyon incelemesindeki başvurular", new[] { (Id: "K1", Ad: "Bir") }, k => k.Id, "ornek",
            new KayitSutunu<(string Id, string Ad)>("Ad", k => k.Ad),
            new KayitSutunu<(string Id, string Ad)>("Sayı", _ => 3, Sayi: true));

        Assert.Equal("tablo-komisyon-incelemesindeki-basvurular", tablo.BaslikKimligi);
        Assert.Equal(KayitTablosu.VarsayilanBosMetin, tablo.BosMetin);
        Assert.Equal([new TabloSutunu("Ad", false), new TabloSutunu("Sayı", true)], tablo.Sutunlar);
        Assert.Equal("K1", Assert.Single(tablo.Satirlar).Kimlik);
        Assert.Throws<ArgumentException>(() => KayitTablosu.Olustur("x", Array.Empty<string>(), null, "data\" onclick=\"x"));
    }
}
