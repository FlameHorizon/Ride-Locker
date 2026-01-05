using Website.Models;

public static class DriverDynamics
{
    public static double[] DecelerationsRates(List<TrackPoint> track)
    {
        double[] xs = track.Select(x => x.Time.ToOADate()).ToArray();
        double[] ys = track.Select(x => x.Speed * 3.6).ToArray();
        return DecelerationsRates(xs, ys);
    }

    public static double[] DecelerationsRates(double[] xs, double[] ys)
    {
        double[] result = new double[xs.Length];
        for (int i = 1; i < ys.Length; i++)
        {
            DateTime dt1 = DateTime.FromOADate(xs[i - 1]);
            DateTime dt2 = DateTime.FromOADate(xs[i]);
            double dtSec = (dt2 - dt1).TotalSeconds;
            if (dtSec <= 0)
            {
                result[i] = 0;
                continue;
            }

            // Since speed is already converted into km/h, remember to convert
            // them here into m/s.
            double value = (ys[i] - ys[i - 1]) / 3.6 / dtSec;
            result[i] = value < 0 ? value : 0;
        }

        return result;
    }

    public static double[] AccelerationsRates(List<TrackPoint> track)
    {
        double[] xs = track.Select(x => x.Time.ToOADate()).ToArray();
        double[] ys = track.Select(x => x.Speed * 3.6).ToArray();
        return AccelerationsRates(xs, ys);
    }

    public static double[] AccelerationsRates(double[] xs, double[] ys)
    {
        double[] result = new double[xs.Length];
        for (int i = 1; i < ys.Length; i++)
        {
            DateTime dt1 = DateTime.FromOADate(xs[i - 1]);
            DateTime dt2 = DateTime.FromOADate(xs[i]);
            double dtSec = (dt2 - dt1).TotalSeconds;
            if (dtSec <= 0)
            {
                result[i] = 0;
                continue;
            }

            // Since speed is already converted into km/h, remember to convert
            // them here into m/s.
            double value = (ys[i] - ys[i - 1]) / 3.6 / dtSec;
            result[i] = value > 0 ? value : 0;
        }

        return result;
    }
}
