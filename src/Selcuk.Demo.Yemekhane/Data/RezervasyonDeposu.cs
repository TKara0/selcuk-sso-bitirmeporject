namespace Selcuk.Demo.Yemekhane.Data;

/// <summary>Bellek içi rezervasyon kaydı (uygulama yeniden başlarsa silinir). Anahtar: kullanıcının sub claim'i.</summary>
public sealed class RezervasyonDeposu
{
    private readonly Lock _kilit = new();
    private readonly Dictionary<string, SortedSet<DateOnly>> _kayitlar = [];

    public IReadOnlyList<DateOnly> Listele(string kullanici)
    {
        lock (_kilit)
        {
            return _kayitlar.TryGetValue(kullanici, out var gunler) ? [.. gunler] : [];
        }
    }

    public bool Ekle(string kullanici, DateOnly gun)
    {
        lock (_kilit)
        {
            if (!_kayitlar.TryGetValue(kullanici, out var gunler))
            {
                _kayitlar[kullanici] = gunler = [];
            }

            return gunler.Add(gun);
        }
    }

    public bool IptalEt(string kullanici, DateOnly gun)
    {
        lock (_kilit)
        {
            return _kayitlar.TryGetValue(kullanici, out var gunler) && gunler.Remove(gun);
        }
    }

    public int Sayi(DateOnly gun)
    {
        lock (_kilit)
        {
            return _kayitlar.Values.Count(g => g.Contains(gun));
        }
    }
}
