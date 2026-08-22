using Kodnix.Character;

namespace MikuSB.Util;

public class IConsole
{
    public static readonly string PrefixContent = "[MikuSB]> ";
    public static readonly string Prefix = $"\u001b[38;2;255;192;203m{PrefixContent}\u001b[0m";
    private static readonly int HistoryMaxCount = 10;

    public static List<char> Input { get; set; } = [];
    private static int CursorIndex { get; set; } = 0;
    private static readonly List<string> InputHistory = [];
    private static int HistoryIndex = -1;

    public static event Action<string>? OnConsoleExcuteCommand;

    public static void InitConsole()
    {
        try
        {
            Console.Title = ConfigManager.Config.GameServer.GameServerName;
        }
        catch
        {
        }
    }

    public static int GetWidth(string str)
        => str.ToCharArray().Sum(EastAsianWidth.GetLength);

    public static void RedrawInput(List<char> input, bool hasPrefix = true)
        => RedrawInput(new string([.. input]), hasPrefix);

    public static void RedrawInput(string input, bool hasPrefix = true)
    {
        try
        {
            if (Console.IsOutputRedirected) return;
            var length = GetWidth(input);
            if (hasPrefix)
            {
                input = Prefix + input;
                length += GetWidth(PrefixContent);
            }

            if (Console.GetCursorPosition().Left > 0)
                Console.SetCursorPosition(0, Console.CursorTop);

            var bufferWidth = Math.Max(length, Console.BufferWidth);
            Console.Write(input + new string(' ', Math.Max(0, bufferWidth - length)));
            Console.SetCursorPosition(length, Console.CursorTop);
        }
        catch
        {
        }
    }

    #region Handlers

    public static void HandleEnter()
    {
        var input = new string([.. Input]);
        if (string.IsNullOrWhiteSpace(input)) return;

        // New line
        Console.WriteLine();
        Input = [];
        CursorIndex = 0;
        if (InputHistory.Count >= HistoryMaxCount)
            InputHistory.RemoveAt(0);
        InputHistory.Add(input);
        HistoryIndex = InputHistory.Count;

        // Handle command
        if (input.StartsWith('/')) input = input[1..].Trim();
        OnConsoleExcuteCommand?.Invoke(input);
    }

    public static void HandleBackspace()
    {
        try
        {
            if (CursorIndex <= 0) return;
            CursorIndex--;
            var targetWidth = GetWidth(Input[CursorIndex].ToString());
            Input.RemoveAt(CursorIndex);

            var (left, _) = Console.GetCursorPosition();
            Console.SetCursorPosition(left - targetWidth, Console.CursorTop);
            var remain = new string([.. Input.Skip(CursorIndex)]);
            Console.Write(remain + new string(' ', targetWidth));
            Console.SetCursorPosition(left - targetWidth, Console.CursorTop);
        }
        catch
        {
        }
    }

    public static void HandleUpArrow()
    {
        if (InputHistory.Count == 0) return;

        if (HistoryIndex > 0)
        {
            HistoryIndex--;
            var history = InputHistory[HistoryIndex];
            Input = [.. history];
            CursorIndex = Input.Count;
            RedrawInput(Input);
        }
    }

    public static void HandleDownArrow()
    {
        if (HistoryIndex >= InputHistory.Count) return;

        HistoryIndex++;
        if (HistoryIndex >= InputHistory.Count)
        {
            HistoryIndex = InputHistory.Count;
            Input = [];
            CursorIndex = 0;
        }
        else
        {
            var history = InputHistory[HistoryIndex];
            Input = [.. history];
            CursorIndex = Input.Count;
        }
        RedrawInput(Input);
    }

    public static void HandleLeftArrow()
    {
        try
        {
            if (CursorIndex <= 0) return;

            var (left, _) = Console.GetCursorPosition();
            CursorIndex--;
            Console.SetCursorPosition(left - GetWidth(Input[CursorIndex].ToString()), Console.CursorTop);
        }
        catch
        {
        }
    }

    public static void HandleRightArrow()
    {
        try
        {
            if (CursorIndex >= Input.Count) return;

            var (left, _) = Console.GetCursorPosition();
            CursorIndex++;
            Console.SetCursorPosition(left + GetWidth(Input[CursorIndex - 1].ToString()), Console.CursorTop);
        }
        catch
        {
        }
    }

    public static void HandleInput(ConsoleKeyInfo keyInfo)
    {
        try
        {
            if (char.IsControl(keyInfo.KeyChar)) return;
            if (Input.Count >= (Console.BufferWidth - PrefixContent.Length)) return;
            HandleInput(keyInfo.KeyChar);
        }
        catch
        {
            if (!char.IsControl(keyInfo.KeyChar))
                HandleInput(keyInfo.KeyChar);
        }
    }

    public static void HandleInput(char keyChar)
    {
        try
        {
            Input.Insert(CursorIndex, keyChar);
            CursorIndex++;

            var (left, _) = Console.GetCursorPosition();
            Console.Write(new string([.. Input.Skip(CursorIndex - 1)]));
            Console.SetCursorPosition(left + GetWidth(keyChar.ToString()), Console.CursorTop);
        }
        catch
        {
        }
    }

    #endregion

    public static async Task ListenConsole(CancellationToken exitToken)
    {
        if (Console.IsInputRedirected)
        {
            var reader = Console.In;
            while (!exitToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(exitToken);
                if (line == null) break;
                if (line.StartsWith('/')) line = line[1..].Trim();
                OnConsoleExcuteCommand?.Invoke(line);
            }
            return;
        }

        while (!exitToken.IsCancellationRequested)
        {
            ConsoleKeyInfo keyInfo;
            try
            {
                if (!Console.KeyAvailable)
                {
                    await Task.Delay(10, exitToken);
                    continue;
                }
                keyInfo = Console.ReadKey(true);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                await Task.Delay(50, exitToken);
                continue;
            }

            switch (keyInfo.Key)
            {
                case ConsoleKey.Enter:
                    HandleEnter();
                    break;
                case ConsoleKey.Backspace:
                    HandleBackspace();
                    break;
                case ConsoleKey.LeftArrow:
                    HandleLeftArrow();
                    break;
                case ConsoleKey.RightArrow:
                    HandleRightArrow();
                    break;
                case ConsoleKey.UpArrow:
                    HandleUpArrow();
                    break;
                case ConsoleKey.DownArrow:
                    HandleDownArrow();
                    break;
                default:
                    HandleInput(keyInfo);
                    break;
            }
        }
    }
}