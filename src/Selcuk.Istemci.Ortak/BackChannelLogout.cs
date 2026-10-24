using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Selcuk.Istemci.Ortak;

/// <summary>
/// OpenID Connect Back-Channel Logout 1.0 alıcısı. Kimlik sunucusu, kullanıcı çıkış yaptığında bu adrese
/// imzalı bir logout_token gönderir; geçerliyse token'daki oturum (sid) iptal edilir.
/// </summary>
internal static class BackChannelLogout
{
    public const string Yol = "/signout-backchannel";
    public const string OlayAdi = "http://schemas.openid.net/event/backchannel-logout";

    private static readonly TimeSpan SaatKaymasi = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan EnFazlaYas = TimeSpan.FromMinutes(5);

    public static async Task<IResult> AlAsync(
        HttpContext context,
        IOptionsMonitor<OpenIdConnectOptions> oidcAyarlari,
        OturumIptalDeposu depo,
        TimeProvider zaman,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(BackChannelLogout));
        context.Response.Headers.CacheControl = "no-store";

        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest();
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var token = form["logout_token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            return Results.BadRequest();
        }

        var ayarlar = oidcAyarlari.Get(OpenIdConnectDefaults.AuthenticationScheme);
        OpenIdConnectConfiguration yapilandirma;
        try
        {
            yapilandirma = await ayarlar.ConfigurationManager!.GetConfigurationAsync(context.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Kimlik sunucusunun discovery veya JWKS adresine ulaşılamıyor: token doğrulanamaz. Geçici bir durum
            // olduğundan 500 yerine 503 döner; kimlik sunucusu bunu başarısız gönderim olarak loglar.
            logger.LogWarning("logout_token doğrulanamadı: discovery/JWKS alınamadı ({Hata}).", ex.GetType().Name);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        // İmza (kimlik sunucusunun JWKS'i), iss, aud (client_id), exp ve typ doğrulaması.
        var sonuc = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = yapilandirma.Issuer,
            ValidAudience = ayarlar.ClientId,
            IssuerSigningKeys = yapilandirma.SigningKeys,
            ValidTypes = ["logout+jwt"],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = SaatKaymasi,
        });
        if (!sonuc.IsValid)
        {
            // Token içeriği loglanmaz; yalnızca hata türü.
            logger.LogWarning("Geçersiz logout_token reddedildi: {Hata}", sonuc.Exception?.GetType().Name);
            return Results.BadRequest();
        }

        var jwt = (JsonWebToken)sonuc.SecurityToken;
        if (!IceriGecerli(jwt, zaman.GetUtcNow(), out var sid, out var sub, out var jti, out var neden))
        {
            logger.LogWarning("Geçersiz logout_token reddedildi: {Neden}", neden);
            return Results.BadRequest();
        }

        // Tekrar oynatma koruması: aynı jti token geçerli olduğu sürece bir kez kabul edilir.
        if (!await depo.JtiIlkKezMiAsync(jti, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero) + SaatKaymasi,
                context.RequestAborted))
        {
            logger.LogWarning("Daha önce kullanılmış logout_token (jti) reddedildi.");
            return Results.BadRequest();
        }

        if (sid is not null)
        {
            await depo.SidIptalEtAsync(sid, context.RequestAborted);
        }
        else
        {
            await depo.SubIptalEtAsync(sub!, context.RequestAborted);
        }

        return Results.Ok();
    }

    /// <summary>Spesifikasyonun imza dışındaki kuralları: events, nonce yok, iat, jti, sid veya sub.</summary>
    private static bool IceriGecerli(JsonWebToken jwt, DateTimeOffset simdi,
        out string? sid, out string? sub, out string jti, out string neden)
    {
        sid = sub = null;
        jti = string.Empty;

        using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(jwt.EncodedPayload));
        var kok = payload.RootElement;

        if (!kok.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Object
            || !events.TryGetProperty(OlayAdi, out var olay) || olay.ValueKind != JsonValueKind.Object)
        {
            neden = "events claim'i eksik veya hatalı";
            return false;
        }

        if (kok.TryGetProperty("nonce", out _))
        {
            neden = "nonce claim'i bulunmamalı";
            return false;
        }

        if (!kok.TryGetProperty("iat", out var iat) || !iat.TryGetInt64(out var iatSaniye))
        {
            neden = "iat claim'i eksik";
            return false;
        }

        var verilis = DateTimeOffset.FromUnixTimeSeconds(iatSaniye);
        if (verilis > simdi + SaatKaymasi || verilis < simdi - EnFazlaYas)
        {
            neden = "iat makul aralıkta değil";
            return false;
        }

        if (!kok.TryGetProperty("jti", out var jtiDegeri) || jtiDegeri.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(jti = jtiDegeri.GetString()!))
        {
            neden = "jti claim'i eksik";
            return false;
        }

        sid = kok.TryGetProperty("sid", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        sub = kok.TryGetProperty("sub", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
        if (string.IsNullOrEmpty(sid) && string.IsNullOrEmpty(sub))
        {
            neden = "sid veya sub claim'i yok";
            return false;
        }

        if (string.IsNullOrEmpty(sid))
        {
            sid = null;
        }

        neden = string.Empty;
        return true;
    }
}
