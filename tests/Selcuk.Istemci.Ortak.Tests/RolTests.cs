namespace Selcuk.Istemci.Ortak.Tests;

/// <summary>
/// Kimlik sunucusunun role claim'i tek rollü kullanıcıda metin, çok rollüde JSON dizisidir; AddSelcukSSO ikisini de
/// ayrı role claim'lerine çevirir ve User.IsInRole her rol için doğru sonuç verir (tam OIDC girişiyle).
/// </summary>
public sealed class RolTests
{
    private static async Task<string> RoldeMiAsync(HttpClient tarayici, string rol) =>
        await tarayici.GetStringAsync($"/test/rolde-mi?rol={Uri.EscapeDataString(rol)}");

    [Fact]
    public async Task Cok_rollu_kullanicida_IsInRole_her_rolde_true_olmayan_rolde_false()
    {
        await using var uygulama = await TestUygulamasi.BaslatAsync();
        using var tarayici = uygulama.YeniTarayici();
        string[] roller = [Roller.Akademisyen, Roller.BolumBaskani, Roller.BolumKuruluUyesi, Roller.Danisman];

        await uygulama.OidcGirisiAsync(tarayici, roller);

        foreach (var rol in roller)
        {
            Assert.Equal("evet", await RoldeMiAsync(tarayici, rol));
        }

        foreach (var rol in Roller.Tumu.Except(roller))
        {
            Assert.Equal("hayir", await RoldeMiAsync(tarayici, rol));
        }

        // Dizinin metin hâli rol sayılmaz.
        Assert.Equal("hayir", await RoldeMiAsync(tarayici, string.Join(",", roller)));
    }

    [Fact]
    public async Task Tek_rollu_kullanicida_metin_bicimi_ayni_sekilde_isleniyor()
    {
        await using var uygulama = await TestUygulamasi.BaslatAsync();
        using var tarayici = uygulama.YeniTarayici();

        await uygulama.OidcGirisiAsync(tarayici, Roller.Ogrenci);

        Assert.Equal("evet", await RoldeMiAsync(tarayici, Roller.Ogrenci));
        Assert.Equal("hayir", await RoldeMiAsync(tarayici, Roller.Akademisyen));
    }
}
