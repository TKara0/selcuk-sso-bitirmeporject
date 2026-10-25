using System.Net;
using System.Text.RegularExpressions;

namespace Selcuk.Olcum;

/// <summary>Bir HTTP yanıtının ölçüm için gereken kısmı. Gövde tamamen okunmuştur.</summary>
public sealed record Sayfa(HttpStatusCode Durum, Uri Adres, Uri? Yonlendirme, string Govde)
{
    public bool YonlendirmeMi => Yonlendirme is not null;

    /// <summary>Razor Türkçe karakterleri HTML varlığı olarak yazar; metin kontrolleri çözülmüş gövdede yapılır.</summary>
    public string Metin => WebUtility.HtmlDecode(Govde);
}

/// <summary>Bir akışta tarayıcının kullanıcı adına yapacağı form gönderimleri.</summary>
/// <param name="KullaniciAdi">Giriş formu gelirse yazılacak kullanıcı adı.</param>
/// <param name="Sifre">null ise giriş formu hata sayılır (tek giriş: şifre sorulmamalı).</param>
/// <param name="Durdur">Bu adrese yönlendirmede durulur (ör. kodu yakalamak için redirect_uri).</param>
public sealed record AkisSecenekleri(string? KullaniciAdi = null, string? Sifre = null, Func<Uri, bool>? Durdur = null);

/// <summary>
/// Tarayıcı taklidi: kendi çerez kabı olan bir HttpClient. Yönlendirmeler elle izlenir; kimlik sunucusunun giriş,
/// onay ve çıkış onayı sayfalarındaki formlar tarayıcının yapacağı gibi gönderilir (gizli alanlar ve antiforgery
/// token'ı dahil). Her istek <see cref="YalnizcaYerelHandler"/>'dan geçer.
/// </summary>
public sealed partial class TarayiciOturumu : IDisposable
{
    private const int EnFazlaAdim = 25;
    private readonly HttpClient _client;
    private readonly Uri _kimlik;

    public TarayiciOturumu(Func<CookieContainer, HttpMessageHandler> handlerFabrikasi, Uri kimlik, TimeSpan zamanAsimi)
    {
        _kimlik = kimlik;
        _client = new HttpClient(new YalnizcaYerelHandler(handlerFabrikasi(Cerezler))) { Timeout = zamanAsimi };
    }

    public CookieContainer Cerezler { get; } = new();

    public Task<Sayfa> GetAsync(Uri adres) => GonderAsync(new HttpRequestMessage(HttpMethod.Get, adres));

    public Task<Sayfa> PostAsync(Uri adres, IEnumerable<KeyValuePair<string, string>> alanlar) =>
        GonderAsync(new HttpRequestMessage(HttpMethod.Post, adres) { Content = new FormUrlEncodedContent(alanlar) });

    public async Task<Sayfa> GonderAsync(HttpRequestMessage istek)
    {
        using (istek)
        using (var yanit = await _client.SendAsync(istek))
        {
            var adres = istek.RequestUri!;
            var konum = yanit.Headers.Location is { } l ? new Uri(adres, l) : null;
            var yonlendirme = (int)yanit.StatusCode is >= 300 and < 400 ? konum : null;
            return new Sayfa(yanit.StatusCode, adres, yonlendirme, await yanit.Content.ReadAsStringAsync());
        }
    }

    /// <summary>
    /// İlk isteği gönderir; yönlendirmeleri izler, kimlik sunucusunun giriş/onay/çıkış formlarını doldurur ve ilk
    /// "son" yanıtı (form olmayan sayfa veya durdurulan yönlendirme) döndürür.
    /// </summary>
    public async Task<Sayfa> IzleAsync(Func<Task<Sayfa>> ilk, AkisSecenekleri secenekler)
    {
        var sayfa = await ilk();
        var sifreGonderildi = false;
        var onayVerildi = false;
        for (var adim = 0; adim < EnFazlaAdim; adim++)
        {
            if (sayfa.Yonlendirme is { } hedef)
            {
                if (secenekler.Durdur?.Invoke(hedef) == true)
                {
                    return sayfa;
                }

                sayfa = await GetAsync(hedef);
                continue;
            }

            if (sayfa.Durum != HttpStatusCode.OK || !KimlikSunucusundaMi(sayfa.Adres))
            {
                return sayfa;
            }

            var yol = sayfa.Adres.AbsolutePath;
            if (Yol(yol, "/Account/Login") && sayfa.Govde.Contains("name=\"Input.Sifre\"", StringComparison.Ordinal))
            {
                if (secenekler.Sifre is null || secenekler.KullaniciAdi is null)
                {
                    throw new OlcumHatasi("sifre-soruldu");
                }

                if (sifreGonderildi)
                {
                    throw new OlcumHatasi("giris-basarisiz");
                }

                sifreGonderildi = true;
                sayfa = await PostAsync(sayfa.Adres, [
                    .. GizliAlanlar(sayfa.Govde),
                    KeyValuePair.Create("Input.KullaniciAdi", secenekler.KullaniciAdi),
                    KeyValuePair.Create("Input.Sifre", secenekler.Sifre),
                ]);
            }
            else if (Yol(yol, "/connect/authorize") && sayfa.Govde.Contains("name=\"karar\"", StringComparison.Ordinal))
            {
                if (onayVerildi)
                {
                    throw new OlcumHatasi("onay-dongusu");
                }

                onayVerildi = true;
                sayfa = await PostAsync(new Uri(_kimlik, "connect/authorize"),
                    [.. GizliAlanlar(sayfa.Govde), KeyValuePair.Create("karar", "kabul")]);
            }
            else if (Yol(yol, "/connect/logout") && sayfa.Govde.Contains("<form", StringComparison.Ordinal))
            {
                sayfa = await PostAsync(new Uri(_kimlik, "connect/logout"), GizliAlanlar(sayfa.Govde));
            }
            else
            {
                return sayfa;
            }
        }

        throw new OlcumHatasi("cok-fazla-yonlendirme");
    }

    public bool KimlikSunucusundaMi(Uri adres) =>
        Uri.Compare(adres, _kimlik, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;

    /// <summary>
    /// Sayfadaki ilk formun gizli alanları (antiforgery token'ı ve OIDC parametreleri dahil), öznitelik sırasından
    /// bağımsız. Tarayıcı yalnızca gönderilen formun alanlarını yollar; sayfada birden çok form olabilir (ör. demo
    /// uygulamaların üst menüsündeki çıkış formu).
    /// </summary>
    public static List<KeyValuePair<string, string>> GizliAlanlar(string html)
    {
        var form = FormEtiketi().Match(html) is { Success: true } eslesme ? eslesme.Value : html;
        var alanlar = new List<KeyValuePair<string, string>>();
        foreach (Match etiket in InputEtiketi().Matches(form))
        {
            var oznitelikler = Oznitelik().Matches(etiket.Value)
                .GroupBy(m => m.Groups[1].Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => WebUtility.HtmlDecode(g.First().Groups[2].Value), StringComparer.OrdinalIgnoreCase);
            if (oznitelikler.TryGetValue("type", out var tur) && tur.Equals("hidden", StringComparison.OrdinalIgnoreCase)
                && oznitelikler.TryGetValue("name", out var ad))
            {
                alanlar.Add(KeyValuePair.Create(ad, oznitelikler.GetValueOrDefault("value") ?? string.Empty));
            }
        }

        return alanlar;
    }

    public void Dispose() => _client.Dispose();

    private static bool Yol(string yol, string beklenen) =>
        string.Equals(yol.TrimEnd('/'), beklenen, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"<form\b.*?</form>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FormEtiketi();

    [GeneratedRegex(@"<input\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputEtiketi();

    [GeneratedRegex(@"([\w.:-]+)\s*=\s*""([^""]*)""")]
    private static partial Regex Oznitelik();
}
