using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Rooms;
using lihuowang2.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace lihuowang2.Relics;

// 骗经：商人处的「职业卡牌」卖 0 金币；在商店里买过任何一样东西之后，
// 本遗物立刻破损（所以实际只够白拿一张职业卡），也无法再对下个商人使用。
[RegisterRelic(typeof(lihuowang2RelicPool))]
public class lihuowang2Relic_PianJing : ModRelicTemplate
{
    private bool _usedUp;

    public override RelicRarity Rarity => RelicRarity.Common;

    // 破损后图标变灰、钩子不再生效
    public override bool IsUsedUp => _usedUp;

    // 计数显示：还有效时显示 1（对应塔1原版遗物的 counter = 1），破损后隐藏
    public override bool ShowCounter => !_usedUp;
    public override int DisplayAmount => 1;

    // 不进商店：商店生成遗物时会按这个属性过滤
    // （MerchantRelicEntry.FillSlot 里的 r.IsAllowedInShops），默认 true。
    public override bool IsAllowedInShops => false;
    
    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/{GetType().Name}.png");

    // 进商店时放「坐忘道」BGM（对应塔1 PianJing.onEnterRoom 里那句
    // CardCrawlGame.music.playTempBgmInstantly("zuowangdao.mp3")）。
    // 房间作用域：离开商店时会随作用域一起清理，不会盖住后面的正常 BGM。
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is MerchantRoom && !_usedUp)
        {
            Flash();
            Lihuowang2MusicUtil.PlayRoomMusic("zuowangdao.mp3");
        }

        await base.AfterRoomEntered(room);
    }

    // 商店价格。MerchantEntry.Cost 每次取值都会经过 Hook.ModifyMerchantPrice，
    // 所以这里返回 0 就等于把售价改成 0（官方的会员卡就是这么打折的）。
    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost)
    {
        if (_usedUp || player != Owner || !LocalContext.IsMe(Owner))
            return cost;

        // 只对「职业卡牌」生效：商店里的卡牌条目里，无色牌的 Pool 是 ColorlessCardPool
        if (entry is not MerchantCardEntry cardEntry || cardEntry.CreationResult == null)
            return cost;

        if (cardEntry.CreationResult.Card.Pool is ColorlessCardPool)
            return cost;

        return 0m;
    }

    // 只要在商店里买过东西（包括那张 0 元的职业卡），本遗物就破损
    public override async Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent)
    {
        if (_usedUp || player != Owner)
            return;

        _usedUp = true;
        Status = RelicStatus.Disabled;
        Flash();
        InvokeDisplayAmountChanged();

        await base.AfterItemPurchased(player, itemPurchased, goldSpent);
    }
}
