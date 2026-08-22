using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MikuSB.Data.Excel;

[ResourceEntity("shop/goods.json")]
public class ShopGoodsExcel : ExcelResource
{
    [JsonProperty("ShopId")] private JToken? ShopIdRaw { get; set; }
    [JsonProperty("GoodsId")] private JToken? GoodsIdRaw { get; set; }
    [JsonProperty("GDPLN")] private JToken? GdplnRaw { get; set; }
    [JsonProperty("Price1")] private JToken? Price1Raw { get; set; }
    [JsonProperty("Price2")] private JToken? Price2Raw { get; set; }
    [JsonProperty("LimitType")] private JToken? LimitTypeRaw { get; set; }
    [JsonProperty("LimitNum")] private JToken? LimitNumRaw { get; set; }
    [JsonProperty("Weight")] private JToken? WeightRaw { get; set; }

    [JsonIgnore] public uint ShopId => JsonTokenLists.ReadUInt(ShopIdRaw);
    [JsonIgnore] public uint GoodsId => JsonTokenLists.ReadUInt(GoodsIdRaw);
    [JsonIgnore] public List<uint> Gdpln => JsonTokenLists.ReadUIntList(GdplnRaw);
    [JsonIgnore] public List<uint> Price1 => JsonTokenLists.ReadUIntList(Price1Raw);
    [JsonIgnore] public List<uint> Price2 => JsonTokenLists.ReadUIntList(Price2Raw);
    [JsonIgnore] public uint LimitType => JsonTokenLists.ReadUInt(LimitTypeRaw);
    [JsonIgnore] public uint LimitNum => JsonTokenLists.ReadUInt(LimitNumRaw);
    [JsonIgnore] public uint Weight => JsonTokenLists.ReadUInt(WeightRaw);

    public override uint GetId() => GoodsId == 0 ? ShopId : GoodsId;

    public override void Loaded()
    {
        if (GoodsId == 0)
            return;

        GameData.ShopGoodsData[GoodsId] = this;
        if (!GameData.ShopGoodsByShop.TryGetValue(ShopId, out var list))
        {
            list = [];
            GameData.ShopGoodsByShop[ShopId] = list;
        }

        list.Add(this);
    }
}
