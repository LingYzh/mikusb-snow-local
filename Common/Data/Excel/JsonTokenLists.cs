using Newtonsoft.Json.Linq;

namespace MikuSB.Data.Excel;

internal static class JsonTokenLists
{
    public static uint ReadUInt(JToken? token)
    {
        if (token == null || token.Type is JTokenType.Null or JTokenType.Undefined)
            return 0;
        if (token.Type == JTokenType.String)
        {
            var text = token.Value<string>();
            return uint.TryParse(text, out var parsed) ? parsed : 0;
        }
        if (token.Type == JTokenType.Integer)
            return token.Value<uint>();
        if (token.Type == JTokenType.Float)
            return (uint)token.Value<double>();
        return 0;
    }

    public static List<uint> ReadUIntList(JToken? token)
    {
        if (token == null || token.Type is JTokenType.Null or JTokenType.Undefined)
            return [];
        if (token.Type is JTokenType.Integer or JTokenType.Float or JTokenType.String)
        {
            var value = ReadUInt(token);
            return value > 0 ? [value] : [];
        }
        if (token is not JArray array)
            return [];

        var list = new List<uint>();
        foreach (var entry in array)
        {
            if (entry.Type is JTokenType.Integer or JTokenType.Float or JTokenType.String)
                list.Add(ReadUInt(entry));
        }

        return list;
    }

    public static List<List<uint>> ReadUIntTable(JToken? token)
    {
        if (token is not JArray array)
            return [];

        var table = new List<List<uint>>();
        foreach (var row in array)
        {
            if (row is JArray inner)
            {
                var list = ReadUIntList(inner);
                if (list.Count > 0)
                    table.Add(list);
            }
            else if (row.Type is JTokenType.Integer or JTokenType.Float or JTokenType.String)
            {
                table.Add([ReadUInt(row)]);
            }
        }

        return table;
    }
}
