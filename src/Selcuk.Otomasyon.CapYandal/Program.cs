using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.CapYandal;

var builder = WebApplication.CreateBuilder(args);

// SSO, rol tabanlı menü ve yetki, veri kaynağı (VeriKaynagi:Mod), şart ayarları ve hata sayfaları: Selcuk.Otomasyon.Ortak.
builder.Services.AddSelcukOtomasyon(builder.Configuration, CapYandalOtomasyonu.Ayarla);

var app = builder.Build();
app.UseSelcukOtomasyon();
app.Run();

public partial class Program;
