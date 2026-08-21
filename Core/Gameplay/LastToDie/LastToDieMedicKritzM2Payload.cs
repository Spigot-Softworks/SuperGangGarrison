namespace OpenGarrison.Core.LastToDie;

/// <summary>
/// Immutable Last to Die payload captured by a Kritzkrieg M2 projectile when
/// it is spawned. The base Kritz M2 bit distinguishes the healing needle from
/// a stock Medic primary needle and leaves room for later projectile perks.
/// </summary>
public readonly record struct LastToDieMedicKritzM2Payload(
    bool IsMedicKritzM2,
    bool AppliesHailMary,
    bool AppliesNeurotoxin,
    bool AppliesJavelin)
{
    private const byte MedicKritzM2Bit = 1 << 0;
    private const byte HailMaryBit = 1 << 1;
    private const byte NeurotoxinBit = 1 << 2;
    private const byte JavelinBit = 1 << 3;
    private const byte KnownBits = MedicKritzM2Bit | HailMaryBit | NeurotoxinBit | JavelinBit;

    public static LastToDieMedicKritzM2Payload Create(
        bool appliesHailMary,
        bool appliesNeurotoxin,
        bool appliesJavelin = false)
        => new(
            IsMedicKritzM2: true,
            AppliesHailMary: appliesHailMary,
            AppliesNeurotoxin: appliesNeurotoxin,
            AppliesJavelin: appliesJavelin);

    public byte Encode()
    {
        if (!IsMedicKritzM2)
        {
            return 0;
        }

        var encoded = MedicKritzM2Bit;
        if (AppliesHailMary)
        {
            encoded |= HailMaryBit;
        }

        if (AppliesNeurotoxin)
        {
            encoded |= NeurotoxinBit;
        }

        if (AppliesJavelin)
        {
            encoded |= JavelinBit;
        }

        return encoded;
    }

    public static LastToDieMedicKritzM2Payload Decode(int encoded)
    {
        var sanitized = checked((byte)(encoded & KnownBits));
        var isMedicKritzM2 = (sanitized & MedicKritzM2Bit) != 0;
        return new LastToDieMedicKritzM2Payload(
            isMedicKritzM2,
            isMedicKritzM2 && (sanitized & HailMaryBit) != 0,
            isMedicKritzM2 && (sanitized & NeurotoxinBit) != 0,
            isMedicKritzM2 && (sanitized & JavelinBit) != 0);
    }
}
