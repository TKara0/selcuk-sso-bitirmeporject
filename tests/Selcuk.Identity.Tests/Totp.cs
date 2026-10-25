using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Identity;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Testlerde telefondaki doğrulayıcı uygulamanın yerine geçer. Identity'nin AuthenticatorTokenProvider'ının kodu
/// doğrularken kullandığı fonksiyonları çağırır: <c>Base32.FromBase32</c> ve
/// <c>Rfc6238AuthenticationService.ComputeTotp</c>. İkisi de Identity'de <c>internal</c> olduğu için yansımayla
/// (reflection) erişilir; HMAC, Base32 veya RFC 6238 elle yazılmadı. Zaman adımı da sağlayıcıyla aynı: Unix saniyesi / 30.
/// </summary>
public static class Totp
{
    private static readonly Assembly IdentityCore = typeof(AuthenticatorTokenProvider<>).Assembly;

    private static readonly MethodInfo ComputeTotp = IcMetot("Rfc6238AuthenticationService", "ComputeTotp");
    private static readonly MethodInfo FromBase32 = IcMetot("Base32", "FromBase32");

    /// <summary>Paylaşılan anahtar için şu anki (adimFarki = 0) veya komşu zaman adımındaki 6 haneli kod.</summary>
    public static string Uret(string anahtar, int adimFarki = 0) => Kodlar(anahtar, adimFarki)[0];

    /// <summary>
    /// Tek bir zaman ölçümünden hesaplanan, verilen adım farklarındaki kodlar. Tekrar oynatma testlerinde farklı zaman
    /// adımlarına ait kodlar gerekir; ayrı ayrı <see cref="Uret"/> çağrıları arasında 30 saniyelik sınır geçilirse iki
    /// "farklı" kod aynı adıma düşebilir, burada düşmez.
    /// </summary>
    public static string[] Kodlar(string anahtar, params int[] adimFarklari)
    {
        var anahtarBaytlari = (byte[])FromBase32.Invoke(null, [anahtar])!;
        var unixSaniye = Convert.ToInt64(Math.Round((DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds));
        return [.. adimFarklari.Select(fark =>
        {
            var adim = (ulong)(unixSaniye / 30 + fark);
            var kod = (int)ComputeTotp.Invoke(null, [anahtarBaytlari, adim, null])!;
            return kod.ToString("D6", CultureInfo.InvariantCulture);
        })];
    }

    /// <summary>
    /// Doğrulayıcının kabul ettiği pencerede (±2 adım; burada güvenlik payıyla ±3) geçerli olmayan bir 6 haneli kod.
    /// </summary>
    public static string Gecersiz(string anahtar)
    {
        var gecerliler = Enumerable.Range(-3, 7).Select(fark => Uret(anahtar, fark)).ToHashSet();
        return Enumerable.Range(0, 100)
            .Select(i => ((123_457 + i * 7_919) % 1_000_000).ToString("D6", CultureInfo.InvariantCulture))
            .First(kod => !gecerliler.Contains(kod));
    }

    private static MethodInfo IcMetot(string tur, string metot) =>
        IdentityCore.GetTypes().Single(t => t.Name == tur)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(m => m.Name == metot);
}
