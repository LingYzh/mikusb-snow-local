using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MikuSB.Data.Excel;

[ResourceEntity("shop/shop_tab.json")]
public class ShopTabExcel : ExcelResource
{
    [JsonProperty("ShopId")] private JToken? ShopIdRaw { get; set; }
    [JsonProperty("OnOff")] private JToken? OnOffRaw { get; set; }
    [JsonProperty("Begin")] public string Begin { get; set; } = "";
    [JsonProperty("End")] public string End { get; set; } = "";
    [JsonProperty("RefreshRule")] private JToken? RefreshRuleRaw { get; set; }

    [JsonIgnore] public uint ShopId => JsonTokenLists.ReadUInt(ShopIdRaw);
    [JsonIgnore] public uint OnOff => JsonTokenLists.ReadUInt(OnOffRaw);
    [JsonIgnore] public uint RefreshRule => JsonTokenLists.ReadUInt(RefreshRuleRaw);

    public override uint GetId() => ShopId;

    public override void Loaded()
    {
        if (ShopId == 0)
            return;
        GameData.ShopTabData[ShopId] = this;
    }
}
