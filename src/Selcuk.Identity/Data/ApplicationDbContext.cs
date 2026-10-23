using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Selcuk.Identity.Data;

/// <summary>
/// Identity tabloları ve OpenIddict tabloları (uygulamalar, yetkilendirmeler, kapsamlar, tokenlar)
/// aynı veritabanında tutulur. OpenIddict varlıkları UseOpenIddict() ile eklenir.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options);
