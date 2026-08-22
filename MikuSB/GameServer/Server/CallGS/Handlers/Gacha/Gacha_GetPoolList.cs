using MikuSB.Data;
using System.Text.Json.Nodes;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Gacha;

internal static class GachaPoolList
{
    // Gacha.lua parses these as compact calendar values with ParseTime(), not
    // as Unix timestamps.  Keep every locally bundled pool available over a
    // stable offline time window.
    private const string OfflinePoolBegin = "202001010000";
    private const string OfflinePoolEnd = "203512310400";

    public static async Task Send(Connection connection, string api)
    {
        var pools = new JsonObject();
        var list = new JsonArray();
        foreach (var id in GameData.GachaData.Keys.OrderBy(x => x))
        {
            pools[id.ToString()] = 1;
            list.Add((int)id);
        }

        var rsp = new JsonObject
        {
            ["tbPool"] = list,
            ["tbOpen"] = pools.DeepClone(),
            ["tbList"] = list.DeepClone()
        };
        foreach (var (key, value) in pools)
            rsp[key] = value?.DeepClone();

        await CallGSRouter.SendScript(connection, api, rsp.ToJsonString());
    }

    public static Task SendOpenTimes(Connection connection)
    {
        // Lua indexes this table with a numeric pool id (tbOpenTime[id]). A
        // JSON object has string keys after decoding, so id 1 would not find
        // object key "1".  Send an array instead: JSON index 0 becomes Lua
        // index 1 and therefore aligns with the configured pool IDs.
        var maxPoolId = GameData.GachaData.Keys.DefaultIfEmpty(0u).Max();
        var openTimes = new JsonArray();
        for (var id = 1u; id <= maxPoolId; id++)
        {
            // The client treats an absent entry as a closed pool.  Filling
            // holes is harmless and keeps every configured ID aligned.
            openTimes.Add(new JsonArray
            {
                OfflinePoolBegin,
                OfflinePoolEnd
            });
        }

        var response = new JsonObject { ["tbOpenTime"] = openTimes };
        return CallGSRouter.SendScript(connection, "Gacha_GetOpenTime", response.ToJsonString());
    }
}

[CallGSApi("Gacha_GetOpenTime")]
public class Gacha_GetOpenTime : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
        => GachaPoolList.SendOpenTimes(connection);
}

[CallGSApi("Gacha_GetPoolList")]
public class Gacha_GetPoolList : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
        => GachaPoolList.Send(connection, "Gacha_GetPoolList");
}

[CallGSApi("Gacha_GetOpenPool")]
public class Gacha_GetOpenPool : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
        => GachaPoolList.Send(connection, "Gacha_GetOpenPool");
}

[CallGSApi("Gacha_GetPool")]
public class Gacha_GetPool : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
        => GachaPoolList.Send(connection, "Gacha_GetPool");
}
