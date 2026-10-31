using System.Net;
using System.Text.Json;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Data;

/// <summary>
/// Kimlik sunucusuna bağlanan istemci uygulamaların tanımları. Development'ta açılışta kaydedilir;
/// testler de aynı tanımları kullanır. Adresler https olmalıdır; tek istisna RFC 8252'deki yerel (native)
/// uygulamalar için http://127.0.0.1 loopback adresleridir (<see cref="AdresleriDogrula"/>).
/// </summary>
public static class Istemciler
{
    // İstemcinin özel ayarları (OpenIddict "Properties"). Adlar OIDC istemci meta verisi adlarıdır.
    public const string BackChannelLogoutUriOzelligi = "backchannel_logout_uri";
    public const string ClientUriOzelligi = "client_uri";
    public const string InitiateLoginUriOzelligi = "initiate_login_uri";
    public const string AciklamaOzelligi = "aciklama";
    // İstemci bazlı erişim izni (IstemciErisimi): ikisi de boşsa herkes, doluysa en az birine uyan kullanıcı girebilir.
    public const string IzinliRollerOzelligi = "izinliRoller";
    public const string IzinliKullaniciAdlariOzelligi = "izinliKullaniciAdlari";
    // true ise bu istemciye yalnızca iki aşamalı doğrulanmış (amr "mfa") oturumdan kod ve token verilir.
    public const string MfaZorunluOzelligi = "mfaZorunlu";
    // Uygulamalarım kartındaki simgenin vurgu rengi (site.css'te tanımlı bir ad, ör. "zeytin"); yoksa firuze.
    public const string VurguOzelligi = "vurgu";
    // Uygulamalarım kartındaki simgenin harfleri (en fazla iki harf, ör. "ST"); yoksa addan üretilir.
    public const string SimgeOzelligi = "simge";

    // Demo API (kaynak sunucusu): notlar.oku kapsamıyla verilen access token'ın aud'u demo-api olur.
    public const string DemoApiKaynagi = "demo-api";
    public const string NotlarOkuKapsami = "notlar.oku";

    public const string DemoObisClientId = "demo-obis";
    private const string DemoObisAdres = "https://localhost:5101";
    public const string DemoObisRedirectUri = DemoObisAdres + "/signin-oidc";
    public const string DemoObisPostLogoutRedirectUri = DemoObisAdres + "/signout-callback-oidc";
    public const string DemoObisBackChannelLogoutUri = DemoObisAdres + "/signout-backchannel";

    public const string DemoYemekhaneClientId = "demo-yemekhane";
    private const string DemoYemekhaneAdres = "https://localhost:5102";
    public const string DemoYemekhaneRedirectUri = DemoYemekhaneAdres + "/signin-oidc";
    public const string DemoYemekhanePostLogoutRedirectUri = DemoYemekhaneAdres + "/signout-callback-oidc";
    public const string DemoYemekhaneBackChannelLogoutUri = DemoYemekhaneAdres + "/signout-backchannel";

    // Bölüm otomasyonları (portlar 5301-5311, docs/mimari.md). Gelecekteki Staj API'si: staj.oku / staj.yaz
    // kapsamlarıyla verilen access token'ın aud'u staj-api olur.
    public const string StajApiKaynagi = "staj-api";
    public const string StajOkuKapsami = "staj.oku";
    public const string StajYazKapsami = "staj.yaz";

    public const string StajOtomasyonuClientId = "staj-otomasyonu";
    private const string StajOtomasyonuAdres = "https://localhost:5302";
    public const string StajOtomasyonuRedirectUri = StajOtomasyonuAdres + "/signin-oidc";
    public const string StajOtomasyonuPostLogoutRedirectUri = StajOtomasyonuAdres + "/signout-callback-oidc";
    public const string StajOtomasyonuBackChannelLogoutUri = StajOtomasyonuAdres + "/signout-backchannel";

    /// <summary>Staj Otomasyonu'na girebilen roller (en az biri).</summary>
    public static readonly string[] StajOtomasyonuIzinliRolleri =
        [Roller.Ogrenci, Roller.StajKomisyonu, Roller.BolumSekreteri, Roller.BolumBaskani];

    // İntibak, Mezuniyet ve ÇAP-Yandal otomasyonları: gelecekteki API'leri için <ad>.oku / <ad>.yaz kapsamları,
    // access token'ın aud'u <ad>-api.
    public const string IntibakApiKaynagi = "intibak-api";
    public const string IntibakOkuKapsami = "intibak.oku";
    public const string IntibakYazKapsami = "intibak.yaz";
    public const string IntibakOtomasyonuClientId = "intibak-otomasyonu";
    private const string IntibakOtomasyonuAdres = "https://localhost:5301";
    public const string IntibakOtomasyonuRedirectUri = IntibakOtomasyonuAdres + "/signin-oidc";

    /// <summary>İntibak Otomasyonu'na girebilen roller (en az biri).</summary>
    public static readonly string[] IntibakOtomasyonuIzinliRolleri =
        [Roller.Ogrenci, Roller.IntibakKomisyonu, Roller.BolumBaskani, Roller.BolumSekreteri, Roller.FakulteSekreteri];

    public const string MezuniyetApiKaynagi = "mezuniyet-api";
    public const string MezuniyetOkuKapsami = "mezuniyet.oku";
    public const string MezuniyetYazKapsami = "mezuniyet.yaz";
    public const string MezuniyetOtomasyonuClientId = "mezuniyet-otomasyonu";
    private const string MezuniyetOtomasyonuAdres = "https://localhost:5304";
    public const string MezuniyetOtomasyonuRedirectUri = MezuniyetOtomasyonuAdres + "/signin-oidc";

    /// <summary>Mezuniyet Otomasyonu'na girebilen roller (en az biri).</summary>
    public static readonly string[] MezuniyetOtomasyonuIzinliRolleri =
        [Roller.Ogrenci, Roller.MezuniyetKomisyonu, Roller.BolumSekreteri, Roller.FakulteSekreteri];

    public const string CapYandalApiKaynagi = "cap-yandal-api";
    public const string CapYandalOkuKapsami = "cap-yandal.oku";
    public const string CapYandalYazKapsami = "cap-yandal.yaz";
    public const string CapYandalOtomasyonuClientId = "cap-yandal-otomasyonu";
    private const string CapYandalOtomasyonuAdres = "https://localhost:5311";
    public const string CapYandalOtomasyonuRedirectUri = CapYandalOtomasyonuAdres + "/signin-oidc";

    /// <summary>ÇAP-Yandal Otomasyonu'na girebilen roller (en az biri).</summary>
    public static readonly string[] CapYandalOtomasyonuIzinliRolleri =
        [Roller.Ogrenci, Roller.CapYandalKoordinatoru, Roller.BolumBaskani, Roller.BolumSekreteri];

    public const string EtikKurulApiKaynagi = "etik-kurul-api";
    public const string EtikKurulOkuKapsami = "etik-kurul.oku";
    public const string EtikKurulYazKapsami = "etik-kurul.yaz";
    public const string EtikKurulOtomasyonuClientId = "etik-kurul-otomasyonu";
    private const string EtikKurulOtomasyonuAdres = "https://localhost:5303";
    public const string EtikKurulOtomasyonuRedirectUri = EtikKurulOtomasyonuAdres + "/signin-oidc";

    /// <summary>Etik Kurul Otomasyonu'na girebilen roller (en az biri).</summary>
    public static readonly string[] EtikKurulOtomasyonuIzinliRolleri = [Roller.Akademisyen, Roller.EtikKurulUyesi, Roller.BolumBaskani];

    public const string BolumKurulApiKaynagi = "bolum-kurul-api";
    public const string BolumKurulOkuKapsami = "bolum-kurul.oku";
    public const string BolumKurulYazKapsami = "bolum-kurul.yaz";
    public const string BolumKurulOtomasyonuClientId = "bolum-kurul-otomasyonu";
    private const string BolumKurulOtomasyonuAdres = "https://localhost:5305";
    public const string BolumKurulOtomasyonuRedirectUri = BolumKurulOtomasyonuAdres + "/signin-oidc";

    /// <summary>Bölüm Kurul Karar Otomasyonu'na girebilen roller (en az biri).</summary>
    public static readonly string[] BolumKurulOtomasyonuIzinliRolleri =
        [Roller.BolumBaskani, Roller.BolumKuruluUyesi, Roller.BolumSekreteri, Roller.FakulteSekreteri];

    public const string LisansProjeleriApiKaynagi = "lisans-projeleri-api";
    public const string LisansProjeleriOkuKapsami = "lisans-projeleri.oku";
    public const string LisansProjeleriYazKapsami = "lisans-projeleri.yaz";
    public const string LisansProjeleriOtomasyonuClientId = "lisans-projeleri-otomasyonu";
    private const string LisansProjeleriOtomasyonuAdres = "https://localhost:5309";
    public const string LisansProjeleriOtomasyonuRedirectUri = LisansProjeleriOtomasyonuAdres + "/signin-oidc";

    /// <summary>Lisans Projeleri Otomasyonu'na girebilen roller (en az biri).</summary>
    public static readonly string[] LisansProjeleriOtomasyonuIzinliRolleri = [Roller.Ogrenci, Roller.Akademisyen, Roller.BolumBaskani];

    public const string DanismanlikApiKaynagi = "danismanlik-api";
    public const string DanismanlikOkuKapsami = "danismanlik.oku";
    public const string DanismanlikYazKapsami = "danismanlik.yaz";
    public const string DanismanlikOtomasyonuClientId = "danismanlik-otomasyonu";
    private const string DanismanlikOtomasyonuAdres = "https://localhost:5306";
    public const string DanismanlikOtomasyonuRedirectUri = DanismanlikOtomasyonuAdres + "/signin-oidc";

    /// <summary>Öğrenci Danışmanlık Otomasyonu'na girebilen roller (en az biri).</summary>
    public static readonly string[] DanismanlikOtomasyonuIzinliRolleri = [Roller.Danisman, Roller.Ogrenci, Roller.BolumBaskani];

    public const string IskurApiKaynagi = "iskur-api";
    public const string IskurOkuKapsami = "iskur.oku";
    public const string IskurYazKapsami = "iskur.yaz";
    public const string IskurOtomasyonuClientId = "iskur-otomasyonu";
    private const string IskurOtomasyonuAdres = "https://localhost:5307";
    public const string IskurOtomasyonuRedirectUri = IskurOtomasyonuAdres + "/signin-oidc";

    /// <summary>İŞKUR Otomasyonu'na girebilen roller (en az biri).</summary>
    public static readonly string[] IskurOtomasyonuIzinliRolleri = [Roller.IskurSorumlusu, Roller.Ogrenci];

    public const string MudekApiKaynagi = "mudek-api";
    public const string MudekOkuKapsami = "mudek.oku";
    public const string MudekYazKapsami = "mudek.yaz";
    public const string MudekOtomasyonuClientId = "mudek-otomasyonu";
    private const string MudekOtomasyonuAdres = "https://localhost:5308";
    public const string MudekOtomasyonuRedirectUri = MudekOtomasyonuAdres + "/signin-oidc";

    /// <summary>MÜDEK Otomasyonu'na girebilen roller (en az biri).</summary>
    public static readonly string[] MudekOtomasyonuIzinliRolleri = [Roller.MudekKomisyonu, Roller.Akademisyen, Roller.BolumBaskani, Roller.BolumSekreteri];

    public const string SinavAnaliziApiKaynagi = "sinav-analizi-api";
    public const string SinavAnaliziOkuKapsami = "sinav-analizi.oku";
    public const string SinavAnaliziYazKapsami = "sinav-analizi.yaz";
    public const string SinavAnaliziOtomasyonuClientId = "sinav-analizi-otomasyonu";
    private const string SinavAnaliziOtomasyonuAdres = "https://localhost:5310";
    public const string SinavAnaliziOtomasyonuRedirectUri = SinavAnaliziOtomasyonuAdres + "/signin-oidc";

    /// <summary>Sınav Başarısı Analizi Otomasyonu'na girebilen roller (en az biri).</summary>
    public static readonly string[] SinavAnaliziOtomasyonuIzinliRolleri = [Roller.Akademisyen, Roller.BolumBaskani, Roller.MudekKomisyonu];

    public const string TekKapiClientId = "tek-kapi";
    private const string TekKapiAdres = "http://127.0.0.1:4545";
    public const string TekKapiRedirectUri = TekKapiAdres + "/auth/callback";
    public const string TekKapiPostLogoutRedirectUri = TekKapiAdres + "/";
    public const string TekKapiBackChannelLogoutUri = TekKapiAdres + "/auth/backchannel-logout";
    public const string TekKapiInitiateLoginUri = TekKapiAdres + "/auth/login";
    public const string TekKapiClientUri = TekKapiAdres + "/";
    public const string TekKapiAyarlari = "Istemciler:TekKapi";

    /// <summary>Yapılandırmada liste yoksa veya boşsa Tek Kapı'ya yalnızca bu kullanıcı girebilir (herkese açılmaz).</summary>
    public static readonly string[] TekKapiVarsayilanIzinliKullaniciAdlari = ["203011001"];

    public static OpenIddictApplicationDescriptor DemoObis(string clientSecret)
    {
        var tanim = Tanim(DemoObisClientId, "Demo OBİS", DemoObisAdres,
            "Notlarını görüntüle; akademisyenler için ders yönetimi.", clientSecret);
        // Notlarım sayfası notları Demo API'den (notlar.oku) çeker.
        tanim.Permissions.Add(Permissions.Prefixes.Scope + NotlarOkuKapsami);
        return tanim;
    }

    /// <summary>Demo API'nin kapsamı: kimlik sunucusunda açılışta oluşturulur veya güncellenir (tüm ortamlarda).</summary>
    public static OpenIddictScopeDescriptor NotlarOku() => new()
    {
        Name = NotlarOkuKapsami,
        DisplayName = "Not bilgilerini okuma",
        Resources = { DemoApiKaynagi },
    };

    /// <summary>
    /// Staj Otomasyonu (temsili ekranlar, örnek veri): gizli web istemcisi; yalnızca izinli rollere açık, gelecekteki
    /// Staj API'si için staj.oku ve staj.yaz kapsamları.
    /// </summary>
    public static OpenIddictApplicationDescriptor StajOtomasyonu(string clientSecret)
    {
        var tanim = Tanim(StajOtomasyonuClientId, "Staj Otomasyonu", StajOtomasyonuAdres,
            "Staj başvurusu, staj defteri, komisyon değerlendirmesi ve evrak takibi (temsili, örnek veri).", clientSecret);
        tanim.Permissions.Add(Permissions.Prefixes.Scope + StajOkuKapsami);
        tanim.Permissions.Add(Permissions.Prefixes.Scope + StajYazKapsami);
        tanim.Properties[IzinliRollerOzelligi] = JsonSerializer.SerializeToElement(StajOtomasyonuIzinliRolleri);
        tanim.Properties[VurguOzelligi] = JsonSerializer.SerializeToElement("zeytin");
        tanim.Properties[SimgeOzelligi] = JsonSerializer.SerializeToElement("ST");
        return tanim;
    }

    /// <summary>Staj API'sinin kapsamları: kimlik sunucusunda açılışta oluşturulur veya güncellenir (tüm ortamlarda).</summary>
    public static IReadOnlyList<OpenIddictScopeDescriptor> StajKapsamlari() =>
    [
        new() { Name = StajOkuKapsami, DisplayName = "Staj bilgilerini okuma", Resources = { StajApiKaynagi } },
        new() { Name = StajYazKapsami, DisplayName = "Staj başvurusu ve değerlendirmesi yazma", Resources = { StajApiKaynagi } },
    ];

    /// <summary>İntibak Otomasyonu (temsili ekranlar, örnek veri): gizli web istemcisi, intibak.oku/intibak.yaz.</summary>
    public static OpenIddictApplicationDescriptor IntibakOtomasyonu(string clientSecret) => OtomasyonTanimi(
        IntibakOtomasyonuClientId, "İntibak Otomasyonu", IntibakOtomasyonuAdres,
        "Geçiş öğrencilerinin intibak başvurusu, ders eşleştirme, komisyon kararı ve yönetim kurulu evrakı (temsili, örnek veri).",
        clientSecret, IntibakOkuKapsami, IntibakYazKapsami, IntibakOtomasyonuIzinliRolleri, "eflatun", "İN");

    /// <summary>Mezuniyet Otomasyonu (temsili ekranlar, örnek veri): gizli web istemcisi, mezuniyet.oku/mezuniyet.yaz.</summary>
    public static OpenIddictApplicationDescriptor MezuniyetOtomasyonu(string clientSecret) => OtomasyonTanimi(
        MezuniyetOtomasyonuClientId, "Mezuniyet Otomasyonu", MezuniyetOtomasyonuAdres,
        "Mezuniyet durumu ve başvurusu, ilişik kesme, komisyon kontrolü ve diploma hazırlığı (temsili, örnek veri).",
        clientSecret, MezuniyetOkuKapsami, MezuniyetYazKapsami, MezuniyetOtomasyonuIzinliRolleri, "bakir", "ME");

    /// <summary>ÇAP-Yandal Otomasyonu (temsili ekranlar, örnek veri): gizli web istemcisi, cap-yandal.oku/cap-yandal.yaz.</summary>
    public static OpenIddictApplicationDescriptor CapYandalOtomasyonu(string clientSecret) => OtomasyonTanimi(
        CapYandalOtomasyonuClientId, "ÇAP-Yandal Otomasyonu", CapYandalOtomasyonuAdres,
        "Çift anadal ve yandal başvurusu, koordinatör sıralaması, bölüm onayı ve programdaki ilerleme (temsili, örnek veri).",
        clientSecret, CapYandalOkuKapsami, CapYandalYazKapsami, CapYandalOtomasyonuIzinliRolleri, "hardal", "ÇY");

    /// <summary>Etik Kurul Otomasyonu (temsili ekranlar, örnek veri): gizli web istemcisi, etik-kurul.oku/etik-kurul.yaz.</summary>
    public static OpenIddictApplicationDescriptor EtikKurulOtomasyonu(string clientSecret) => OtomasyonTanimi(
        EtikKurulOtomasyonuClientId, "Etik Kurul Otomasyonu", EtikKurulOtomasyonuAdres,
        "Araştırmalar için etik kurul başvurusu, kurul üyelerinin görüşleri ve kurul kararları (temsili, örnek veri).",
        clientSecret, EtikKurulOkuKapsami, EtikKurulYazKapsami, EtikKurulOtomasyonuIzinliRolleri, "murdum", "EK");

    /// <summary>Bölüm Kurul Karar Otomasyonu (temsili ekranlar, örnek veri): gizli web istemcisi, bolum-kurul.oku/bolum-kurul.yaz.</summary>
    public static OpenIddictApplicationDescriptor BolumKurulOtomasyonu(string clientSecret) => OtomasyonTanimi(
        BolumKurulOtomasyonuClientId, "Bölüm Kurul Karar Otomasyonu", BolumKurulOtomasyonuAdres,
        "Bölüm kurulu toplantıları, gündem, oylama, karar ve tutanak arşivi (temsili, örnek veri).",
        clientSecret, BolumKurulOkuKapsami, BolumKurulYazKapsami, BolumKurulOtomasyonuIzinliRolleri, "arduvaz", "BK");

    /// <summary>Lisans Projeleri Otomasyonu (temsili ekranlar, örnek veri): gizli web istemcisi, lisans-projeleri.oku/.yaz.</summary>
    public static OpenIddictApplicationDescriptor LisansProjeleriOtomasyonu(string clientSecret) => OtomasyonTanimi(
        LisansProjeleriOtomasyonuClientId, "Lisans Projeleri Otomasyonu", LisansProjeleriOtomasyonuAdres,
        "Bitirme projelerinde konu önerisi ve seçimi, öğrenci-hoca eşleştirmesi, aşamalar ve değerlendirme (temsili, örnek veri).",
        clientSecret, LisansProjeleriOkuKapsami, LisansProjeleriYazKapsami, LisansProjeleriOtomasyonuIzinliRolleri, "cam", "LP");

    /// <summary>Öğrenci Danışmanlık Otomasyonu (temsili ekranlar, örnek veri): gizli web istemcisi, danismanlik.oku/danismanlik.yaz.</summary>
    public static OpenIddictApplicationDescriptor DanismanlikOtomasyonu(string clientSecret) => OtomasyonTanimi(
        DanismanlikOtomasyonuClientId, "Öğrenci Danışmanlık Otomasyonu", DanismanlikOtomasyonuAdres,
        "Danışman-öğrenci eşleşmeleri, görüşme talepleri ve görüşme notları (temsili, örnek veri).",
        clientSecret, DanismanlikOkuKapsami, DanismanlikYazKapsami, DanismanlikOtomasyonuIzinliRolleri, "indigo", "DA");

    /// <summary>İŞKUR Otomasyonu (temsili ekranlar, örnek veri): gizli web istemcisi, iskur.oku/iskur.yaz.</summary>
    public static OpenIddictApplicationDescriptor IskurOtomasyonu(string clientSecret) => OtomasyonTanimi(
        IskurOtomasyonuClientId, "İŞKUR Otomasyonu", IskurOtomasyonuAdres,
        "İŞKUR gençlik programında devam girişi ve aylık takip (temsili, örnek veri).",
        clientSecret, IskurOkuKapsami, IskurYazKapsami, IskurOtomasyonuIzinliRolleri, "petrol", "İŞ");

    /// <summary>MÜDEK Otomasyonu (temsili ekranlar, örnek veri): gizli web istemcisi, mudek.oku/mudek.yaz.</summary>
    public static OpenIddictApplicationDescriptor MudekOtomasyonu(string clientSecret) => OtomasyonTanimi(
        MudekOtomasyonuClientId, "MÜDEK Otomasyonu", MudekOtomasyonuAdres,
        "Program ve ders çıktılarının izlenmesi, eksikler ve dönem raporları (temsili, örnek veri).",
        clientSecret, MudekOkuKapsami, MudekYazKapsami, MudekOtomasyonuIzinliRolleri, "gulkurusu", "MÜ");

    /// <summary>Sınav Başarısı Analizi Otomasyonu (temsili ekranlar, örnek veri): gizli web istemcisi, sinav-analizi.oku/sinav-analizi.yaz.</summary>
    public static OpenIddictApplicationDescriptor SinavAnaliziOtomasyonu(string clientSecret) => OtomasyonTanimi(
        SinavAnaliziOtomasyonuClientId, "Sınav Başarısı Analizi Otomasyonu", SinavAnaliziOtomasyonuAdres,
        "Derslerin sınav başarısının toplu analizi: not dağılımı, geçme oranı ve hedefler (temsili, örnek veri).",
        clientSecret, SinavAnaliziOkuKapsami, SinavAnaliziYazKapsami, SinavAnaliziOtomasyonuIzinliRolleri, "grafit", "SA");

    /// <summary>Bölüm otomasyonlarının (Staj hariç) API kapsamları (tüm ortamlarda açılışta oluşturulur).</summary>
    public static IReadOnlyList<OpenIddictScopeDescriptor> OtomasyonKapsamlari() =>
    [
        new() { Name = IntibakOkuKapsami, DisplayName = "İntibak bilgilerini okuma", Resources = { IntibakApiKaynagi } },
        new() { Name = IntibakYazKapsami, DisplayName = "İntibak başvurusu ve kararı yazma", Resources = { IntibakApiKaynagi } },
        new() { Name = MezuniyetOkuKapsami, DisplayName = "Mezuniyet bilgilerini okuma", Resources = { MezuniyetApiKaynagi } },
        new() { Name = MezuniyetYazKapsami, DisplayName = "Mezuniyet başvurusu ve onayı yazma", Resources = { MezuniyetApiKaynagi } },
        new() { Name = CapYandalOkuKapsami, DisplayName = "ÇAP ve yandal bilgilerini okuma", Resources = { CapYandalApiKaynagi } },
        new() { Name = CapYandalYazKapsami, DisplayName = "ÇAP ve yandal başvurusu ve onayı yazma", Resources = { CapYandalApiKaynagi } },
        new() { Name = EtikKurulOkuKapsami, DisplayName = "Etik kurul bilgilerini okuma", Resources = { EtikKurulApiKaynagi } },
        new() { Name = EtikKurulYazKapsami, DisplayName = "Etik başvurusu ve görüş yazma", Resources = { EtikKurulApiKaynagi } },
        new() { Name = BolumKurulOkuKapsami, DisplayName = "Bölüm kurulu bilgilerini okuma", Resources = { BolumKurulApiKaynagi } },
        new() { Name = BolumKurulYazKapsami, DisplayName = "Toplantı, gündem ve oy yazma", Resources = { BolumKurulApiKaynagi } },
        new() { Name = LisansProjeleriOkuKapsami, DisplayName = "Lisans projesi bilgilerini okuma", Resources = { LisansProjeleriApiKaynagi } },
        new() { Name = LisansProjeleriYazKapsami, DisplayName = "Konu, tercih ve değerlendirme yazma", Resources = { LisansProjeleriApiKaynagi } },
        new() { Name = DanismanlikOkuKapsami, DisplayName = "Danışmanlık bilgilerini okuma", Resources = { DanismanlikApiKaynagi } },
        new() { Name = DanismanlikYazKapsami, DisplayName = "Görüşme talebi ve notu yazma", Resources = { DanismanlikApiKaynagi } },
        new() { Name = IskurOkuKapsami, DisplayName = "İŞKUR devam bilgilerini okuma", Resources = { IskurApiKaynagi } },
        new() { Name = IskurYazKapsami, DisplayName = "Devam girişi yazma", Resources = { IskurApiKaynagi } },
        new() { Name = MudekOkuKapsami, DisplayName = "MÜDEK bilgilerini okuma", Resources = { MudekApiKaynagi } },
        new() { Name = MudekYazKapsami, DisplayName = "Ders çıktısı ölçümü yazma", Resources = { MudekApiKaynagi } },
        new() { Name = SinavAnaliziOkuKapsami, DisplayName = "Sınav sonuçlarını okuma", Resources = { SinavAnaliziApiKaynagi } },
        new() { Name = SinavAnaliziYazKapsami, DisplayName = "Sınav analizi yazma", Resources = { SinavAnaliziApiKaynagi } },
    ];

    /// <summary>Bölüm otomasyonu: gizli web istemcisi, oku/yaz kapsamları, izinli roller, vurgu rengi ve simge.</summary>
    private static OpenIddictApplicationDescriptor OtomasyonTanimi(string clientId, string ad, string adres, string aciklama,
        string clientSecret, string okuKapsami, string yazKapsami, string[] izinliRoller, string vurgu, string simge)
    {
        var tanim = Tanim(clientId, ad, adres, aciklama, clientSecret);
        tanim.Permissions.Add(Permissions.Prefixes.Scope + okuKapsami);
        tanim.Permissions.Add(Permissions.Prefixes.Scope + yazKapsami);
        tanim.Properties[IzinliRollerOzelligi] = JsonSerializer.SerializeToElement(izinliRoller);
        tanim.Properties[VurguOzelligi] = JsonSerializer.SerializeToElement(vurgu);
        tanim.Properties[SimgeOzelligi] = JsonSerializer.SerializeToElement(simge);
        return tanim;
    }

    public static OpenIddictApplicationDescriptor DemoYemekhane(string clientSecret) => Tanim(
        DemoYemekhaneClientId, "Demo Yemekhane", DemoYemekhaneAdres,
        "Haftalık menü ve yemek rezervasyonu; personel için günlük sayılar.", clientSecret);

    /// <summary>
    /// Tek Kapı: kullanıcının kendi bilgisayarında çalışan yerel köprü uygulaması (Node.js). Public istemci (RFC 8252
    /// native app): client_secret yok, çünkü yerel uygulamada gizli tutulamaz; bunun yerine PKCE zorunlu ve adresler
    /// yalnızca 127.0.0.1 loopback. Explicit onay; yalnızca izinliKullaniciAdlari listesindeki kullanıcılar girebilir.
    /// Gerçek sistem şifrelerinin kilidini açtığı için iki aşamalı doğrulama zorunludur (mfaZorunlu).
    /// </summary>
    public static OpenIddictApplicationDescriptor TekKapi(IEnumerable<string> izinliKullaniciAdlari)
    {
        var izinliler = izinliKullaniciAdlari.Where(ad => !string.IsNullOrWhiteSpace(ad)).Select(ad => ad.Trim()).ToArray();
        return new OpenIddictApplicationDescriptor
        {
            ClientId = TekKapiClientId,
            ClientType = ClientTypes.Public,
            ApplicationType = ApplicationTypes.Native,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = "Tek Kapı — gerçek Selçuk sistemleri (yerel)",
            RedirectUris = { new Uri(TekKapiRedirectUri) },
            PostLogoutRedirectUris = { new Uri(TekKapiPostLogoutRedirectUri) },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
            Properties =
            {
                [BackChannelLogoutUriOzelligi] = JsonSerializer.SerializeToElement(TekKapiBackChannelLogoutUri),
                [ClientUriOzelligi] = JsonSerializer.SerializeToElement(TekKapiClientUri),
                [InitiateLoginUriOzelligi] = JsonSerializer.SerializeToElement(TekKapiInitiateLoginUri),
                [AciklamaOzelligi] = JsonSerializer.SerializeToElement(
                    "Kendi üniversite hesabınla gerçek OBİS, UZEM ve e-postaya bu bilgisayardan otomatik giriş. " +
                    "Üniversite şifren yalnızca bu bilgisayarda, şifreli saklanır."),
                [IzinliKullaniciAdlariOzelligi] = JsonSerializer.SerializeToElement(
                    izinliler.Length > 0 ? izinliler : TekKapiVarsayilanIzinliKullaniciAdlari),
                [MfaZorunluOzelligi] = JsonSerializer.SerializeToElement(true),
            },
        };
    }

    /// <summary>
    /// Tanımdaki tüm adresler (redirect, post-logout, back-channel logout, client_uri, initiate_login_uri) https
    /// olmalı; http yalnızca 127.0.0.1 loopback adresinde kabul edilir (RFC 8252 bölüm 7.3, 8.3). "localhost" adı
    /// da kabul edilmez: ad çözümlemesi değiştirilerek başka bir arayüze yönlendirilebilir.
    /// </summary>
    public static void AdresleriDogrula(OpenIddictApplicationDescriptor tanim)
    {
        var adresler = tanim.RedirectUris.Concat(tanim.PostLogoutRedirectUris)
            .Concat(new[] { BackChannelLogoutUriOzelligi, ClientUriOzelligi, InitiateLoginUriOzelligi }
                .Where(tanim.Properties.ContainsKey)
                .Select(ad => new Uri(tanim.Properties[ad].GetString()!)));
        foreach (var adres in adresler)
        {
            if (adres.Scheme != Uri.UriSchemeHttps && !HttpLoopbackMi(adres))
            {
                throw new InvalidOperationException(
                    $"'{tanim.ClientId}' istemcisinde '{adres}' kabul edilmez: adres https veya http://127.0.0.1 olmalı.");
            }
        }
    }

    /// <summary>http şemalı ve sunucusu 127.0.0.1 IP adresi olan adres (RFC 8252 loopback).</summary>
    public static bool HttpLoopbackMi(Uri adres) =>
        adres.Scheme == Uri.UriSchemeHttp
        && IPAddress.TryParse(adres.Host, out var ip)
        && ip.Equals(IPAddress.Loopback);

    /// <summary>
    /// Gizli web istemcisi: authorization_code + PKCE zorunlu, explicit onay; redirect, post-logout ve
    /// back-channel logout adresleri uygulamanın kök adresinden türetilir. offline_access ile refresh token alabilir
    /// (kayan, tek kullanımlık) ve çıkışta refresh token'ını revocation uç noktasıyla iptal edebilir.
    /// </summary>
    private static OpenIddictApplicationDescriptor Tanim(
        string clientId, string gorunenAd, string adres, string aciklama, string clientSecret) => new()
    {
        ClientId = clientId,
        ClientSecret = clientSecret,
        ClientType = ClientTypes.Confidential,
        ApplicationType = ApplicationTypes.Web,
        ConsentType = ConsentTypes.Explicit,
        DisplayName = gorunenAd,
        RedirectUris = { new Uri(adres + "/signin-oidc") },
        PostLogoutRedirectUris = { new Uri(adres + "/signout-callback-oidc") },
        Permissions =
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.EndSession,
            Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Scopes.Profile,
            Permissions.Scopes.Email,
            Permissions.Scopes.Roles,
            Permissions.Prefixes.Scope + Scopes.OfflineAccess,
        },
        Requirements =
        {
            Requirements.Features.ProofKeyForCodeExchange,
        },
        Properties =
        {
            [BackChannelLogoutUriOzelligi] = JsonSerializer.SerializeToElement(adres + "/signout-backchannel"),
            [ClientUriOzelligi] = JsonSerializer.SerializeToElement(adres + "/"),
            [InitiateLoginUriOzelligi] = JsonSerializer.SerializeToElement(adres + "/Giris"),
            [AciklamaOzelligi] = JsonSerializer.SerializeToElement(aciklama),
            [MfaZorunluOzelligi] = JsonSerializer.SerializeToElement(false),
        },
    };
}
