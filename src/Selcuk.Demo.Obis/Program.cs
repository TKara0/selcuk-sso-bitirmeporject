using Selcuk.Demo.Obis.Data;
using Selcuk.Istemci.Ortak;

var builder = WebApplication.CreateBuilder(args);

// Cookie + OpenID Connect (code + PKCE), claim eşlemeleri ve Back-Channel Logout: Selcuk.Istemci.Ortak.
builder.Services.AddSelcukSSO(builder.Configuration);
builder.Services.AddRazorPages();
// Notlarım: notlar Demo API'den, oturumdaki access token ile (Authorization: Bearer) sunucu tarafında çekilir.
builder.Services.AddHttpClient(NotlarApiIstemcisi.Ad, client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DemoApi:Adres"] ?? "https://localhost:5201/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<NotlarApiIstemcisi>();

var app = builder.Build();

app.UseSelcukGuvenlikBasliklari();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Hata");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapSelcukBackChannelLogout();

app.Run();

public partial class Program;
