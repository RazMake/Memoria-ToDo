namespace Memoria.ToDo.Shared.Rendering.Tests;

/// <summary>Test-side color measures for "#rrggbb" values: WCAG contrast and CIEDE2000 difference.</summary>
internal static class ColorMath
{
    public static double Contrast(string foreground, string background)
    {
        var (lighter, darker) = (Luminance(foreground), Luminance(background));
        if (lighter < darker)
        {
            (lighter, darker) = (darker, lighter);
        }

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>CIEDE2000 (Sharma, Wu, Dalal 2005) between two sRGB colors, under D65.</summary>
    public static double DeltaE2000(string first, string second)
    {
        var (l1, a1, b1) = ToLab(first);
        var (l2, a2, b2) = ToLab(second);

        var c1 = Math.Sqrt(a1 * a1 + b1 * b1);
        var c2 = Math.Sqrt(a2 * a2 + b2 * b2);
        var cMean7 = Math.Pow((c1 + c2) / 2, 7);
        var g = 0.5 * (1 - Math.Sqrt(cMean7 / (cMean7 + Math.Pow(25, 7))));
        var ap1 = (1 + g) * a1;
        var ap2 = (1 + g) * a2;
        var cp1 = Math.Sqrt(ap1 * ap1 + b1 * b1);
        var cp2 = Math.Sqrt(ap2 * ap2 + b2 * b2);
        var hp1 = HueDegrees(b1, ap1);
        var hp2 = HueDegrees(b2, ap2);

        var dL = l2 - l1;
        var dC = cp2 - cp1;
        double dh = 0;
        if (cp1 * cp2 != 0)
        {
            dh = hp2 - hp1;
            if (dh > 180) dh -= 360;
            else if (dh < -180) dh += 360;
        }

        var dH = 2 * Math.Sqrt(cp1 * cp2) * Math.Sin(Rad(dh / 2));

        var lMean = (l1 + l2) / 2;
        var cMean = (cp1 + cp2) / 2;
        double hMean = hp1 + hp2;
        if (cp1 * cp2 != 0)
        {
            hMean = Math.Abs(hp1 - hp2) <= 180
                ? (hp1 + hp2) / 2
                : (hp1 + hp2 < 360 ? (hp1 + hp2 + 360) / 2 : (hp1 + hp2 - 360) / 2);
        }

        var t = 1 - 0.17 * Math.Cos(Rad(hMean - 30)) + 0.24 * Math.Cos(Rad(2 * hMean))
            + 0.32 * Math.Cos(Rad(3 * hMean + 6)) - 0.20 * Math.Cos(Rad(4 * hMean - 63));
        var dTheta = 30 * Math.Exp(-Math.Pow((hMean - 275) / 25, 2));
        var rc = 2 * Math.Sqrt(Math.Pow(cMean, 7) / (Math.Pow(cMean, 7) + Math.Pow(25, 7)));
        var sl = 1 + 0.015 * Math.Pow(lMean - 50, 2) / Math.Sqrt(20 + Math.Pow(lMean - 50, 2));
        var sc = 1 + 0.045 * cMean;
        var sh = 1 + 0.015 * cMean * t;
        var rt = -Math.Sin(Rad(2 * dTheta)) * rc;

        var lTerm = dL / sl;
        var cTerm = dC / sc;
        var hTerm = dH / sh;
        return Math.Sqrt(lTerm * lTerm + cTerm * cTerm + hTerm * hTerm + rt * cTerm * hTerm);
    }

    private static double Rad(double degrees) => degrees * Math.PI / 180;

    private static double HueDegrees(double b, double a)
    {
        if (b == 0 && a == 0) return 0;
        var h = Math.Atan2(b, a) * 180 / Math.PI;
        return h < 0 ? h + 360 : h;
    }

    private static (double L, double A, double B) ToLab(string hex)
    {
        var r = Linear(hex, 1);
        var g = Linear(hex, 3);
        var b = Linear(hex, 5);
        var x = (0.4124564 * r + 0.3575761 * g + 0.1804375 * b) / 0.95047;
        var y = 0.2126729 * r + 0.7151522 * g + 0.0721750 * b;
        var z = (0.0193339 * r + 0.1191920 * g + 0.9503041 * b) / 1.08883;
        double F(double v) => v > 216.0 / 24389 ? Math.Cbrt(v) : (24389.0 / 27 * v + 16) / 116;
        var (fx, fy, fz) = (F(x), F(y), F(z));
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    private static double Luminance(string hex) =>
        0.2126 * Linear(hex, 1) + 0.7152 * Linear(hex, 3) + 0.0722 * Linear(hex, 5);

    private static double Linear(string hex, int from)
    {
        var c = Convert.ToInt32(hex.Substring(from, 2), 16) / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
