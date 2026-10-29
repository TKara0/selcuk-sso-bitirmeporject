using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Selcuk.Identity.Tests;

public sealed class RolTests : IDisposable
{
    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    /// <summary>Açılışta rol kataloğundaki (Selcuk.Roller) bütün roller oluşur; mevcut üç kullanıcı türü değişmeden.</summary>
    [Fact]
    public async Task Katalogdaki_butun_roller_olusturulmus()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var roller = await roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToListAsync();

        Assert.Equal(Roller.Tumu.Order(StringComparer.Ordinal), roller.Order(StringComparer.Ordinal));
        Assert.Equal(15, roller.Count);
        Assert.Contains("ogrenci", roller);
        Assert.Contains("akademisyen", roller);
        Assert.Contains("personel", roller);
    }
}
