using lihuowang2.Characters;
using STS2RitsuLib.Audio;

namespace lihuowang2;

// BGM 播放工具：复刻塔1 mod 的 playTempBgmInstantly 语义（切歌→打完自动清理）。
public static class Lihuowang2MusicUtil
{
    /// <summary>卡片触发 BGM（战斗内作用域，战斗结束自动清理）。</summary>
    public static void PlayCardMusic(string fileName)
        => PlayMusic($"audio/music/{fileName}", AudioLifecycleScope.Combat);

    /// <summary>胜利场景 BGM（房间作用域）。</summary>
    public static void PlayVictoryMusic(string fileName = "chengxian.mp3")
        => PlayMusic($"audio/music/{fileName}", AudioLifecycleScope.Room);

    private static void PlayMusic(string relativePath, AudioLifecycleScope scope)
    {
        GameAudioService.Shared.PlayMusic(
            AudioSource.StreamingResourceMusic($"{Entry.ResPath}/{relativePath}"),
            new AudioPlaybackOptions
            {
                AutoPlay = true,
                Scope = scope
            });
    }
}
