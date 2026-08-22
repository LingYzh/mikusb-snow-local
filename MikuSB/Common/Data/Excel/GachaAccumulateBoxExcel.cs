namespace MikuSB.Data.Excel;

[ResourceEntity("gacha/accumulate_box.json")]
public class GachaAccumulateBoxExcel : ExcelResource
{
    public uint ProtectTag { get; set; }
    public uint Accumulate { get; set; }
    public List<uint> ExtraBox { get; set; } = [];
    public List<List<uint>> ExtraTokens { get; set; } = [];

    public override uint GetId() => ProtectTag;

    public override void Loaded()
    {
        if (ProtectTag != 0 && Accumulate != 0 && ExtraBox.Count >= 5)
            GameData.GachaAccumulateBoxData[ProtectTag] = this;
    }
}
