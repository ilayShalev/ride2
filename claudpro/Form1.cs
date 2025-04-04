using System;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;

namespace claudpro
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            ConfigureAsSplashScreen();
        }

        private void ConfigureAsSplashScreen()
        {
            // Configure form properties for splash screen
            this.Text = "RideMatch System";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(500, 300);
            this.BackColor = Color.White;

            // Add logo or title
            var titleLabel = new Label
            {
                Text = "RideMatch System",
                Font = new Font("Arial", 24, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(0, 120, 215),
                Size = new Size(500, 50),
                Location = new Point(0, 50)
            };
            this.Controls.Add(titleLabel);

            // Add version info
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var versionLabel = new Label
            {
                Text = $"Version {version.Major}.{version.Minor}.{version.Build}",
                Font = new Font("Arial", 10),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(500, 20),
                Location = new Point(0, 120)
            };
            this.Controls.Add(versionLabel);

            // Add copyright info
            var copyrightLabel = new Label
            {
                Text = $"© {DateTime.Now.Year} RideMatch",
                Font = new Font("Arial", 8),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(500, 20),
                Location = new Point(0, 250)
            };
            this.Controls.Add(copyrightLabel);

            // Auto-close after a few seconds
            var timer = new Timer
            {
                Interval = 3000
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                this.Close();
            };
            timer.Start();
        }
    }
}