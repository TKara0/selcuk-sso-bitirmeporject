using Selcuk.Demo.Yemekhane.Data;
using Selcuk.Istemci.Ortak;

var builder = WebApplication.CreateBuilder(args);

// Cookie + OpenID Connect (code + PKCE), claim eşlemeleri ve Back-Channel Logout: Selcuk.Istemci.Ortak.
builder.Services.AddSelcukSSO(builder.Configuration);
builder.Services.AddSingleton<RezervasyonDeposu>();
builder.Services.AddRazorPages();

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
