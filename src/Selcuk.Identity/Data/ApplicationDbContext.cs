using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Selcuk.Identity.Data;

/// <summary>
/// Identity tabloları, OpenIddict tabloları (uygulamalar, yetkilendirmeler, kapsamlar, tokenlar), oturum–istemci
/// kayıtları ve denetim kaydı aynı veritabanında tutulur. OpenIddict varlıkları UseOpenIddict() ile eklenir.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<IstemciOturumu> IstemciOturumlari => Set<IstemciOturumu>();

    public DbSet<DenetimKaydi> DenetimKayitlari => Set<DenetimKaydi>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IstemciOturumu>(entity =>
        {
            entity.ToTable("IstemciOturumlari");
            entity.HasIndex(o => new { o.Sid, o.ClientId }).IsUnique();
            entity.HasIndex(o => o.KullaniciId);
        });

        builder.Entity<DenetimKaydi>(entity =>
        {
            entity.ToTable("DenetimKayitlari");
            entity.Property(k => k.OlayTuru).HasConversion<string>().HasMaxLength(50);
            entity.Property(k => k.Sonuc).HasConversion<string>().HasMaxLength(20);
            // Saklama süresi temizliği ve kullanıcının güvenlik geçmişi (son kayıtlar) için.
            entity.HasIndex(k => k.ZamanUtc);
            entity.HasIndex(k => new { k.KullaniciId, k.Id });
        });
    }
}
