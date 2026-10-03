using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace lihuowang2;

// 模组设置的数据载体：整个实例会被 RitsuLib 序列化成 settings.json。
// 用 class（而不是裸 bool）保存，以后加设置项不用换存储槽。
public sealed class Lihuowang2SettingsData
{
    // 是否播放模组特殊 BGM（卡牌 BGM / 房间 BGM / 胜利 BGM）。
    // 默认开启：属性初始化器给出的值就是「默认值」，settings.json 不存在或缺少字段时都取它。
    public bool PlaySpecialMusic { get; set; } = true;
}

// 模组设置：注册设置页 + 提供运行时读取入口。
//
// 存储作用域用 SaveScope.Global（跨存档共享，和游戏自带的音量/画面设置同性质）：
// 玩家改一次之后，下次启动游戏、换存档都复用同一个值。
// 落盘时机：开关一改动就立即 Save()，不需要退出游戏才生效。
public static class Lihuowang2ModSettings
{
    private const string DataKey = "settings";
    private const string FileName = "settings.json";
    private const string PageId = "settings";

    // settings.json 对应的缓存包装器。RitsuLib 在档案重载/数据重新加载后会让它自动失效，
    // 所以不要自己缓存 Get<T>() 的结果。
    private static ModDataStoreCache<Lihuowang2SettingsData>? _cache;

    /// <summary>
    /// 是否播放模组特殊 BGM。默认 true；Initialize() 之前也按 true 处理
    /// （宁可多放一次，也不要因为初始化时序把音乐吞掉）。
    /// </summary>
    public static bool PlaySpecialMusic
    {
        get
        {
            // ⚠ 这里**不能**用 cache.HasValue 做短路（踩过坑：玩家反馈"设置 BGM 失效"）：
            //   HasValue 的语义是"这个缓存包装器当前是否已经实例化过值"（RitsuLib 原话：
            //   "Gets whether this wrapper currently holds a cached instance."），首次访问时它必然
            //   是 false；而一旦在此处 return，就永远不会去读 cache.Value → 值永远不会被惰性加载，
            //   于是玩家存在 settings.json 里的「关闭」被当成「开启」：
            //   关掉后当场有效（写值时会顺带实例化），但**重启游戏后又开始放**（= 设置失效）。
            //   cache.Value 自己会按需从存储加载（ModDataStoreCache.Value → ModDataStore.Get<T>），
            //   所以直接读它即可，既读得到玩家的设置，又保持"没有值就是默认 true"的语义。
            ModDataStoreCache<Lihuowang2SettingsData>? cache = _cache;
            if (cache == null)
                return true;   // Initialize() 还没跑（极早期）：先按默认值「开启」

            return cache.Value.PlaySpecialMusic;
        }
    }

    /// <summary>由 Entry.Initialize() 调用一次：注册存储槽与设置页。</summary>
    public static void Initialize()
    {
        // 1. 注册持久化存储槽（settings.json）。
        //    autoCreateIfMissing：首次进游戏时用 defaultFactory 建一份，玩家马上就能改。
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
        {
            ModDataStore store = RitsuLibFramework.GetDataStore(Entry.ModId);
            store.Register(
                key: DataKey,
                fileName: FileName,
                scope: SaveScope.Global,
                defaultFactory: () => new Lihuowang2SettingsData(),
                autoCreateIfMissing: true);

            _cache = store.CreateCache<Lihuowang2SettingsData>(DataKey);
        }

        // 2. 注册设置页：模组设置中心 → 「李火旺模组」→ 音乐 → 播放特殊 BGM。
        //    绑定用 Callback 显式给出读/写/保存：写入时同步改内存里的设置对象并立刻落盘，
        //    这样开关状态会被记录，下次启动复用。
        RitsuLibFramework.RegisterModSettings(Entry.ModId, page =>
        {
            page.WithTitle(Text("LIHUOWANG2_SETTINGS_PAGE_TITLE", "李火旺模组"))
                .WithDescription(Text("LIHUOWANG2_SETTINGS_PAGE_DESC", "李火旺模组的设置。"))
                .AddSection("music", section =>
                {
                    section.WithTitle(Text("LIHUOWANG2_SETTINGS_SECTION_MUSIC", "音乐"))
                        .AddToggle(
                            "play_special_music",
                            Text("LIHUOWANG2_SETTINGS_MUSIC_PLAY_LABEL", "播放特殊 BGM"),
                            ModSettingsBindings.Callback<bool>(
                                Entry.ModId,
                                DataKey,
                                read: () => Current.PlaySpecialMusic,
                                write: value =>
                                {
                                    Current.PlaySpecialMusic = value;
                                    Save();
                                },
                                save: Save,
                                scope: SaveScope.Global),
                            Text("LIHUOWANG2_SETTINGS_MUSIC_PLAY_DESC",
                                "关闭后，打出会触发专属音乐的卡牌、以及进商店 / 通关等场景都不再播放模组特殊 BGM（游戏原声不受影响）。"));
                });
        }, PageId);
    }

    // 当前设置对象（缓存失效后由 RitsuLib 重新取，不要长期持有）
    private static Lihuowang2SettingsData Current =>
        (_cache ?? throw new InvalidOperationException(
            "Lihuowang2ModSettings.Initialize() 还没有被调用，无法读写模组设置。")).Value;

    // 保存到 settings.json
    private static void Save() => RitsuLibFramework.GetDataStore(Entry.ModId).Save(DataKey);

    // 设置界面文案：优先读模组本地化表 settings（localization/<lang>/settings.json），读不到用 fallback
    private static ModSettingsText Text(string key, string fallback)
        => ModSettingsText.LocString("settings", key, fallback);
}
