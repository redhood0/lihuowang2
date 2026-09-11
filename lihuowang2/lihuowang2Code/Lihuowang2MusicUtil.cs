using lihuowang2.Characters;
using STS2RitsuLib.Audio;

namespace lihuowang2;

// BGM 播放工具：复刻塔1 mod 的 playTempBgmInstantly 语义（切歌→打完自动清理）。
public static class Lihuowang2MusicUtil
{
    // 卡片 BGM 的默认音量倍率（1 = 原声）。想统一调小/调大卡片 BGM 只改这里。
    public const float DefaultCardMusicVolume = 0.5f;

    /// <summary>卡片触发 BGM（战斗内作用域，战斗结束自动清理）。</summary>
    /// <param name="fileName">audio/music 目录下的文件名。</param>
    /// <param name="volume">音量倍率：1 = 100%，0.5 = 50%。不传则用 DefaultCardMusicVolume。</param>
    public static void PlayCardMusic(string fileName, float volume = DefaultCardMusicVolume)
        => PlayMusic($"audio/music/{fileName}", AudioLifecycleScope.Combat, volume);

    /// <summary>胜利场景 BGM（房间作用域）。</summary>
    /// <param name="volume">音量倍率：1 = 100%，0.5 = 50%。</param>
    public static void PlayVictoryMusic(string fileName = "chengxian.mp3", float volume = DefaultCardMusicVolume)
        => PlayMusic($"audio/music/{fileName}", AudioLifecycleScope.Room, volume);

    private static void PlayMusic(string relativePath, AudioLifecycleScope scope, float volume)
    {
        GameAudioService.Shared.PlayMusic(
            AudioSource.StreamingResourceMusic($"{Entry.ResPath}/{relativePath}"),
            new AudioPlaybackOptions
            {
                AutoPlay = true,
                Scope = scope,
                Volume = volume
            });
    }
}
