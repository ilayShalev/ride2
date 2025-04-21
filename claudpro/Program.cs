using System;
using System.IO;
using System.Configuration;
using System.Windows.Forms;
using RideMatchProject.Services;

namespace RideMatchProject
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Get database path from configuration
            string dbPath = ConfigurationManager.AppSettings["DatabasePath"] ?? "ridematch.db";

            // Make sure it's an absolute path
            if (!Path.IsPathRooted(dbPath))
            {
                // Store in application folder by default
                dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);
            }

            // Ensure directory exists
            string dbDirectory = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(dbDirectory) && !string.IsNullOrEmpty(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }

            // For development, recreate the database
            bool isDevMode = false;  // Set to true to recreate DB during development
            if (isDevMode && File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                    Console.WriteLine("Development mode: Deleted existing database");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not delete existing database: {ex.Message}",
                        "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            // Create database service
            using (var dbService = new DatabaseService(dbPath))
            {
                // Show login form
                using (var loginForm = new LoginForm(dbService))
                {
                    if (loginForm.ShowDialog() == DialogResult.OK)
                    {
                        // Get API key from configuration
                        string apiKey = ConfigurationManager.AppSettings["GoogleApiKey"];
                        var mapService = new MapService(apiKey);

                        // Show appropriate form based on user type
                        Form mainForm = null;

                        switch (loginForm.UserType.ToLower())
                        {
                            case "admin":
                                mainForm = new AdminForm(dbService, mapService);
                                break;

                            case "driver":
                                mainForm = new DriverForm(dbService, mapService, loginForm.UserId, loginForm.Username);
                                break;

                            case "passenger":
                                mainForm = new PassengerForm(dbService, mapService, loginForm.UserId, loginForm.Username);
                                break;

                            default:
                                MessageBox.Show(
                                    $"Unknown user type: {loginForm.UserType}",
                                    "Error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error
                                );
                                return;
                        }

                        if (mainForm != null)
                        {
                            Application.Run(mainForm);
                        }
                    }
                }
            }
        }
    }
}