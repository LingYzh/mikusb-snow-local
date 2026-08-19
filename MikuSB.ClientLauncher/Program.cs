using MikuSB.Loader;
using MikuSB.Util;

Console.Title = "尘白禁区 - 离线客户端启动器";
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("=================================================");
Console.WriteLine("        尘白禁区 (Snowbreak) 离线客户端启动器     ");
Console.WriteLine("=================================================");
Console.ResetColor();

try
{
    Console.WriteLine("[1/2] 正在读取服务端配置与补丁文件...");
    Console.WriteLine("[2/2] 正在启动客户端进程并挂钩本地服务端...");
    var pid = GameLaunchService.Launch(args);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[成功] 游戏客户端启动成功！(进程 ID: {pid})");
    Console.WriteLine("请在游戏登录界面输入任意账号/邮箱直接进入！");
    Console.ResetColor();
    Thread.Sleep(2500);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n[失败] 启动客户端遇到错误: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine("\n常见原因与解决办法:");
    Console.WriteLine("1. 请确保本地服务端已先启动 (1-启动本地服务端.bat)");
    Console.WriteLine("2. 请检查 Config/Config.json 中的 GamePath 路径是否正确");
    Console.WriteLine("3. 请检查 Patch/MikuSB-Patch.dll 是否被安全软件拦截");
    Console.WriteLine("\n按任意键退出...");
    Console.ReadKey();
}
