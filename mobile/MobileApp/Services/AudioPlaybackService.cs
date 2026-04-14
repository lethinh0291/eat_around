using Plugin.Maui.Audio;

namespace MobileApp.Services;

public class AudioPlaybackService
{
    private readonly IAudioManager _audioManager;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _playLock = new(1, 1);

    public AudioPlaybackService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public async Task<bool> TryPlayFromUrlAsync(string? audioUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(audioUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        await _playLock.WaitAsync(cancellationToken);
        try
        {
            await using var stream = await OpenAudioStreamAsync(uri, cancellationToken);
            if (stream is null)
            {
                return false;
            }

            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            using var player = _audioManager.CreatePlayer(memoryStream);
            if (player is null)
            {
                memoryStream.Dispose();
                return false;
            }

            var playedToEnd = await WaitForPlaybackEndAsync(player, cancellationToken);
            memoryStream.Dispose();
            return playedToEnd;
        }
        catch
        {
            return false;
        }
        finally
        {
            _playLock.Release();
        }
    }

    private async Task<Stream?> OpenAudioStreamAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.IsFile)
        {
            if (!File.Exists(uri.LocalPath))
            {
                return null;
            }

            return File.OpenRead(uri.LocalPath);
        }

        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var memoryStream = new MemoryStream();
        await responseStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static async Task<bool> WaitForPlaybackEndAsync(IAudioPlayer player, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnPlaybackEnded(object? sender, EventArgs args)
        {
            completion.TrySetResult(true);
        }

        player.PlaybackEnded += OnPlaybackEnded;

        try
        {
            player.Play();

            using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            return await completion.Task;
        }
        catch
        {
            return false;
        }
        finally
        {
            player.Stop();
            player.PlaybackEnded -= OnPlaybackEnded;
        }
    }
}
