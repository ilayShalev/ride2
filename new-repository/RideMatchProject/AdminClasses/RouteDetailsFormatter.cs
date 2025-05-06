using RideMatchProject.Models;
using RideMatchProject.Services;
using RideMatchProject.Utilities;
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
    /// This static class is responsible for formatting and displaying detailed route information for vehicles, including stops, passenger details, and related data.
    /// </summary>
    public static class RouteDetailsFormatter
    {
        /// <summary>
        /// Displays route details for a specific vehicle.
        /// </summary>
        /// <param name="textBox">The RichTextBox control where the route details will be displayed.</param>
        /// <param name="vehicle">The vehicle for which the route details will be displayed.</param>
        public static void DisplayRouteDetails(RichTextBox textBox, Vehicle vehicle)
        {
            textBox.Clear(); // Clear the text box before displaying new data.
            FormatHeader(textBox, $"Route Details for Vehicle {vehicle.Id}");

            // Display basic vehicle details.
            textBox.AppendText($"Driver: {vehicle.DriverName ?? $"Driver {vehicle.Id}"}\n");
            textBox.AppendText($"Vehicle Capacity: {vehicle.Capacity}\n");
            textBox.AppendText($"Total Distance: {vehicle.TotalDistance:F2} km\n");
            textBox.AppendText($"Total Time: {TimeFormatter.FormatMinutesWithUnits(vehicle.TotalTime)}\n");

            // Add departure time if available.
            if (!string.IsNullOrEmpty(vehicle.DepartureTime))
            {
                FormatBold(textBox, $"Departure Time: {vehicle.DepartureTime}\n");
            }

            textBox.AppendText("\n");
            FormatBold(textBox, "Pickup Order:\n");

            // If there are no assigned passengers, inform the user.
            if (vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
            {
                textBox.AppendText("No passengers assigned to this vehicle.\n");
                return;
            }

            // Display the list of assigned passengers.
            DisplayPassengerList(textBox, vehicle.AssignedPassengers);
        }

        /// <summary>
        /// Displays the list of passengers assigned to the vehicle.
        /// </summary>
        /// <param name="textBox">The RichTextBox control where the passenger list will be displayed.</param>
        /// <param name="passengers">The list of passengers assigned to the vehicle.</param>
        private static void DisplayPassengerList(RichTextBox textBox, List<Passenger> passengers)
        {
            // Iterate over each passenger and display their details.
            for (int i = 0; i < passengers.Count; i++)
            {
                var passenger = passengers[i];
                textBox.AppendText($"{i + 1}. {passenger.Name}\n");

                // Display the pickup location. If the address is unavailable, use latitude and longitude.
                string location = !string.IsNullOrEmpty(passenger.Address)
                    ? passenger.Address
                    : $"({passenger.Latitude:F4}, {passenger.Longitude:F4})";
                textBox.AppendText($"   Pickup at: {location}\n");

                // Display estimated pickup time if available.
                if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                {
                    FormatBold(textBox, $"   Estimated pickup time: {passenger.EstimatedPickupTime}\n");
                }

                textBox.AppendText("\n");
            }
        }

        /// <summary>
        /// Displays detailed route information for all vehicles.
        /// </summary>
        /// <param name="textBox">The RichTextBox control where the route details will be displayed.</param>
        /// <param name="routeDetails">A dictionary of route details, keyed by vehicle ID.</param>
        /// <param name="solution">The solution object containing the vehicles and other related data.</param>
        /// <param name="dbService">The service used to interact with the database for additional data, such as destination information.</param>
        public static void DisplayDetailedRoutes(
            RichTextBox textBox,
            Dictionary<int, RouteDetails> routeDetails,
            Solution solution,
            DatabaseService dbService)
        {
            textBox.Clear(); // Clear the text box before displaying new data.

            // Check if any route details exist.
            if (routeDetails.Count == 0)
            {
                textBox.AppendText("No route details available.\n\n");
                textBox.AppendText("Load a route and use the 'Get Google Routes' button to see detailed timing information.");
                return;
            }

            // Iterate through the route details and display them.
            foreach (var detail in routeDetails.Values.OrderBy(d => d.VehicleId))
            {
                DisplaySingleRouteDetails(textBox, detail, solution, dbService);
                textBox.AppendText("--------------------------------\n\n");
            }
        }

        /// <summary>
        /// Displays route details for a single vehicle.
        /// </summary>
        /// <param name="textBox">The RichTextBox control where the route details will be displayed.</param>
        /// <param name="detail">The route details for the specific vehicle.</param>
        /// <param name="solution">The solution object containing the vehicles and other related data.</param>
        /// <param name="dbService">The service used to interact with the database for additional data, such as destination information.</param>
        private static void DisplaySingleRouteDetails(
            RichTextBox textBox,
            RouteDetails detail,
            Solution solution,
            DatabaseService dbService)
        {
            // Get the vehicle info from the solution.
            var vehicle = solution.Vehicles.FirstOrDefault(v => v.Id == detail.VehicleId);

            // Determine the start location of the vehicle.
            string startLocation = vehicle != null && !string.IsNullOrEmpty(vehicle.StartAddress)
                ? vehicle.StartAddress
                : $"({vehicle?.StartLatitude ?? 0:F4}, {vehicle?.StartLongitude ?? 0:F4})";

            FormatBold(textBox, $"Vehicle {detail.VehicleId}\n");
            textBox.AppendText($"Start Location: {startLocation}\n");
            textBox.AppendText($"Driver: {vehicle?.DriverName ?? "Unknown"}\n");
            textBox.AppendText($"Total Distance: {detail.TotalDistance:F2} km\n");
            textBox.AppendText($"Total Time: {TimeFormatter.FormatMinutesWithUnits(detail.TotalTime)}\n");

            // Add departure time if available.
            if (!string.IsNullOrEmpty(vehicle?.DepartureTime))
            {
                FormatBold(textBox, $"Departure Time: {vehicle.DepartureTime}\n");
            }

            textBox.AppendText("\n");
            FormatBold(textBox, "Stop Details:\n");

            // Display stop details for the route.
            DisplayStopDetails(textBox, detail, vehicle, dbService);
        }

        /// <summary>
        /// Displays details for each stop in the route, including passenger stops and destination stops.
        /// </summary>
        /// <param name="textBox">The RichTextBox control where the stop details will be displayed.</param>
        /// <param name="detail">The route details for the specific vehicle.</param>
        /// <param name="vehicle">The vehicle associated with the route details.</param>
        /// <param name="dbService">The service used to interact with the database for additional data, such as destination information.</param>
        private static void DisplayStopDetails(
            RichTextBox textBox,
            RouteDetails detail,
            Vehicle vehicle,
            DatabaseService dbService)
        {
            int stopNumber = 1;
            foreach (var stop in detail.StopDetails)
            {
                // Check if the stop is a passenger stop or a destination stop.
                if (stop.PassengerId >= 0)
                {
                    DisplayPassengerStop(textBox, stop, vehicle, stopNumber);
                }
                else
                {
                    DisplayDestinationStop(textBox, stop, dbService, stopNumber);
                }

                // Display statistics for this stop.
                textBox.AppendText($"   Distance: {stop.DistanceFromPrevious:F2} km\n");
                textBox.AppendText($"   Time: {TimeFormatter.FormatMinutesWithUnits(stop.TimeFromPrevious)}\n");
                textBox.AppendText($"   Cumulative: {stop.CumulativeDistance:F2} km, {TimeFormatter.FormatMinutes(stop.CumulativeTime)} min\n\n");
                stopNumber++;
            }
        }

        /// <summary>
        /// Displays the details of a passenger stop.
        /// </summary>
        /// <param name="textBox">The RichTextBox control where the passenger stop details will be displayed.</param>
        /// <param name="stop">The stop details for the passenger.</param>
        /// <param name="vehicle">The vehicle associated with the route details.</param>
        /// <param name="stopNumber">The number of the current stop in the route.</param>
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

            // Display the estimated pickup time if available.
            if (passenger != null && !string.IsNullOrEmpty(passenger.EstimatedPickupTime))
            {
                FormatBold(textBox, $"   Pickup Time: {passenger.EstimatedPickupTime}\n");
            }
        }

        /// <summary>
        /// Displays the details of a destination stop.
        /// </summary>
        /// <param name="textBox">The RichTextBox control where the destination stop details will be displayed.</param>
        /// <param name="stop">The stop details for the destination.</param>
        /// <param name="dbService">The service used to interact with the database for additional data, such as destination information.</param>
        /// <param name="stopNumber">The number of the current stop in the route.</param>
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
                // Attempt to get the destination information from the database.
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

        /// <summary>
        /// Formats the header text with bold style and appends it to the RichTextBox.
        /// </summary>
        /// <param name="textBox">The RichTextBox control where the header will be displayed.</param>
        /// <param name="text">The header text.</param>
        private static void FormatHeader(RichTextBox textBox, string text)
        {
            textBox.SelectionFont = new Font(textBox.Font, FontStyle.Bold); // Set font to bold.
            textBox.AppendText(text + "\n\n");
            textBox.SelectionFont = textBox.Font; // Reset font to normal.
        }

        /// <summary>
        /// Formats the given text with bold style and appends it to the RichTextBox.
        /// </summary>
        /// <param name="textBox">The RichTextBox control where the bold text will be displayed.</param>
        /// <param name="text">The text to be formatted in bold.</param>
        private static void FormatBold(RichTextBox textBox, string text)
        {
            textBox.SelectionFont = new Font(textBox.Font, FontStyle.Bold); // Set font to bold.
            textBox.AppendText(text);
            textBox.SelectionFont = textBox.Font; // Reset font to normal.
        }
    }
}
