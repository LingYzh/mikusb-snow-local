using MikuSB.Proto;
using MikuSB.Util;
using System.Reflection;

namespace MikuSB.GameServer.Server.CallGS;

public static class CallGSRouter
{
    private static readonly Logger Logger = new("CallGS");
    private static readonly Dictionary<string, ICallGSHandler> Handlers = [];
    private const string UnavailableTipKey = "ui.TxtNotOpen";

    public static void Init()
    {
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            var attrs = type.GetCustomAttributes<CallGSApiAttribute>().ToList();
            if (attrs.Count == 0) continue;
            var handler = (ICallGSHandler)Activator.CreateInstance(type)!;
            foreach (var attr in attrs)
                Handlers[attr.Api] = handler;
        }
        Logger.Info($"Registered {Handlers.Count} CallGS handlers.");
    }

    public static async Task Route(Connection connection, ReqCallGS req, ushort seqNo)
    {
        if (req.Api.StartsWith("ShopLogic_", StringComparison.Ordinal) ||
            req.Api.StartsWith("Gacha_", StringComparison.Ordinal) ||
            req.Api is "Chapter_DealLevelSettlement" or "House_Request")
        {
            Logger.Info($"{req.Api} param={TrimParam(req.Param)}");
        }

        if (Handlers.TryGetValue(req.Api, out var handler))
        {
            try
            {
                await handler.Handle(connection, req.Param, seqNo);
                await connection.Player!.OnHeartBeat();
            }
            catch (Exception e)
            {
                Logger.Error($"[{req.Api}] {e.Message}", e);
                await SendUnavailableResponse(connection, req.Api);
            }
            return;
        }

        Logger.Error($"No handler for CallGS API: {req.Api} param={TrimParam(req.Param)}");
        await SendUnavailableResponse(connection, req.Api);
    }

    public static async Task SendScript(Connection connection, string api, string arg, NtfSyncPlayer extra = null!)
    {
        var rsp = new NtfCallScript { Api = api, Arg = arg, ExtraSync = extra };
        await connection.SendPacket(CmdIds.NtfScript, rsp);
    }

    private static Task SendUnavailableResponse(Connection connection, string api)
    {
        // Shop Lua retries on sErr with no timeout, which freezes the loading spinner.
        // Return an empty success object so the client can exit the wait loop.
        if (api.StartsWith("ShopLogic_", StringComparison.Ordinal))
            return SendScript(connection, api, "{}");

        // Many client Lua handlers treat sErr/sError as a recoverable failure path,
        // which is preferable to leaving the request hanging forever.
        return SendScript(connection, api, $$"""{"sErr":"{{UnavailableTipKey}}","sError":"{{UnavailableTipKey}}"}""");
    }

    private static string TrimParam(string? param)
    {
        if (string.IsNullOrEmpty(param))
            return "";
        return param.Length <= 500 ? param : param[..500] + "...";
    }
}
