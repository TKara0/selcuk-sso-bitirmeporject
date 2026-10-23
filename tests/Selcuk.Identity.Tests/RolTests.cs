using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Selcuk.Identity.Tests;

public sealed class RolTests : IDisposable
{
    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Uc_rol_olusturulmus()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var roller = await roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToListAsync();

        Assert.Equal(["akademisyen", "ogrenci", "personel"], roller);
    }
}
