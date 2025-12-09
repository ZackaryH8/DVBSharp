using System.Globalization;

namespace DVBSharp.Tuner.Transmitters;

internal static class OsgbConverter
{
    private const double AirySemiMajor = 6377563.396;
    private const double AirySemiMinor = 6356256.909;
    private const double Wgs84SemiMajor = 6378137.0;
    private const double Wgs84SemiMinor = 6356752.3141;
    private const double ScaleFactor = 0.9996012717;
    private const double LatOrigin = 49 * Math.PI / 180;
    private const double LonOrigin = -2 * Math.PI / 180;
    private const double NorthOrigin = -100000;
    private const double EastOrigin = 400000;
    private const double HelmertTx = 446.448;
    private const double HelmertTy = -125.157;
    private const double HelmertTz = 542.06;
    private const double HelmertRx = 0.1502 * Math.PI / (180 * 3600);
    private const double HelmertRy = 0.2470 * Math.PI / (180 * 3600);
    private const double HelmertRz = 0.8421 * Math.PI / (180 * 3600);
    private const double HelmertScale = 20.4894 * 1e-6;

    public static bool TryParseGridReference(string? ngr, out double easting, out double northing)
    {
        easting = 0;
        northing = 0;
        if (string.IsNullOrWhiteSpace(ngr))
        {
            return false;
        }

        var value = ngr.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        if (value.Length < 2) return false;

        var l1 = value[0];
        var l2 = value[1];
        if (!char.IsLetter(l1) || !char.IsLetter(l2)) return false;

        int i1 = l1 - 'A';
        int i2 = l2 - 'A';
        if (i1 > 7) i1--;
        if (i2 > 7) i2--;

        var e100km = ((i1 - 2) % 5) * 5 + (i2 % 5);
        var n100km = (19 - (i1 / 5) * 5) - (i2 / 5);

        var digits = value.Substring(2);
        if (digits.Length % 2 == 1) return false;

        var half = digits.Length / 2;
        var eDigits = digits.Substring(0, half).PadRight(5, '0');
        var nDigits = digits.Substring(half).PadRight(5, '0');

        if (!int.TryParse(eDigits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var e) ||
            !int.TryParse(nDigits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            return false;
        }

        easting = e100km * 100000 + e;
        northing = n100km * 100000 + n;
        return true;
    }

    public static bool TryOsGridToWgs84(double easting, double northing, out double lat, out double lon)
    {
        lat = 0;
        lon = 0;

        if (double.IsNaN(easting) || double.IsNaN(northing)) return false;

        var e2 = 1 - (Math.Pow(AirySemiMinor, 2) / Math.Pow(AirySemiMajor, 2));
        var n = (AirySemiMajor - AirySemiMinor) / (AirySemiMajor + AirySemiMinor);

        double phi = LatOrigin;
        double m = 0;

        do
        {
            phi = (northing - NorthOrigin - m) / (AirySemiMajor * ScaleFactor) + phi;
            var phi2 = phi * phi;
            var phi3 = phi2 * phi;
            m = AirySemiMinor * ScaleFactor *
                ((1 + n + (5.0 / 4.0) * n * n + (5.0 / 4.0) * n * n * n) * (phi - LatOrigin) -
                 (3 * n + 3 * n * n + (21.0 / 8.0) * n * n * n) * Math.Sin(phi - LatOrigin) * Math.Cos(phi + LatOrigin) +
                 ((15.0 / 8.0) * n * n + (15.0 / 8.0) * n * n * n) * Math.Sin(2 * (phi - LatOrigin)) * Math.Cos(2 * (phi + LatOrigin)) -
                 (35.0 / 24.0) * n * n * n * Math.Sin(3 * (phi - LatOrigin)) * Math.Cos(3 * (phi + LatOrigin)));
        } while (Math.Abs(northing - NorthOrigin - m) >= 1e-5);

        var sinPhi = Math.Sin(phi);
        var cosPhi = Math.Cos(phi);
        var tanPhi = Math.Tan(phi);

        var nu = AirySemiMajor * ScaleFactor / Math.Sqrt(1 - e2 * sinPhi * sinPhi);
        var rho = AirySemiMajor * ScaleFactor * (1 - e2) / Math.Pow(1 - e2 * sinPhi * sinPhi, 1.5);
        var eta2 = nu / rho - 1;
        var secPhi = 1 / cosPhi;
        var tan2 = tanPhi * tanPhi;
        var tan4 = tan2 * tan2;
        var tan6 = tan4 * tan2;

        var VII = tanPhi / (2 * nu * rho);
        var VIII = tanPhi / (24 * nu * nu * nu * rho) * (5 + 3 * tan2 + eta2 - 9 * tan2 * eta2);
        var IX = tanPhi / (720 * Math.Pow(nu, 5) * rho) * (61 + 90 * tan2 + 45 * tan4);
        var X = secPhi / nu;
        var XI = secPhi / (6 * nu * nu * nu) * (nu / rho + 2 * tan2);
        var XII = secPhi / (120 * Math.Pow(nu, 5)) * (5 + 28 * tan2 + 24 * tan4);
        var XIIA = secPhi / (5040 * Math.Pow(nu, 7)) * (61 + 662 * tan2 + 1320 * tan4 + 720 * tan6);

        var de = easting - EastOrigin;

        var latOsgb = phi - VII * de * de + VIII * Math.Pow(de, 4) - IX * Math.Pow(de, 6);
        var lonOsgb = LonOrigin + X * de - XI * Math.Pow(de, 3) + XII * Math.Pow(de, 5) - XIIA * Math.Pow(de, 7);

        ConvertOsgb36ToWgs84(latOsgb, lonOsgb, out lat, out lon);
        return true;
    }

    private static void ConvertOsgb36ToWgs84(double latOsgb, double lonOsgb, out double lat, out double lon)
    {
        var e2Osgb = 1 - (Math.Pow(AirySemiMinor, 2) / Math.Pow(AirySemiMajor, 2));
        var nu = AirySemiMajor * ScaleFactor / Math.Sqrt(1 - e2Osgb * Math.Pow(Math.Sin(latOsgb), 2));
        var h = 0;

        var x = (nu + h) * Math.Cos(latOsgb) * Math.Cos(lonOsgb);
        var y = (nu + h) * Math.Cos(latOsgb) * Math.Sin(lonOsgb);
        var z = ((1 - e2Osgb) * nu + h) * Math.Sin(latOsgb);

        var x2 = HelmertTx + (1 + HelmertScale) * x + (-HelmertRz) * y + (HelmertRy) * z;
        var y2 = HelmertTy + (HelmertRz) * x + (1 + HelmertScale) * y + (-HelmertRx) * z;
        var z2 = HelmertTz + (-HelmertRy) * x + (HelmertRx) * y + (1 + HelmertScale) * z;

        var e2Wgs = 1 - (Math.Pow(Wgs84SemiMinor, 2) / Math.Pow(Wgs84SemiMajor, 2));
        var p = Math.Sqrt(x2 * x2 + y2 * y2);
        var latPrev = Math.Atan2(z2, p * (1 - e2Wgs));
        double latNew;
        do
        {
            var sin = Math.Sin(latPrev);
            var nuWgs = Wgs84SemiMajor / Math.Sqrt(1 - e2Wgs * sin * sin);
            latNew = Math.Atan2(z2 + e2Wgs * nuWgs * sin, p);
            if (Math.Abs(latNew - latPrev) < 1e-11) break;
            latPrev = latNew;
        } while (true);

        lat = latNew * 180 / Math.PI;
        lon = Math.Atan2(y2, x2) * 180 / Math.PI;
    }
}
