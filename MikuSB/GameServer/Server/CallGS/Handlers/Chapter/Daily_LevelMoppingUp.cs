using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Chapter;

[CallGSApi("Daily_LevelMoppingUp")]
public class Daily_LevelMoppingUp : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var request = JsonSerializer.Deserialize<DailyLevelMoppingUpParam>(param);
        if (request?.LevelId is null or 0)
        {
            await CallGSRouter.SendScript(connection, "Daily_LevelMoppingUp", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var parameters = new JsonObject { ["nLevelID"] = request.LevelId };
        var (awards, sync) = await StageSettlement.GrantMoppingUpAsync(
            connection.Player!,
            parameters,
            request.Multiple);

        var response = new JsonObject
        {
            ["nLevelID"] = request.LevelId,
            ["nMultiple"] = (int)Math.Clamp(request.Multiple, 1u, 20u),
            ["nTeamID"] = request.TeamId ?? 1,
            ["tbAward"] = awards
        };
        await CallGSRouter.SendScript(connection, "Daily_LevelMoppingUp", response.ToJsonString(), sync);
    }
}

internal sealed class DailyLevelMoppingUpParam
{
    [JsonPropertyName("nLevelID")]
    public uint? LevelId { get; set; }

    [JsonPropertyName("nMultiple")]
    public uint Multiple { get; set; } = 1;

    [JsonPropertyName("nTeamID")]
    public uint? TeamId { get; set; }
}
