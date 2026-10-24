using System.Collections.Concurrent;
using System.Threading.Channels;
using Selcuk.Identity.Eposta;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Kimlik sunucusunun gönderdiği e-postaları yakalar; dosyaya veya ağa yazılmaz. Gönderim arka planda (kuyruk)
/// yapıldığı için testler <see cref="SiradakiAsync"/> ile bekler.
/// </summary>
public sealed class SahteEpostaGonderici : IEpostaGonderici
{
    private readonly Channel<EpostaMesaji> _gelenler = Channel.CreateUnbounded<EpostaMesaji>();

    public ConcurrentQueue<EpostaMesaji> Gonderilenler { get; } = new();

    public Task GonderAsync(EpostaMesaji mesaj, CancellationToken cancellationToken)
    {
        Gonderilenler.Enqueue(mesaj);
        _gelenler.Writer.TryWrite(mesaj);
        return Task.CompletedTask;
    }

    /// <summary>Gönderilen bir sonraki e-postayı bekler (en fazla 10 sn).</summary>
    public async Task<EpostaMesaji> SiradakiAsync()
    {
        using var zamanAsimi = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await _gelenler.Reader.ReadAsync(zamanAsimi.Token);
    }
}
