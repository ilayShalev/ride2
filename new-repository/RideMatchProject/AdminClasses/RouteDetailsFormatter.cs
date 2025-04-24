using RideMatchProject.Models;
using RideMatchProject.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.AdminClasses
{

    /// <summary>
    /// Class to handle route details formatting
    /// </summary>
    public static class RouteDetailsFormatter
    {
        public static void DisplayRouteDetails(RichTextBox textBox, Vehicle vehicle)
        {
            textBox.Clear();
            FormatHeader(textBox, $"Route Details for Vehicle {vehicle.Id}");

            textBox.AppendText($"Driver: {vehicle.DriverName ?? $"Driver {vehicle.Id}"}\n");
            textBox.AppendText($"Vehicle Capacity: {vehicle.Capacity}\n");
            textBox.AppendText($"Total Distance: {vehicle.TotalDistance:F2} km\n");
            textBox.AppendText($"Total Time: {vehicle.TotalTime:F2} minutes\n");

            // Add departure time if available
            if (!string.IsNullOrEmpty(vehicle.DepartureTime))
            {
                FormatBold(textBox, $"Departure Time: {vehicle.DepartureTime}\n");
            }

            textBox.AppendText("\n");
            FormatBold(textBox, "Pickup Order:\n");

            if (vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
            {
                textBox.AppendText("No passengers assigned to this vehicle.\n");
                return;
            }

            DisplayPassengerList(textBox, vehicle.AssignedPassengers);
        }

        private static void DisplayPassengerList(
            RichTextBox textBox,
            List<Passenger> passengers)
        {
            for (int i = 0; i < passengers.Count; i++)
            {
                var passenger = passengers[i];
                textBox.AppendText($"{i + 1}. {passenger.Name}\n");

                string location = !string.IsNullOrEmpty(passenger.Address)
                    ? passenger.Address
                    : $"({passenger.Latitude:F4}, {passenger.Longitude:F4})";
                textBox.AppendText($"   Pickup at: {location}\n");

                // Display estimated pickup time if available
                if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                {
                    FormatBold(textBox, $"   Estimated pickup time: {passenger.EstimatedPickupTime}\n");
                }

                textBox.AppendText("\n");
            }
        }

        public static void DisplayDetailedRoutes(
            RichTextBox textBox,
            Dictionary<int, RouteDetails> routeDetails,
            Solution solution,
            DatabaseService dbService)
        {
            textBox.Clear();

            if (routeDetails.Count == 0)
            {
                textBox.AppendText("No route details available.\n\n");
                textBox.AppendText("Load a route and use the 'Get Google Routes' button to see detailed timing information.");
                return;
            }

            foreach (var detail in routeDetails.Values.OrderBy(d => d.VehicleId))
            {
                DisplaySingleRouteDetails(textBox, detail, solution, dbService);
                textBox.AppendText("--------------------------------\n\n");
            }
        }

        private static void DisplaySingleRouteDetails(
            RichTextBox textBox,
            RouteDetails detail,
            Solution solution,
            DatabaseService dbService)
        {
            // Get vehicle info
            var vehicle = solution.Vehicles.FirstOrDefault(v => v.Id == detail.VehicleId);
            string startLocation = vehicle != null && !string.IsNullOrEmpty(vehicle.StartAddress)
                ? vehicle.StartAddress
                : $"({vehicle?.StartLatitude ?? 0:F4}, {vehicle?.StartLongitude ?? 0:F4})";

            FormatBold(textBox, $"Vehicle {detail.VehicleId}\n");
            textBox.AppendText($"Start Location: {startLocation}\n");
            textBox.AppendText($"Driver: {vehicle?.DriverName ?? "Unknown"}\n");
            textBox.AppendText($"Total Distance: {detail.TotalDistance:F2} km\n");
            textBox.AppendText($"Total Time: {detail.TotalTime:F2} min\n");

            if (!string.IsNullOrEmpty(vehicle?.DepartureTime))
            {
                FormatBold(textBox, $"Departure Time: {vehicle.DepartureTime}\n");
            }

            textBox.AppendText("\n");
            FormatBold(textBox, "Stop Details:\n");

            DisplayStopDetails(textBox, detail, vehicle, dbService);
        }

        private static void DisplayStopDetails(
            RichTextBox textBox,
            RouteDetails detail,
            Vehicle vehicle,
            DatabaseService dbService)
        {
            int stopNumber = 1;
            foreach (var stop in detail.StopDetails)
            {
                if (stop.PassengerId >= 0)
                {
                    DisplayPassengerStop(textBox, stop, vehicle, stopNumber);
                }
                else
                {
                    DisplayDestinationStop(textBox, stop, dbService, stopNumber);
                }

                // Display stats for this stop
                textBox.AppendText($"   Distance: {stop.DistanceFromPrevious:F2} km\n");
                textBox.AppendText($"   Time: {stop.TimeFromPrevious:F2} min\n");
                textBox.AppendText($"   Cumulative: {stop.CumulativeDistance:F2} km, {stop.CumulativeTime:F2} min\n\n");
                stopNumber++;
            }
        }

        private static void DisplayPassengerStop(
            RichTextBox textBox,
            StopDetail stop,
            Vehicle vehicle,
            int stopNumber)
        {
            var passenger = vehicle?.AssignedPassengers?.FirstOrDefault(p => p.Id == stop.PassengerId);
            string stopName = passenger != null ? passenger.Name : $"Passenger {stop.PassengerName}";
            string stopLocation = passenger != null && !string.IsNullOrEmpty(passenger.Address)
                ? passenger.Address
                : $"({passenger?.Latitude ?? 0:F4}, {passenger?.Longitude ?? 0:F4})";

            FormatBold(textBox, $"{stopNumber}. {stopName}\n");
            textBox.AppendText($"   Location: {stopLocation}\n");

            // Display estimated pickup time if available
            if (passenger != null && !string.IsNullOrEmpty(passenger.EstimatedPickupTime))
            {
                FormatBold(textBox, $"   Pickup Time: {passenger.EstimatedPickupTime}\n");
            }
        }

        private static void DisplayDestinationStop(
            RichTextBox textBox,
            StopDetail stop,
            DatabaseService dbService,
            int stopNumber)
        {
            string stopName = "Destination";
            string stopLocation = string.Empty;

            try
            {
                var dest = dbService.GetDestinationAsync().GetAwaiter().GetResult();
                stopLocation = !string.IsNullOrEmpty(dest.Address)
                    ? dest.Address
                    : $"({dest.Latitude:F4}, {dest.Longitude:F4})";
            }
            catch
            {
                stopLocation = "Destination coordinates unavailable";
            }

            FormatBold(textBox, $"{stopNumber}. {stopName}\n");
            textBox.AppendText($"   Location: {stopLocation}\n");
        }

        private static void FormatHeader(RichTextBox textBox, string text)
        {
            textBox.SelectionFont = new Font(textBox.Font, FontStyle.Bold);
            textBox.AppendText(text + "\n\n");
            textBox.SelectionFont = textBox.Font;
        }

        private static void FormatBold(RichTextBox textBox, string text)
        {
            textBox.SelectionFont = new Font(textBox.Font, FontStyle.Bold);
            textBox.AppendText(text);
            textBox.SelectionFont = textBox.Font;
        }
    }


}
