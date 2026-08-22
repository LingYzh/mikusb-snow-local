using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MikuSB.Data.Excel;

[ResourceEntity("item/templates/suplies.json")]
public class SuppliesExcel : ExcelResource
{
    public uint Genre { get; set; }
    public uint Detail { get; set; }
    public uint Particular { get; set; }
    public uint Level { get; set; }
    [JsonProperty("Color")] public JToken? ColorRaw { get; set; }
    [JsonProperty("ProvideExp")] public JToken? ProvideExpRaw { get; set; }
    [JsonProperty("ConsumeGold")] public JToken? ConsumeGoldRaw { get; set; }
    [JsonProperty("GMnum")] public JToken? GMnumRaw { get; set; }

    [JsonIgnore] public int Color => ReadInt(ColorRaw);
    [JsonIgnore] public uint ProvideExp => ReadUInt(ProvideExpRaw);
    [JsonIgnore] public uint ConsumeGold => ReadUInt(ConsumeGoldRaw);
    [JsonIgnore] public uint GMnum => ReadUInt(GMnumRaw);

    public override uint GetId()
    {
        return (uint)GameResourceTemplateId.FromGdpl(Genre, Detail, Particular, Level);
    }

    public override void Loaded()
    {
        GameData.SuppliesData[GameResourceTemplateId.FromGdpl(Genre, Detail, Particular, Level)] = this;
        GameData.AllSuppliesData.Add(this);
    }

    private static int ReadInt(JToken? token)
    {
        if (token == null)
        {
            return 0;
        }

        return token.Type switch
        {
            JTokenType.Integer => token.Value<int>(),
            JTokenType.Float => (int)token.Value<decimal>(),
            JTokenType.String when int.TryParse(token.Value<string>(), out var value) => value,
            _ => 0
        };
    }

    private static uint ReadUInt(JToken? token)
    {
        var value = ReadInt(token);
        return value > 0 ? (uint)value : 0;
    }
}
