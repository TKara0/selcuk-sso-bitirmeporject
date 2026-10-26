using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;
using Selcuk.Demo.Obis.Pages;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>
/// Token İnceleme (yalnızca Development): her claim bir satır, adı ve değeri Mono, yanında Türkçe açıklama. Sahte
/// token'lar kimlik sunucusunun (OpenIddict) gerçek id_token ve access_token'larındaki alanlarla aynıdır; imzaları
/// yoktur (sayfa imza doğrulamaz, yalnızca çözer). Token'ın kendisi sayfada gösterilmez.
/// </summary>
public sealed class TokenIncelemeTests
{
    private static string Jwt(object baslik, object icerik) =>
        $"{Base64UrlEncoder.Encode(JsonSerializer.Serialize(baslik))}.{Base64UrlEncoder.Encode(JsonSerializer.Serialize(icerik))}.sahte-imza";

    private static readonly long Simdi = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static readonly string IdToken = Jwt(
        new Dictionary<string, object> { ["alg"] = "RS256", ["kid"] = "ANAHTAR", ["x5t"] = "PARMAKIZI", ["typ"] = "JWT" },
        new Dictionary<string, object>
        {
            ["iss"] = "https://localhost:5001/",
            ["exp"] = Simdi + 1200,
            ["iat"] = Simdi,
            ["aud"] = "demo-obis",
            ["sub"] = "6f1c2f4e-0000-4000-8000-000000000001",
            ["name"] = "Test Öğrenci",
            ["preferred_username"] = "203011001",
            ["email"] = "203011001@test.local",
            ["fakulte"] = "Teknoloji Fakültesi",
            ["bolum"] = "Bilgisayar Mühendisliği",
            ["role"] = "ogrenci",
            ["sid"] = "oturum-kimligi-ornek",
            ["amr"] = new[] { "pwd", "mfa" },
            ["oi_au_id"] = "yetkilendirme-ornek",
            ["azp"] = "demo-obis",
            ["nonce"] = "tek-kullanimlik-deger-ornek-uzun-bir-nonce-degeri-0123456789",
            ["at_hash"] = "ozet-ornek",
            ["oi_tkn_id"] = "token-ornek",
        });

    private static readonly string AccessToken = Jwt(
        new Dictionary<string, object> { ["alg"] = "RS256", ["kid"] = "ANAHTAR", ["x5t"] = "PARMAKIZI", ["typ"] = "at+jwt" },
        new Dictionary<string, object>
        {
            ["iss"] = "https://localhost:5001/",
            ["exp"] = Simdi + 900,
            ["iat"] = Simdi,
            ["aud"] = "demo-api",
            ["scope"] = "openid profile email roles offline_access notlar.oku",
            ["jti"] = "benzersiz-ornek",
            ["sub"] = "6f1c2f4e-0000-4000-8000-000000000001",
            ["sid"] = "oturum-kimligi-ornek",
            ["role"] = "ogrenci",
            ["oi_prst"] = "demo-obis",
            ["oi_au_id"] = "yetkilendirme-ornek",
            ["client_id"] = "demo-obis",
            ["oi_tkn_id"] = "token-ornek",
        });

    private static async Task<string> SayfaAsync(string ortam)
    {
        using var factory = new ObisFactory(sahteKimlik: true, ortam: ortam, tokenlar:
        [
            new AuthenticationToken { Name = "id_token", Value = IdToken },
            new AuthenticationToken { Name = "access_token", Value = AccessToken },
            new AuthenticationToken { Name = "refresh_token", Value = "sahte-refresh-token" },
        ]);
        var yanit = await factory.HttpsIstemcisi("ogrenci").GetAsync("/TokenInceleme");
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Her_claim_Turkce_aciklamasiyla_bir_satirda_gosteriliyor()
    {
        var html = await SayfaAsync("Development");

        foreach (var token in new[] { IdToken, AccessToken })
        {
            var parcalar = token.Split('.');
            foreach (var parca in parcalar[..2])
            {
                using var json = JsonDocument.Parse(Base64UrlEncoder.Decode(parca));
                foreach (var alan in json.RootElement.EnumerateObject())
                {
                    var aciklama = TokenIncelemeModel.AciklamaBul(alan.Name);
                    Assert.NotEqual(TokenIncelemeModel.AciklamaYok, aciklama);
                    Assert.Matches(new Regex(
                        $@"<th scope=""row"" class=""claim-adi mono"">{Regex.Escape(alan.Name)}</th>\s*<td class=""claim-degeri"">.*?</td>\s*" +
                        $@"<td class=""claim-aciklamasi"">{Regex.Escape(aciklama)}</td>", RegexOptions.Singleline), html);
                }
            }
        }

        Assert.DoesNotContain(TokenIncelemeModel.AciklamaYok, html);
        Assert.Contains("<td class=\"claim-aciklamasi\">Token'ı veren kimlik sunucusu</td>", html);
        Assert.Contains("<td class=\"claim-aciklamasi\">Token'ın geçerli olduğu alıcı</td>", html);
        Assert.Contains("<td class=\"claim-aciklamasi\">Kimlik sunucusundaki oturum</td>", html);
        Assert.Contains("<td class=\"claim-aciklamasi\">Giriş yöntemi</td>", html);
    }

    [Fact]
    public async Task Access_token_bandi_ham_hal_ve_kirpilmis_deger_var_token_ve_refresh_token_degeri_yok()
    {
        var html = await SayfaAsync("Development");

        Assert.Contains("Bu token kişisel bilgi içermez. Ad, e-posta, fakülte ve bölüm yalnızca id_token'da ve userinfo'da bulunur.", html);
        Assert.Equal(2, Regex.Matches(html, "<summary>Ham hâlini göster</summary>").Count);
        // Uzun değer kırpılır, tam hâli title'da.
        Assert.Contains("title=\"tek-kullanimlik-deger-ornek-uzun-bir-nonce-degeri-0123456789\">tek-kullanimlik-deger-ornek-uzun-bir…</span>", html);
        Assert.Contains("[\"pwd\",\"mfa\"]", html);
        Assert.DoesNotContain(IdToken, html);
        Assert.DoesNotContain(AccessToken, html);
        Assert.DoesNotContain("sahte-imza", html);
        Assert.DoesNotContain("sahte-refresh-token", html);
    }
}
