using System;

namespace RideMatchProject.Utilities
{
    /// <summary>
    /// Helper utilities for route scheduling logic
    /// </summary>
    public static class RouteScheduleHelper
    {
        /// <summary>
        /// Determines whether to show today's or tomorrow's route based on target arrival time
        /// </summary>
        /// <param name="targetTimeString">Format: "HH:MM:SS"</param>
        /// <returns>Date string in format "yyyy-MM-dd" for the appropriate day</returns>
        public static string GetRouteQueryDate(string targetTimeString)
        {
            // Get current date/time
            DateTime now = DateTime.Now;

            // Default to 8:00 AM if target time is invalid
            TimeSpan targetTimeSpan;
            if (!TimeSpan.TryParse(targetTimeString, out targetTimeSpan))
            {
                targetTimeSpan = new TimeSpan(8, 0, 0);
            }

            // Create today's target time by combining today's date with target time
            DateTime todayTargetTime = DateTime.Today.Add(targetTimeSpan);

            // Always show today's route if target hasn't passed; tomorrow's if it has
            if (todayTargetTime > now)
            {
                // Target time is still in the future - show today's route
                return DateTime.Today.ToString("yyyy-MM-dd");
            }
            else
            {
                // Target time has passed - show tomorrow's route
                return DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
            }
        }

        /// <summary>
        /// Debug version that logs information about the date calculation
        /// </summary>
        public static string GetRouteQueryDateWithLogging(string targetTimeString)
        {
            DateTime now = DateTime.Now;
            Console.WriteLine($"Current date/time: {now}");
            Console.WriteLine($"Target time string: {targetTimeString}");

            // Default to 8:00 AM if target time is invalid
            TimeSpan targetTimeSpan;
            if (!TimeSpan.TryParse(targetTimeString, out targetTimeSpan))
            {
                Console.WriteLine("Failed to parse target time, defaulting to 8:00 AM");
                targetTimeSpan = new TimeSpan(8, 0, 0);
            }
            else
            {
                Console.WriteLine($"Parsed target time: {targetTimeSpan}");
            }

            // Create today's target time
            DateTime todayTargetTime = DateTime.Today.Add(targetTimeSpan);
            Console.WriteLine($"Target time today would be: {todayTargetTime}");
            Console.WriteLine($"Has target time passed? {todayTargetTime <= now}");

            // If target time has passed for today, show tomorrow's route
            if (todayTargetTime <= now)
            {
                Console.WriteLine("Target time has passed - showing tomorrow's route");
                return DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
            }

            // Otherwise show today's route
            Console.WriteLine("Target time has not passed - showing today's route");
            return DateTime.Today.ToString("yyyy-MM-dd");
        }
    }
}