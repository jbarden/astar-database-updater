using System.Globalization;

namespace AStar.Update.Database.WorkerService.Services;

internal static class TimeDelay
{
    public static TimeSpan CalculateDelayToNextRun(string targetTime)
    {
        var duration = DateTime.Parse(targetTime, CultureInfo.CurrentCulture).Subtract(DateTime.Now);
        if (duration < TimeSpan.Zero)
        {
            duration = duration.Add(TimeSpan.FromHours(24));
        }

        return duration;
    }
}