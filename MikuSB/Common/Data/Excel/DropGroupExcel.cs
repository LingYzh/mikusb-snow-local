using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MikuSB.Data.Excel;

[ResourceEntity("drop/drop_grop.json")]
public class DropGroupExcel : ExcelResource
{
    [JsonProperty("ID")] public uint ID { get; set; }
    [JsonProperty("Grop")] public JToken? GropRaw { get; set; }

    public override uint GetId() => ID;

    public override void Loaded()
    {
        GameData.DropGroupData[ID] = this;
    }

    public IEnumerable<List<uint>> Roll(Random rng)
    {
        if (GropRaw is not JArray rows)
            yield break;

        foreach (var row in rows)
        {
            if (row is not JArray parts || parts.Count < 3)
                continue;

            var gdpl = JsonTokenLists.ReadUIntList(parts[0]);
            if (gdpl.Count < 4)
                continue;

            var chance = JsonTokenLists.ReadUInt(parts[1]);
            var count = Math.Max(1u, JsonTokenLists.ReadUInt(parts[2]));
            if (chance < 10000 && rng.Next(10000) >= chance)
                continue;

            var reward = gdpl.Take(4).ToList();
            reward.Add(count);
            yield return reward;
        }
    }
}

[ResourceEntity("drop/drop.json")]
public class DropExcel : ExcelResource
{
    [JsonProperty("ID")] public uint ID { get; set; }
    [JsonProperty("Drop")] public JToken? DropRaw { get; set; }

    public override uint GetId() => ID;

    public override void Loaded()
    {
        GameData.DropData[ID] = this;
    }

    public IEnumerable<uint> GroupIds()
    {
        foreach (var row in JsonTokenLists.ReadUIntTable(DropRaw))
        {
            if (row.Count > 0 && row[0] > 0)
                yield return row[0];
        }
    }
}
