using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Selcuk.Otomasyon.Ortak.Pages;

/// <summary>
/// Her ortamda tek hata sayfası (UseExceptionHandler ve durum kodu sayfaları). İstisnanın ayrıntısı ve stack trace
/// gösterilmez. Veri kaynağına ulaşılamazsa (bağlantı hatası, zaman aşımı, 5xx) 503 ve "Veri kaynağına şu an
/// ulaşılamıyor"; veri kaynağı 401/403 dönerse 403 ve yetki mesajı.
/// </summary>
public class HataModel : PageModel
{
    public const string UlasilamiyorBasligi = "Veri kaynağına şu an ulaşılamıyor";
    public const string YetkiBasligi = "Bu verilere erişim yetkin yok";

    public string Baslik { get; private set; } = "Bir hata oluştu";

    public string Aciklama { get; private set; } = "İsteğin işlenemedi. Birkaç dakika sonra tekrar dene.";

    public void OnGet(int? kod) => Hazirla(kod);

    public void OnPost(int? kod) => Hazirla(kod);

    private void Hazirla(int? kod)
    {
        if (HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error is VeriKaynagiHatasi hata)
        {
            if (hata.Tur == VeriKaynagiHatasi.Turu.Yetki)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                (Baslik, Aciklama) = (YetkiBasligi, "Veri kaynağı bu ekranın verilerini senin hesabınla paylaşmadı.");
            }
            else
            {
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                (Baslik, Aciklama) = (UlasilamiyorBasligi, "Bilgiler şu an alınamıyor. Biraz sonra tekrar dene.");
            }

            return;
        }

        (Baslik, Aciklama) = kod switch
        {
            404 => ("Sayfa bulunamadı", "Aradığın sayfa yok veya taşınmış."),
            400 => ("İstek geçersiz", "Form süresi dolmuş olabilir. Sayfayı yenileyip tekrar dene."),
            _ => (Baslik, Aciklama),
        };
    }
}
