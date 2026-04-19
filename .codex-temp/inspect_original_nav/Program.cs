using System.Text;

if (args.Length < 2)
{
    Console.WriteLine("usage: <pts-path> <point-id> [point-id]...");
    return;
}

var path = args[0];
var ids = args.Skip(1).Select(static value => int.Parse(value)).ToArray();
var points = ReadOriginalGg2NavMesh(path);

foreach (var id in ids)
{
    if (id < 0 || id >= points.Count)
    {
        Console.WriteLine($"missing {id}");
        continue;
    }

    var point = points[id];
    Console.WriteLine($"point {id} x={point.X} y={point.Y} out=[{string.Join(",", point.Outgoing)}] blocked=[{string.Join(",", point.Blocked)}]");
}

static List<OriginalGg2NavPoint> ReadOriginalGg2NavMesh(string path)
{
    var payload = File.ReadAllText(path).Trim();
    var topLevelEntries = ReadGameMakerSerializedList(HexToBytes(payload));
    var points = new List<OriginalGg2NavPoint>(topLevelEntries.Count);
    for (var index = 0; index < topLevelEntries.Count; index += 1)
    {
        var pointPayload = (string)topLevelEntries[index];
        var pointEntries = ReadGameMakerSerializedList(HexToBytes(pointPayload));
        if (pointEntries.Count < 3)
        {
            Console.WriteLine($"bad point index={index} entryCount={pointEntries.Count}");
            continue;
        }
        var x = (float)(double)pointEntries[0];
        var y = (float)(double)pointEntries[1];
        var outgoingPayload = (string)pointEntries[2];
        var outgoingEntries = ReadGameMakerSerializedList(HexToBytes(outgoingPayload));
        var outgoing = outgoingEntries
            .Select(static value => (int)Math.Round((double)value))
            .OrderBy(static value => value)
            .ToArray();
        var blocked = Array.Empty<int>();
        if (pointEntries.Count >= 4)
        {
            var blockedPayload = (string)pointEntries[3];
            var blockedEntries = ReadGameMakerSerializedList(HexToBytes(blockedPayload));
            blocked = blockedEntries
                .Select(static value => (int)Math.Round((double)value))
                .OrderBy(static value => value)
                .ToArray();
        }

        points.Add(new OriginalGg2NavPoint(x, y, outgoing, blocked));
    }

    return points;
}

static List<object> ReadGameMakerSerializedList(byte[] bytes)
{
    var count = BitConverter.ToInt32(bytes, 4);
    var values = new List<object>(count);
    var offset = 8;
    for (var index = 0; index < count; index += 1)
    {
        var itemType = BitConverter.ToInt32(bytes, offset);
        switch (itemType)
        {
            case 0:
                values.Add(ReadGameMakerReal(bytes, offset + 8));
                offset += 16;
                break;
            case 1:
                var stringLength = BitConverter.ToInt32(bytes, offset + 12);
                values.Add(Encoding.ASCII.GetString(bytes, offset + 16, stringLength));
                offset += 16 + stringLength;
                break;
            default:
                throw new InvalidDataException($"unsupported GameMaker ds_list item type {itemType} at index {index}");
        }
    }

    return values;
}

static double ReadGameMakerReal(byte[] bytes, int offset)
{
    var lowWord = BitConverter.ToUInt32(bytes, offset);
    var highWord = BitConverter.ToUInt32(bytes, offset + 4);
    var bits = ((ulong)lowWord << 32) | highWord;
    return BitConverter.Int64BitsToDouble((long)bits);
}

static byte[] HexToBytes(string hex)
{
    var bytes = new byte[hex.Length / 2];
    for (var index = 0; index < bytes.Length; index += 1)
    {
        bytes[index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
    }

    return bytes;
}

internal readonly record struct OriginalGg2NavPoint(float X, float Y, int[] Outgoing, int[] Blocked);
