using Selcuk.Identity.Data;

namespace Selcuk.Identity.Denetim;

/// <summary>Güvenlik geçmişi sayfasında gösterilen Türkçe olay adları.</summary>
public static class DenetimOlaylari
{
    public static string Ad(DenetimOlayi olay, DenetimSonucu sonuc)
    {
        var basarili = sonuc == DenetimSonucu.Basarili;
        return olay switch
        {
            DenetimOlayi.GirisBasarili => "Giriş yapıldı",
            DenetimOlayi.GirisBasarisiz => "Başarısız giriş denemesi",
            DenetimOlayi.HesapKilitlendi => "Hesap geçici olarak kilitlendi",
            DenetimOlayi.IkiAdimBasarili => "İki aşamalı doğrulama başarılı",
            DenetimOlayi.IkiAdimBasarisiz => "Hatalı doğrulama kodu",
            DenetimOlayi.KurtarmaKoduKullanildi => "Kurtarma kodu kullanıldı",
            DenetimOlayi.IkiAdimKoduTekrarKullanildi => "2FA kodu tekrar kullanıldı",
            DenetimOlayi.IkiAdimAcildi => basarili
                ? "İki aşamalı doğrulama açıldı" : "İki aşamalı doğrulama açılamadı (hatalı kod)",
            DenetimOlayi.IkiAdimKapatildi => basarili
                ? "İki aşamalı doğrulama kapatıldı" : "İki aşamalı doğrulamayı kapatma reddedildi",
            DenetimOlayi.KurtarmaKodlariYenilendi => basarili
                ? "Kurtarma kodları yenilendi" : "Kurtarma kodlarını yenileme reddedildi",
            DenetimOlayi.Cikis => "Çıkış yapıldı",
            DenetimOlayi.SifreSifirlamaIstendi => "Şifre sıfırlama bağlantısı istendi",
            DenetimOlayi.SifreSifirlandi => basarili ? "Şifre sıfırlandı" : "Geçersiz şifre sıfırlama bağlantısı",
            DenetimOlayi.SifreDegisti => basarili ? "Şifre değiştirildi" : "Şifre değiştirme reddedildi",
            DenetimOlayi.OnayVerildi => "Uygulamaya erişim izni verildi",
            DenetimOlayi.OnayReddedildi => "Uygulamaya erişim izni reddedildi",
            DenetimOlayi.TokenVerildi => "Uygulamaya giriş yapıldı",
            DenetimOlayi.BackChannelLogoutGonderildi => "Uygulamaya çıkış bildirimi gönderildi",
            DenetimOlayi.BackChannelLogoutBasarisiz => "Uygulamaya çıkış bildirimi ulaştırılamadı",
            DenetimOlayi.HizSiniriAsildi => "Çok fazla istek (geçici engel)",
            _ => olay.ToString(),
        };
    }
}
