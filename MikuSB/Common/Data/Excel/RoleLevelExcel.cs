using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MikuSB.Data.Excel;

[ResourceEntity("challenge/role/level.json")]
public class RoleLevelExcel : ExcelResource
{
    public uint ID { get; set; }
    [JsonProperty("FirstDropID")] private JToken? FirstDropIdRaw { get; set; }
    [JsonProperty("BaseDropID")] private JToken? BaseDropIdRaw { get; set; }
    [JsonProperty("RandomDropID")] private JToken? RandomDropIdRaw { get; set; }
    [JsonProperty("ShowAward")] private JToken? ShowAwardRaw { get; set; }
    [JsonProperty("ShowFirstAward")] private JToken? ShowFirstAwardRaw { get; set; }
    [JsonProperty("PlayerExp")] private JToken? PlayerExpRaw { get; set; }

    [JsonIgnore] public List<uint> FirstDropIds => JsonTokenLists.ReadUIntList(FirstDropIdRaw);
    [JsonIgnore] public List<uint> BaseDropIds => JsonTokenLists.ReadUIntList(BaseDropIdRaw);
    [JsonIgnore] public List<uint> RandomDropIds => JsonTokenLists.ReadUIntList(RandomDropIdRaw);
    [JsonIgnore] public List<List<uint>> ShowAward => JsonTokenLists.ReadUIntTable(ShowAwardRaw);
    [JsonIgnore] public List<List<uint>> ShowFirstAward => JsonTokenLists.ReadUIntTable(ShowFirstAwardRaw);
    [JsonIgnore] public int PlayerExp => (int)JsonTokenLists.ReadUInt(PlayerExpRaw);

    public override uint GetId() => ID;

    public override void Loaded()
    {
        GameData.RoleLevelData[ID] = this;
    }
}
