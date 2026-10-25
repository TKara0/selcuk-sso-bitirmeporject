using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Selcuk.Demo.Api.Data;

namespace Selcuk.Demo.Api.Tests;

/// <summary>
/// Demo API'nin token doğrulaması ve yetkilendirmesi. Kimlik sunucusu yerine yerel bir RSA anahtarı ve sabit
/// discovery bilgisi kullanılır; token'lar kimlik sunucusunun access token'ı gibi üretilir (typ at+jwt).
/// </summary>
public sealed class NotlarApiTests : IDisposable
{
    private const string Issuer = "https://localhost:5001/";
    private const string AnahtarKimligi = "test-anahtar";

    private static readonly RsaSecurityKey ImzaAnahtari = new(RSA.Create(2048)) { KeyId = AnahtarKimligi };

    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class ApiFactory(INotDeposu? depo = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var yapilandirma = new OpenIdConnectConfiguration { Issuer = Issuer };
                    yapilandirma.SigningKeys.Add(new RsaSecurityKey(ImzaAnahtari.Rsa.ExportParameters(false)) { KeyId = AnahtarKimligi });
                    options.Configuration = yapilandirma;
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(yapilandirma);
                });
                if (depo is not null)
                {
                    services.RemoveAll<INotDeposu>();
                    services.AddSingleton(depo);
                }
            });
        }

        public HttpClient Istemci(string? token = null)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            if (token is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }
    }

    /// <summary>Kimlik sunucusunun access token'ına benzer bir token; testler alanları değiştirebilir.</summary>
    private static string Token(string sub = "ogrenci-1", string audience = "demo-api", string scope = "openid profile roles notlar.oku",
        string rol = "ogrenci", DateTime? bitis = null, SecurityKey? anahtar = null, string tur = "at+jwt")
    {
        var son = bitis ?? DateTime.UtcNow.AddMinutes(15);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            TokenType = tur,
            Issuer = Issuer,
            Audience = audience,
            IssuedAt = son.AddMinutes(-15),
            NotBefore = son.AddMinutes(-15),
            Expires = son,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = sub,
                ["scope"] = scope,
                ["role"] = rol,
                ["client_id"] = "demo-obis",
                ["sid"] = "oturum-1",
            },
            SigningCredentials = new SigningCredentials(anahtar ?? ImzaAnahtari, SecurityAlgorithms.RsaSha256),
        });
    }

    [Fact]
    public async Task Gecerli_token_200_eksik_veya_hatali_token_401_yetkisiz_kapsam_ve_rol_403()
    {
        var gecerli = await _factory.Istemci(Token()).GetAsync("/api/notlar");
        Assert.Equal(HttpStatusCode.OK, gecerli.StatusCode);
        Assert.True(gecerli.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", gecerli.Headers.GetValues("X-Content-Type-Options").Single());
        using var json = JsonDocument.Parse(await gecerli.Content.ReadAsStringAsync());
        Assert.Equal(5, json.RootElement.GetProperty("notlar").GetArrayLength());

        var durumlar = new (string Ad, string? Token, HttpStatusCode Beklenen)[]
        {
            ("token yok", null, HttpStatusCode.Unauthorized),
            ("yanlış imza", Token(anahtar: new RsaSecurityKey(RSA.Create(2048)) { KeyId = AnahtarKimligi }), HttpStatusCode.Unauthorized),
            ("aud demo-obis", Token(audience: "demo-obis"), HttpStatusCode.Unauthorized),
            ("süresi dolmuş", Token(bitis: DateTime.UtcNow.AddMinutes(-5)), HttpStatusCode.Unauthorized),
            ("access token değil (typ JWT)", Token(tur: "JWT"), HttpStatusCode.Unauthorized),
            ("notlar.oku kapsamı yok", Token(scope: "openid profile roles"), HttpStatusCode.Forbidden),
            ("rol akademisyen", Token(rol: "akademisyen"), HttpStatusCode.Forbidden),
        };
        foreach (var (ad, token, beklenen) in durumlar)
        {
            var yanit = await _factory.Istemci(token).GetAsync("/api/notlar");
            Assert.True(beklenen == yanit.StatusCode, $"{ad}: {yanit.StatusCode}");
            // Hata ayrıntısı sızdırılmaz: gövde boş, WWW-Authenticate'te error_description yok.
            Assert.Equal(string.Empty, await yanit.Content.ReadAsStringAsync());
            Assert.DoesNotContain(yanit.Headers.WwwAuthenticate, h => h.Parameter?.Contains("error") == true);
            Assert.True(yanit.Headers.CacheControl?.NoStore, ad);
        }
    }

    [Fact]
    public async Task Yalnizca_tokendaki_subin_notlari_donuyor_parametreyle_baskasi_istenemiyor()
    {
        var depo = new SozlukDeposu(new()
        {
            ["ogrenci-a"] = [new DersNotu("BIL101", "Programlamaya Giriş", 90, 95, "AA")],
            ["ogrenci-b"] = [new DersNotu("MAT201", "Lineer Cebir", 40, 45, "FF")],
        });
        using var factory = new ApiFactory(depo);

        foreach (var adres in new[] { "/api/notlar", "/api/notlar?sub=ogrenci-b", "/api/notlar?ogrenci=ogrenci-b" })
        {
            var yanit = await factory.Istemci(Token(sub: "ogrenci-a")).GetFromJsonAsync<JsonElement>(adres);
            Assert.Equal("ogrenci-a", yanit.GetProperty("ogrenci").GetString());
            Assert.Equal("BIL101", Assert.Single(yanit.GetProperty("notlar").EnumerateArray()).GetProperty("kod").GetString());
        }

        var b = await factory.Istemci(Token(sub: "ogrenci-b")).GetFromJsonAsync<JsonElement>("/api/notlar");
        Assert.Equal("MAT201", Assert.Single(b.GetProperty("notlar").EnumerateArray()).GetProperty("kod").GetString());
        Assert.Equal(["ogrenci-a", "ogrenci-a", "ogrenci-a", "ogrenci-b"], depo.Istenenler);
    }

    private sealed class SozlukDeposu(Dictionary<string, IReadOnlyList<DersNotu>> notlar) : INotDeposu
    {
        public List<string> Istenenler { get; } = [];

        public IReadOnlyList<DersNotu> Notlar(string ogrenciKimligi)
        {
            Istenenler.Add(ogrenciKimligi);
            return notlar.TryGetValue(ogrenciKimligi, out var liste) ? liste : [];
        }
    }
}
