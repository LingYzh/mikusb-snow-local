using MikuSB.Database;
using MikuSB.Database.Player;
using MikuSB.GameServer.Game.Player;
using System.Text.Json.Nodes;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Preview;

// Persists the random main-background selection made by RandomShowGirl.lua.
// The client updates its current session locally, then reloads these numeric
// attributes after the next login.
[CallGSApi("RandomShowGirl_SetMainBg")]
public class RandomShowGirl_SetMainBg : ICallGSHandler
{
    private const uint GroupId = 139;
    private const uint MainSkinStartSid = 401;
    private const uint MainSkinEndSid = 500;
    private const uint BackgroundModeSid = 901;
    private const uint CgStartSid = 1000;
    private const uint CgEndSid = 2000;

    public Task Handle(Connection connection, string param, ushort seqNo)
    {
        var request = ParseRequest(param);
        var is3D = ReadBool(request?["b3D"]);
        var startSid = is3D ? MainSkinStartSid : CgStartSid;
        var endSid = is3D ? MainSkinEndSid : CgEndSid;
        var selectedIds = ReadIds(request?["tbId"], endSid - startSid + 1);
        var player = connection.Player!;

        player.Data.Attrs.RemoveAll(x => x.Gid == GroupId &&
            x.Sid >= startSid && x.Sid <= endSid);
        for (var index = 0; index < selectedIds.Count; index++)
        {
            player.Data.Attrs.Add(new PlayerAttr
            {
                Gid = GroupId,
                Sid = startSid + (uint)index,
                Val = selectedIds[index]
            });
        }

        var mode = player.Data.Attrs.FirstOrDefault(x =>
            x.Gid == GroupId && x.Sid == BackgroundModeSid);
        if (mode == null)
        {
            mode = new PlayerAttr { Gid = GroupId, Sid = BackgroundModeSid };
            player.Data.Attrs.Add(mode);
        }
        mode.Val = is3D ? 0u : 1u;

        DatabaseHelper.SaveDatabaseType(player.Data);
        return Task.CompletedTask;
    }

    private static JsonObject? ParseRequest(string param)
    {
        try
        {
            return JsonNode.Parse(param) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static bool ReadBool(JsonNode? node)
    {
        if (node is not JsonValue value)
            return false;
        if (value.TryGetValue<bool>(out var result))
            return result;
        return value.TryGetValue<string>(out var text) &&
            bool.TryParse(text, out result) && result;
    }

    private static List<uint> ReadIds(JsonNode? node, uint maximum)
    {
        if (node is not JsonArray values)
            return [];

        var ids = new List<uint>();
        foreach (var entry in values)
        {
            if (entry is not JsonValue value)
                continue;
            if (!value.TryGetValue<uint>(out var id) || id == 0 || ids.Contains(id))
                continue;
            ids.Add(id);
            if (ids.Count >= maximum)
                break;
        }
        return ids;
    }
}
