using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Selcuk.Identity.Data;

/// <summary>
/// Identity tabloları ve OpenIddict tabloları (uygulamalar, yetkilendirmeler, kapsamlar, tokenlar)
/// aynı veritabanında tutulur. OpenIddict varlıkları UseOpenIddict() ile eklenir.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<IstemciOturumu> IstemciOturumlari => Set<IstemciOturumu>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IstemciOturumu>(entity =>
        {
            entity.ToTable("IstemciOturumlari");
            entity.HasIndex(o => new { o.Sid, o.ClientId }).IsUnique();
            entity.HasIndex(o => o.KullaniciId);
        });
    }
}
