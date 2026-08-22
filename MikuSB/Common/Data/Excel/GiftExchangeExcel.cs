using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MikuSB.Data.Excel;

[ResourceEntity("house/gift_exchange.json")]
public class GiftExchangeExcel : ExcelResource
{
    [JsonProperty("ShopType")] private JToken? ShopTypeRaw { get; set; }
    [JsonProperty("GiftId")] private JToken? GiftIdRaw { get; set; }
    [JsonProperty("Gift")] private JToken? GiftRaw { get; set; }
    [JsonProperty("NeedItems")] private JToken? NeedItemsRaw { get; set; }
    [JsonProperty("NeedMoney")] private JToken? NeedMoneyRaw { get; set; }
    [JsonProperty("ShopBan")] private JToken? ShopBanRaw { get; set; }

    [JsonIgnore] public uint ShopType => JsonTokenLists.ReadUInt(ShopTypeRaw);
    [JsonIgnore] public uint GiftId => JsonTokenLists.ReadUInt(GiftIdRaw);
    [JsonIgnore] public List<uint> Gift => JsonTokenLists.ReadUIntList(GiftRaw);
    [JsonIgnore] public List<List<uint>> NeedItems => JsonTokenLists.ReadUIntTable(NeedItemsRaw);
    [JsonIgnore] public uint NeedMoney => JsonTokenLists.ReadUInt(NeedMoneyRaw);
    [JsonIgnore] public uint ShopBan => JsonTokenLists.ReadUInt(ShopBanRaw);

    public override uint GetId() => GiftId;

    public override void Loaded()
    {
        if (GiftId == 0)
            return;
        GameData.GiftExchangeData[GiftId] = this;
    }
}
