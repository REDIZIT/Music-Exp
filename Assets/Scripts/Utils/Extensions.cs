using System;

public static class Extensions
{
    public static string ToPrettyMegaBytes(long bytes)
    {
        return (bytes / 1024d / 1024d).ToString("F1");
    }

    public static string ToPrettyTimeleft(TimeSpan timeleft)
    {
        if (timeleft == default) return "-";

        if (timeleft.TotalSeconds < 0) return "00:00:00";

        return timeleft.ToString("hh:mm:ss");
    }
}