using MikuSB.Data;
using System.Text.Json.Nodes;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Gacha;

internal static class GachaPoolList
{
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
