using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using claudpro.Services;

namespace claudpro
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Database path could be configurable
            // Add this code to Program.cs or before the database is accessed
            string dbPath = "ridematch.db";
            // Delete existing file if it exists
            if (System.IO.File.Exists(dbPath))
            {
                System.IO.File.Delete(dbPath);
            }
            // Create new database
            using (var dbService = new DatabaseService(dbPath))
            {
                // The database will be created with default data through the constructor
                Console.WriteLine("Database created successfully at: " + dbPath);
            }
            // Create database service
            using (var dbService = new DatabaseService(dbPath))
            {
                // Show login form
                using (var loginForm = new LoginForm(dbService))
                {
                    if (loginForm.ShowDialog() == DialogResult.OK)
                    {
                        // Create map service (this could be moved to a config file)
                        const string API_KEY = "AIzaSyA8gY0PbmE1EgDjxd-SdIMWWTaQf9Mi7vc";
                        var mapService = new MapService(API_KEY);

                        // Show appropriate form based on user type
                        Form mainForm = null;

                        switch (loginForm.UserType.ToLower())
                        {
                            case "admin":
                                // For admin, show the admin form
                                mainForm = new AdminForm(dbService, mapService);
                                break;

                            case "driver":
                                // For driver, show the driver-specific form
                                mainForm = new DriverForm(dbService, mapService, loginForm.UserId, loginForm.Username);
                                break;

                            case "passenger":
                                // For passenger, show the passenger-specific form
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

                        // Run the appropriate form
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