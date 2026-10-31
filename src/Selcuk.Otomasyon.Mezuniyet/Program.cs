using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.Mezuniyet;

var builder = WebApplication.CreateBuilder(args);

// SSO, rol tabanlı menü ve yetki, veri kaynağı (VeriKaynagi:Mod), şart ayarları ve hata sayfaları: Selcuk.Otomasyon.Ortak.
builder.Services.AddSelcukOtomasyon(builder.Configuration, MezuniyetOtomasyonu.Ayarla);

var app = builder.Build();
app.UseSelcukOtomasyon();
app.Run();

public partial class Program;
