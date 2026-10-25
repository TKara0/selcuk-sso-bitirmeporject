using Microsoft.AspNetCore.Authentication.JwtBearer;
using Selcuk.Demo.Api.Data;

// Demo API: yalnızca kaynak sunucusu (kendi istemci kaydı yok). Kimlik sunucusunun verdiği access token'ı (JWT)
// doğrular: imza (JWKS), iss, aud = demo-api, typ = at+jwt, süre. Yetkilendirme: scope içinde notlar.oku ve rol ogrenci.
var builder = WebApplication.CreateBuilder(args);

var kimlik = builder.Configuration.GetSection("Kimlik");
const string NotlarOkuKapsami = "notlar.oku";
const string NotlarPolitikasi = "NotlarOku";

builder.Services.AddSingleton<INotDeposu, SahteNotDeposu>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Discovery ve JWKS kimlik sunucusundan alınır; imza anahtarı oradan doğrulanır.
        options.Authority = kimlik["Authority"];
        options.Audience = kimlik["Audience"] ?? "demo-api";
        options.MapInboundClaims = false;
        // 401 yanıtındaki WWW-Authenticate başlığına hata ayrıntısı (imza, süre vb.) yazılmaz.
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters.ValidAudience = options.Audience;
        // Yalnızca access token kabul edilir (id_token veya logout_token değil).
        options.TokenValidationParameters.ValidTypes = ["at+jwt"];
        options.TokenValidationParameters.NameClaimType = "sub";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy(NotlarPolitikasi, policy => policy
        .RequireAuthenticatedUser()
        // scope claim'i boşlukla ayrılmış metin veya ayrı değerler olarak gelebilir.
        .RequireAssertion(c => c.User.FindAll("scope")
            .SelectMany(s => s.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(NotlarOkuKapsami))
        .RequireRole("ogrenci")));

var app = builder.Build();

// Güvenlik başlıkları: API yanıtları önbelleğe alınmaz, tarayıcıda içerik olarak yorumlanmaz.
app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers.XFrameOptions = "DENY";
        headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        headers.CacheControl = "no-store";
        return Task.CompletedTask;
    });
    return next(context);
});

// Beklenmeyen hata: ayrıntı (yığın izi, mesaj) dönmez, yalnızca genel bir yanıt.
app.UseExceptionHandler(hata => hata.Run(context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    return context.Response.WriteAsJsonAsync(new { hata = "Sunucu hatası" });
}));

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Öğrenci kimliği yalnızca doğrulanmış token'dan (sub) alınır; parametreyle başka birinin notları istenemez.
app.MapGet("/api/notlar", (HttpContext context, INotDeposu depo) =>
{
    var sub = context.User.FindFirst("sub")?.Value;
    return sub is null
        ? Results.Unauthorized()
        : Results.Ok(new { ogrenci = sub, donem = "2026-2027 Güz", notlar = depo.Notlar(sub) });
}).RequireAuthorization(NotlarPolitikasi);

app.Run();

public partial class Program;
