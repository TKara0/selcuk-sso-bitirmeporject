using System.Net;
using System.Text.Json;

namespace Selcuk.Identity.Tests;

public sealed class DiscoveryTests : IDisposable
{
    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<JsonElement> DiscoveryAsync()
    {
        var yanit = await _factory.HttpsIstemcisi().GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return JsonDocument.Parse(await yanit.Content.ReadAsStringAsync()).RootElement;
    }

    private static string[] Dizi(JsonElement belge, string ad) =>
        [.. belge.GetProperty(ad).EnumerateArray().Select(e => e.GetString()!)];

    [Fact]
    public async Task Discovery_dokumani_donuyor_ve_issuer_dogru()
    {
        var belge = await DiscoveryAsync();

        Assert.Equal("https://localhost:5001/", belge.GetProperty("issuer").GetString());

        // Uç nokta adreslerinin sunucu kısmı isteğin geldiği adresten türetilir; yollar sabittir.
        Assert.EndsWith("/connect/authorize", belge.GetProperty("authorization_endpoint").GetString());
        Assert.EndsWith("/connect/token", belge.GetProperty("token_endpoint").GetString());
        Assert.EndsWith("/connect/userinfo", belge.GetProperty("userinfo_endpoint").GetString());
        Assert.EndsWith("/connect/logout", belge.GetProperty("end_session_endpoint").GetString());
    }

    [Fact]
    public async Task Discovery_grant_turleri_ve_S256_iceriyor()
    {
        var belge = await DiscoveryAsync();

        var grantlar = Dizi(belge, "grant_types_supported");
        Assert.Contains("authorization_code", grantlar);
        Assert.Contains("refresh_token", grantlar);
        Assert.DoesNotContain("password", grantlar);
        Assert.DoesNotContain("implicit", grantlar);

        Assert.Contains("S256", Dizi(belge, "code_challenge_methods_supported"));
    }

    [Fact]
    public async Task Discovery_claims_supported_amr_iceriyor()
    {
        Assert.Contains("amr", Dizi(await DiscoveryAsync(), "claims_supported"));
    }
}
