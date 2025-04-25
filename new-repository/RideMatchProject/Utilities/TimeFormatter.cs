using System;

namespace RideMatchProject.Utilities
{
    /// <summary>
    /// Utility class for formatting time values consistently throughout the application
    /// </summary>
    public static class TimeFormatter
    {
        /// <summary>
        /// Converts decimal minutes to a user-friendly time format (min:sec)
        /// </summary>
        /// <param name="decimalMinutes">Time in decimal minutes (e.g. 31.90)</param>
        /// <returns>Formatted time string (e.g. "31:54")</returns>
        public static string FormatMinutes(double decimalMinutes)
        {
            int minutes = (int)Math.Floor(decimalMinutes);
            int seconds = (int)Math.Round((decimalMinutes - minutes) * 60);

            // Handle case where seconds round up to 60
            if (seconds == 60)
            {
                minutes++;
                seconds = 0;
            }

            return $"{minutes}:{seconds:D2}";
        }

        /// <summary>
        /// Converts decimal minutes to a user-friendly time format with units
        /// </summary>
        /// <param name="decimalMinutes">Time in decimal minutes (e.g. 31.90)</param>
        /// <returns>Formatted time string (e.g. "31 min 54 sec")</returns>
        public static string FormatMinutesWithUnits(double decimalMinutes)
        {
            int minutes = (int)Math.Floor(decimalMinutes);
            int seconds = (int)Math.Round((decimalMinutes - minutes) * 60);

            // Handle case where seconds round up to 60
            if (seconds == 60)
            {
                minutes++;
                seconds = 0;
            }

            if (seconds == 0)
                return $"{minutes} min";

            return $"{minutes} min {seconds} sec";
        }
    }
}