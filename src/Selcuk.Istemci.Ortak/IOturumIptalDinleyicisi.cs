namespace Selcuk.Istemci.Ortak;

/// <summary>
/// Back-Channel Logout ile bir oturum (sid) veya kullanıcının bütün oturumları (sub) iptal edildikten sonra çağrılır.
/// İsteğe bağlıdır: uygulama, oturuma bağlı sunucu tarafı verisini (ör. oturum biletini) hemen silmek için kaydeder.
/// </summary>
public interface IOturumIptalDinleyicisi
{
    Task SidIptalEdildiAsync(string sid, CancellationToken cancellationToken);

    Task SubIptalEdildiAsync(string sub, CancellationToken cancellationToken);
}
