using RideMatchProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.RoutingServiceClasses
{
    /// <summary>
    /// Validates solutions against constraints
    /// </summary>
    public class SolutionValidator
    {
        public string ValidateSolution(Solution solution, List<Passenger> allPassengers)
        {
            if (solution == null)
            {
                return "No solution to validate.";
            }

            var validation = PerformValidation(solution, allPassengers);
            var statistics = CalculateStatistics(solution, validation.AssignedPassengers.Count,
                allPassengers.Count);

            var report = GenerateReport(validation, statistics);
            return report;
        }

        private (HashSet<int> AssignedPassengers, bool CapacityExceeded,
            List<int> PassengersWithMultipleAssignments) PerformValidation(
                Solution solution, List<Passenger> allPassengers)
        {
            var assignedPassengers = new HashSet<int>();
            var capacityExceeded = false;
            var passengersWithMultipleAssignments = new List<int>();

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers.Count > vehicle.Capacity)
                {
                    capacityExceeded = true;
                }

                CheckPassengerAssignments(vehicle, assignedPassengers, passengersWithMultipleAssignments);
            }

            return (assignedPassengers, capacityExceeded, passengersWithMultipleAssignments);
        }

        private void CheckPassengerAssignments(Vehicle vehicle, HashSet<int> assignedPassengers,
            List<int> passengersWithMultipleAssignments)
        {
            foreach (var passenger in vehicle.AssignedPassengers)
            {
                if (assignedPassengers.Contains(passenger.Id))
                {
                    passengersWithMultipleAssignments.Add(passenger.Id);
                }
                else
                {
                    assignedPassengers.Add(passenger.Id);
                }
            }
        }

        private (double TotalDistance, double TotalTime, double AverageTime, int UsedVehicles)
            CalculateStatistics(Solution solution, int assignedCount, int totalPassengers)
        {
            double totalDistance = solution.Vehicles.Sum(v => v.TotalDistance);
            double totalTime = solution.Vehicles.Sum(v => v.TotalTime);
            int usedVehicles = solution.Vehicles.Count(v => v.AssignedPassengers.Count > 0);
            double averageTime = usedVehicles > 0 ? totalTime / usedVehicles : 0;

            return (totalDistance, totalTime, averageTime, usedVehicles);
        }

        private string GenerateReport(
            (HashSet<int> AssignedPassengers, bool CapacityExceeded,
            List<int> PassengersWithMultipleAssignments) validation,
            (double TotalDistance, double TotalTime, double AverageTime, int UsedVehicles) statistics)
        {
            StringBuilder report = new StringBuilder();

            bool allAssigned = validation.AssignedPassengers.Count == validation.AssignedPassengers.Count;

            report.AppendLine("Validation Results:");
            report.AppendLine($"All passengers assigned: {allAssigned}");
            report.AppendLine($"Assigned passengers: {validation.AssignedPassengers.Count}/" +
                $"{validation.AssignedPassengers.Count}");
            report.AppendLine($"Capacity exceeded: {validation.CapacityExceeded}");

            AppendMultipleAssignmentsInfo(report, validation.PassengersWithMultipleAssignments);
            AppendStatisticsInfo(report, statistics);

            return report.ToString();
        }

        private void AppendMultipleAssignmentsInfo(StringBuilder report,
            List<int> passengersWithMultipleAssignments)
        {
            if (passengersWithMultipleAssignments.Count > 0)
            {
                report.AppendLine($"Passengers with multiple assignments: " +
                    $"{passengersWithMultipleAssignments.Count}");
                report.AppendLine($"IDs: {string.Join(", ", passengersWithMultipleAssignments)}");
            }
        }

        private void AppendStatisticsInfo(StringBuilder report,
            (double TotalDistance, double TotalTime, double AverageTime, int UsedVehicles) statistics)
        {
            report.AppendLine();
            report.AppendLine("Statistics:");
            report.AppendLine($"Total distance: {statistics.TotalDistance:F2} km");
            report.AppendLine($"Total time: {statistics.TotalTime:F2} minutes");
            report.AppendLine($"Average time per vehicle: {statistics.AverageTime:F2} minutes");
        }
    }
}
