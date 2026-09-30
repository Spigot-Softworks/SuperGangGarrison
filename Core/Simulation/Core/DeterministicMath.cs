namespace OpenGarrison.Core;

/// <summary>
/// Platform-independent replacements for the <see cref="MathF"/> transcendental functions.
/// </summary>
/// <remarks>
/// <see cref="MathF"/> defers these functions to the operating system's C runtime, and the
/// Windows and Linux runtimes round some results differently by one ulp. Simulation code must use
/// these instead so every platform produces bit-identical state. The implementations follow fdlibm,
/// evaluate in double precision, and use only IEEE basic arithmetic, which is exactly rounded
/// everywhere. Do not introduce <see cref="Math"/> or <see cref="MathF"/> transcendental calls or
/// fused multiply-add here.
/// </remarks>
public static class DeterministicMath
{
    private const double TwoOverPi = 6.36619772367581382433e-01;
    private const double PiOver2 = 1.57079632679489655800e+00;
    private const double PiOver2Hi = 1.57079632673412561417e+00;
    private const double PiOver2Lo = 6.07710050650619224932e-11;

    private const double S1 = -1.66666666666666324348e-01;
    private const double S2 = 8.33333333332248946124e-03;
    private const double S3 = -1.98412698298579493134e-04;
    private const double S4 = 2.75573137070700676789e-06;
    private const double S5 = -2.50507602534068634195e-08;
    private const double S6 = 1.58969099521155010221e-10;

    private const double C1 = 4.16666666666666019037e-02;
    private const double C2 = -1.38888888888741095749e-03;
    private const double C3 = 2.48015872894767294178e-05;
    private const double C4 = -2.75573143513906633035e-07;
    private const double C5 = 2.08757232129817482790e-09;
    private const double C6 = -1.13596475577881948265e-11;

    private static readonly double[] AtanHi =
    [
        4.63647609000806093515e-01,
        7.85398163397448278999e-01,
        9.82793723247329054082e-01,
        1.57079632679489655800e+00,
    ];

    private static readonly double[] AtanLo =
    [
        2.26987774529616870924e-17,
        3.06161699786838301793e-17,
        1.39033110312309984516e-17,
        6.12323399573676603587e-17,
    ];

    private static readonly double[] AtanCoefficients =
    [
        3.33333333333329318027e-01,
        -1.99999999998764832476e-01,
        1.42857142725034663711e-01,
        -1.11111104054623557880e-01,
        9.09088713343650656196e-02,
        -7.69187620504482999495e-02,
        6.66107313738753120669e-02,
        -5.83357013379057348645e-02,
        4.97687799461593236017e-02,
        -3.65315727442169155270e-02,
        1.62858201153657823623e-02,
    ];

    private const double Ln2Hi = 6.93147180369123816490e-01;
    private const double Ln2Lo = 1.90821492927058770002e-10;
    private const double InvLn2 = 1.44269504088896338700e+00;
    private const double ExpOverflowThreshold = 7.09782712893383973096e+02;
    private const double ExpUnderflowThreshold = -7.45133219101941108420e+02;
    private const double P1 = 1.66666666666666019037e-01;
    private const double P2 = -2.77777777770155933842e-03;
    private const double P3 = 6.61375632143793436117e-05;
    private const double P4 = -1.65339022054652515390e-06;
    private const double P5 = 4.13813679705723846039e-08;

    private const double Lg1 = 6.666666666666735130e-01;
    private const double Lg2 = 3.999999999940941908e-01;
    private const double Lg3 = 2.857142874366239149e-01;
    private const double Lg4 = 2.222219843214978396e-01;
    private const double Lg5 = 1.818357216161805012e-01;
    private const double Lg6 = 1.531383769920937332e-01;
    private const double Lg7 = 1.479819860511658591e-01;
    private const double Sqrt2 = 1.41421356237309514547e+00;

    public static float Sin(float x) => (float)Sin((double)x);

    public static float Cos(float x) => (float)Cos((double)x);

    public static float Atan(float x) => (float)Atan((double)x);

    public static float Atan2(float y, float x) => (float)Atan2((double)y, (double)x);

    public static float Exp(float x) => (float)Exp((double)x);

    public static float Pow(float x, float y) => (float)Pow((double)x, (double)y);

    private static double Sin(double x)
    {
        if (x == 0d || double.IsNaN(x))
        {
            return x;
        }

        if (double.IsInfinity(x))
        {
            return double.NaN;
        }

        var (reduced, quadrant) = ReduceByQuarterTurns(x);
        return quadrant switch
        {
            0 => KernelSin(reduced),
            1 => KernelCos(reduced),
            2 => -KernelSin(reduced),
            _ => -KernelCos(reduced),
        };
    }

    private static double Cos(double x)
    {
        if (!double.IsFinite(x))
        {
            return double.NaN;
        }

        var (reduced, quadrant) = ReduceByQuarterTurns(x);
        return quadrant switch
        {
            0 => KernelCos(reduced),
            1 => -KernelSin(reduced),
            2 => -KernelCos(reduced),
            _ => KernelSin(reduced),
        };
    }

    private static (double Reduced, int Quadrant) ReduceByQuarterTurns(double x)
    {
        // Two-part Cody-Waite reduction; n * PiOver2Hi stays exact for every
        // angle the simulation produces (|n| < 2^20).
        var n = Math.Round(x * TwoOverPi);
        var reduced = (x - (n * PiOver2Hi)) - (n * PiOver2Lo);
        var quadrant = (int)(n - (4d * Math.Floor(n * 0.25d)));
        return (reduced, quadrant);
    }

    private static double KernelSin(double x)
    {
        var z = x * x;
        return x + x * z * (S1 + z * (S2 + z * (S3 + z * (S4 + z * (S5 + z * S6)))));
    }

    private static double KernelCos(double x)
    {
        var z = x * x;
        return 1d - 0.5d * z + z * z * (C1 + z * (C2 + z * (C3 + z * (C4 + z * (C5 + z * C6)))));
    }

    private static double Atan(double x)
    {
        if (double.IsNaN(x))
        {
            return x;
        }

        var magnitude = Math.Abs(x);
        if (magnitude >= 7.3786976294838206464e+19)
        {
            var limit = AtanHi[3] + AtanLo[3];
            return x > 0d ? limit : -limit;
        }

        int segment;
        if (magnitude < 0.4375d)
        {
            if (magnitude < 7.450580596923828125e-09)
            {
                return x;
            }

            segment = -1;
            magnitude = x;
        }
        else if (magnitude < 1.1875d)
        {
            if (magnitude < 0.6875d)
            {
                segment = 0;
                magnitude = ((2d * magnitude) - 1d) / (2d + magnitude);
            }
            else
            {
                segment = 1;
                magnitude = (magnitude - 1d) / (magnitude + 1d);
            }
        }
        else if (magnitude < 2.4375d)
        {
            segment = 2;
            magnitude = (magnitude - 1.5d) / (1d + (1.5d * magnitude));
        }
        else
        {
            segment = 3;
            magnitude = -1d / magnitude;
        }

        var z = magnitude * magnitude;
        var w = z * z;
        var a = AtanCoefficients;
        var s1 = z * (a[0] + w * (a[2] + w * (a[4] + w * (a[6] + w * (a[8] + w * a[10])))));
        var s2 = w * (a[1] + w * (a[3] + w * (a[5] + w * (a[7] + w * a[9]))));
        if (segment < 0)
        {
            return magnitude - (magnitude * (s1 + s2));
        }

        var result = AtanHi[segment] - (((magnitude * (s1 + s2)) - AtanLo[segment]) - magnitude);
        return x < 0d ? -result : result;
    }

    private static double Atan2(double y, double x)
    {
        if (double.IsNaN(x) || double.IsNaN(y))
        {
            return double.NaN;
        }

        if (y == 0d)
        {
            if (x > 0d || (x == 0d && !double.IsNegative(x)))
            {
                return y;
            }

            return double.IsNegative(y) ? -Math.PI : Math.PI;
        }

        if (x == 0d)
        {
            return y > 0d ? PiOver2 : -PiOver2;
        }

        if (double.IsInfinity(x))
        {
            if (double.IsInfinity(y))
            {
                var diagonal = x > 0d ? Math.PI * 0.25d : Math.PI * 0.75d;
                return y > 0d ? diagonal : -diagonal;
            }

            if (x > 0d)
            {
                return y > 0d ? 0d : -0d;
            }

            return y > 0d ? Math.PI : -Math.PI;
        }

        if (double.IsInfinity(y))
        {
            return y > 0d ? PiOver2 : -PiOver2;
        }

        var angle = Atan(Math.Abs(y / x));
        if (x < 0d)
        {
            angle = Math.PI - angle;
        }

        return y < 0d ? -angle : angle;
    }

    private static double Exp(double x)
    {
        if (double.IsNaN(x))
        {
            return x;
        }

        if (x > ExpOverflowThreshold)
        {
            return double.PositiveInfinity;
        }

        if (x < ExpUnderflowThreshold)
        {
            return 0d;
        }

        if (Math.Abs(x) < 3.7252902984e-09)
        {
            return 1d + x;
        }

        var k = Math.Round(x * InvLn2);
        var hi = x - (k * Ln2Hi);
        var lo = k * Ln2Lo;
        var r = hi - lo;
        var t = r * r;
        var c = r - t * (P1 + t * (P2 + t * (P3 + t * (P4 + t * P5))));
        var y = 1d - ((lo - ((r * c) / (2d - c))) - hi);
        return Math.ScaleB(y, (int)k);
    }

    private static double Log(double x)
    {
        // Callers guarantee a positive, finite argument.
        var exponent = 0;
        var bits = BitConverter.DoubleToInt64Bits(x);
        if (bits < 0x0010000000000000L)
        {
            x *= 18014398509481984d;
            exponent -= 54;
            bits = BitConverter.DoubleToInt64Bits(x);
        }

        exponent += (int)((bits >> 52) - 1023);
        var mantissa = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
        if (mantissa > Sqrt2)
        {
            mantissa *= 0.5d;
            exponent += 1;
        }

        var f = mantissa - 1d;
        var s = f / (2d + f);
        var z = s * s;
        var w = z * z;
        var t1 = w * (Lg2 + w * (Lg4 + w * Lg6));
        var t2 = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7)));
        var r = t2 + t1;
        var halfSquare = 0.5d * f * f;
        return (exponent * Ln2Hi) - ((halfSquare - ((s * (halfSquare + r)) + (exponent * Ln2Lo))) - f);
    }

    private static double Pow(double x, double y)
    {
        if (y == 0d || x == 1d)
        {
            return 1d;
        }

        if (double.IsNaN(x) || double.IsNaN(y))
        {
            return double.NaN;
        }

        if (double.IsInfinity(y))
        {
            var magnitude = Math.Abs(x);
            if (magnitude == 1d)
            {
                return 1d;
            }

            return (magnitude > 1d) == (y > 0d) ? double.PositiveInfinity : 0d;
        }

        var isInteger = Math.Floor(y) == y;
        var isOddInteger = isInteger
            && Math.Abs(y) < 9007199254740992d
            && y - (2d * Math.Floor(y * 0.5d)) == 1d;
        if (x == 0d || double.IsInfinity(x))
        {
            var growsWithExponent = double.IsInfinity(x);
            var result = (y > 0d) == growsWithExponent ? double.PositiveInfinity : 0d;
            return double.IsNegative(x) && isOddInteger ? -result : result;
        }

        var sign = 1d;
        if (x < 0d)
        {
            if (!isInteger)
            {
                return double.NaN;
            }

            if (isOddInteger)
            {
                sign = -1d;
            }

            x = -x;
        }

        return sign * Exp(y * Log(x));
    }
}
