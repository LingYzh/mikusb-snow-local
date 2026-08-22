using MikuSB.Util.Extensions;
using SqlSugar;
using MikuSB.Proto;

namespace MikuSB.Database.Player;

[SugarTable("Player")]
public class PlayerGameData : BaseDatabaseDataHelper
{
    public const string DefaultDisplayName = "Miku";

    public string? Name { get; set; } = "";
    public string? Signature { get; set; } = "MikuPS";
    public uint Level { get; set; } = 100;
    public int Exp { get; set; } = 0;
    public long RegisterTime { get; set; } = Extensions.GetUnixSec();
    public long LastActiveTime { get; set; }
    public Sex Gender { get; set; } = Sex.Female;
    public uint Vigor {  get; set; } = 240;
    [SugarColumn(IsJson = true)] public List<PlayerAttr> Attrs { get; set; } = [];
    [SugarColumn(IsJson = true)] public List<PlayerStrAttr> StrAttrs { get; set; } = [];
    [SugarColumn(IsJson = true)] public List<ulong> ShowItems { get; set; } = [];

    public static PlayerGameData? GetPlayerByUid(long uid)
    {
        var result = DatabaseHelper.GetInstance<PlayerGameData>((int)uid);
        return result;
    }

    public static string NormalizeDisplayName(string? name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? DefaultDisplayName : normalized;
    }

    public bool EnsureDisplayName()
    {
        var normalized = NormalizeDisplayName(Name);
        if (string.Equals(Name, normalized, StringComparison.Ordinal))
            return false;

        Name = normalized;
        return true;
    }

    public PlayerProfile ToProfileProto()
    {
        var displayName = NormalizeDisplayName(Name);
        var proto = new PlayerProfile
        {
            Pid = (uint)Uid,
            Account = displayName,
            Name = displayName,
            Level = Level,
            Sex = Gender,
            Sign = Signature,
        };
        return proto;
    }
    
}

public class PlayerAttr
{
    public uint Gid { get; set; }
    public uint Sid { get; set; }
    public uint Val { get; set; }
}

public class PlayerStrAttr
{
    public uint Gid { get; set; }
    public uint Sid { get; set; }
    public string Val { get; set; } = "";
}