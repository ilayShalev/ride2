using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using claudpro.Services;
using claudpro.UI;

namespace claudpro
{
    public partial class LoginForm : Form
    {
        private readonly DatabaseService dbService;
        private TextBox usernameTextBox;
        private TextBox passwordTextBox;
        private Button loginButton;
        private LinkLabel registerLinkLabel;
        private Label statusLabel;

        public int UserId { get; private set; }
        public string UserType { get; private set; }
        public string Username { get; private set; }

        public LoginForm(DatabaseService dbService)
        {
            this.dbService = dbService;
            InitializeComponent();
            SetupUI();
        }

      

        private void SetupUI()
        {
            // Add logo or app name
            var titleLabel = ControlExtensions.CreateLabel(
                "RideMatch System",
                new Point(50, 20),
                new Size(300, 40),
                new Font("Arial", 20, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            );
            Controls.Add(titleLabel);

            // Username field
            Controls.Add(ControlExtensions.CreateLabel("Username:", new Point(50, 80), new Size(100, 20)));
            usernameTextBox = ControlExtensions.CreateTextBox(new Point(150, 80), new Size(200, 20));
            Controls.Add(usernameTextBox);

            // Password field
            Controls.Add(ControlExtensions.CreateLabel("Password:", new Point(50, 110), new Size(100, 20)));
            passwordTextBox = ControlExtensions.CreateTextBox(new Point(150, 110), new Size(200, 20));
            passwordTextBox.PasswordChar = '*';
            Controls.Add(passwordTextBox);

            // Login button
            loginButton = ControlExtensions.CreateButton(
                "Login",
                new Point(150, 150),
                new Size(100, 30),
                async (s, e) => await LoginAsync()
            );
            Controls.Add(loginButton);

            // Registration link
            registerLinkLabel = new LinkLabel
            {
                Text = "New User? Register here",
                Location = new Point(130, 190),
                Size = new Size(150, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            registerLinkLabel.LinkClicked += (s, e) => ShowRegistrationForm();
            Controls.Add(registerLinkLabel);

            // Status label for messages
            statusLabel = ControlExtensions.CreateLabel(
                "",
                new Point(50, 220),
                new Size(300, 20),
                null,
                ContentAlignment.MiddleCenter
            );
            statusLabel.ForeColor = Color.Red;
            Controls.Add(statusLabel);

            // Set enter key to trigger login
            AcceptButton = loginButton;
        }

        private async Task LoginAsync()
        {
            loginButton.Enabled = false;
            statusLabel.Text = "Logging in...";

            try
            {
                if (string.IsNullOrWhiteSpace(usernameTextBox.Text) || string.IsNullOrWhiteSpace(passwordTextBox.Text))
                {
                    statusLabel.Text = "Please enter both username and password.";
                    return;
                }

                var result = await dbService.AuthenticateUserAsync(usernameTextBox.Text, passwordTextBox.Text);

                if (result.Success)
                {
                    UserId = result.UserId;
                    UserType = result.UserType;
                    Username = usernameTextBox.Text;

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    statusLabel.Text = "Invalid username or password.";
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error: {ex.Message}";
            }
            finally
            {
                loginButton.Enabled = true;
            }
        }

        private void ShowRegistrationForm()
        {
            using (var regForm = new RegistrationForm(dbService))
            {
                if (regForm.ShowDialog() == DialogResult.OK)
                {
                    usernameTextBox.Text = regForm.Username;
                    statusLabel.Text = "Registration successful! You can now login.";
                }
            }
        }
        private void LoginForm_Load(object sender, EventArgs e)
        {
            // Initialization code for LoginForm
        }
    }

    public class RegistrationForm : Form
    {
        private readonly DatabaseService dbService;
        private TextBox usernameTextBox;
        private TextBox passwordTextBox;
        private TextBox confirmPasswordTextBox;
        private TextBox nameTextBox;
        private TextBox emailTextBox;
        private TextBox phoneTextBox;
        private ComboBox userTypeComboBox;
        private Button registerButton;
        private Button cancelButton;
        private Label statusLabel;

        public string Username { get; private set; }

        public RegistrationForm(DatabaseService dbService)
        {
            this.dbService = dbService;
            InitializeComponent();
            SetupUI();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "RideMatch - Registration";
            this.Size = new Size(450, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            this.ResumeLayout(false);
        }

        private void SetupUI()
        {
            // Title
            var titleLabel = ControlExtensions.CreateLabel(
                "New User Registration",
                new Point(50, 20),
                new Size(350, 30),
                new Font("Arial", 16, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            );
            Controls.Add(titleLabel);

            int y = 70;
            int labelWidth = 120;
            int inputWidth = 230;
            int spacing = 30;

            // Username
            Controls.Add(ControlExtensions.CreateLabel("Username:", new Point(50, y), new Size(labelWidth, 20)));
            usernameTextBox = ControlExtensions.CreateTextBox(new Point(170, y), new Size(inputWidth, 20));
            Controls.Add(usernameTextBox);
            y += spacing;

            // Password
            Controls.Add(ControlExtensions.CreateLabel("Password:", new Point(50, y), new Size(labelWidth, 20)));
            passwordTextBox = ControlExtensions.CreateTextBox(new Point(170, y), new Size(inputWidth, 20));
            passwordTextBox.PasswordChar = '*';
            Controls.Add(passwordTextBox);
            y += spacing;

            // Confirm Password
            Controls.Add(ControlExtensions.CreateLabel("Confirm Password:", new Point(50, y), new Size(labelWidth, 20)));
            confirmPasswordTextBox = ControlExtensions.CreateTextBox(new Point(170, y), new Size(inputWidth, 20));
            confirmPasswordTextBox.PasswordChar = '*';
            Controls.Add(confirmPasswordTextBox);
            y += spacing;

            // Name
            Controls.Add(ControlExtensions.CreateLabel("Name:", new Point(50, y), new Size(labelWidth, 20)));
            nameTextBox = ControlExtensions.CreateTextBox(new Point(170, y), new Size(inputWidth, 20));
            Controls.Add(nameTextBox);
            y += spacing;

            // Email
            Controls.Add(ControlExtensions.CreateLabel("Email:", new Point(50, y), new Size(labelWidth, 20)));
            emailTextBox = ControlExtensions.CreateTextBox(new Point(170, y), new Size(inputWidth, 20));
            Controls.Add(emailTextBox);
            y += spacing;

            // Phone
            Controls.Add(ControlExtensions.CreateLabel("Phone:", new Point(50, y), new Size(labelWidth, 20)));
            phoneTextBox = ControlExtensions.CreateTextBox(new Point(170, y), new Size(inputWidth, 20));
            Controls.Add(phoneTextBox);
            y += spacing;

            // User Type
            Controls.Add(ControlExtensions.CreateLabel("User Type:", new Point(50, y), new Size(labelWidth, 20)));
            userTypeComboBox = ControlExtensions.CreateComboBox(
                new Point(170, y),
                new Size(inputWidth, 20),
                new string[] { "Passenger", "Driver", "Admin" },
                0
            );
            Controls.Add(userTypeComboBox);
            y += spacing + 10;

            // Buttons
            registerButton = ControlExtensions.CreateButton(
                "Register",
                new Point(170, y),
                new Size(100, 30),
                async (s, e) => await RegisterAsync()
            );
            Controls.Add(registerButton);

            cancelButton = ControlExtensions.CreateButton(
                "Cancel",
                new Point(280, y),
                new Size(100, 30),
                (s, e) => Close()
            );
            Controls.Add(cancelButton);
            y += spacing;

            // Status label
            statusLabel = ControlExtensions.CreateLabel(
                "",
                new Point(50, y),
                new Size(350, 20),
                null,
                ContentAlignment.MiddleCenter
            );
            statusLabel.ForeColor = Color.Red;
            Controls.Add(statusLabel);

            // Set enter key to trigger register
            AcceptButton = registerButton;
        }

        private async Task RegisterAsync()
        {
            if (ValidateInput())
            {
                registerButton.Enabled = false;
                statusLabel.Text = "Registering...";

                try
                {
                    // Convert user type to database format
                    string userType = userTypeComboBox.SelectedItem.ToString();

                    // Add user to database
                    int userId = await dbService.AddUserAsync(
                        usernameTextBox.Text,
                        passwordTextBox.Text,
                        userType,
                        nameTextBox.Text,
                        emailTextBox.Text,
                        phoneTextBox.Text
                    );

                    if (userId > 0)
                    {
                        Username = usernameTextBox.Text;
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                    else
                    {
                        statusLabel.Text = "Registration failed. Please try again.";
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("UNIQUE"))
                    {
                        statusLabel.Text = "Username already exists. Please choose a different one.";
                    }
                    else
                    {
                        statusLabel.Text = $"Error: {ex.Message}";
                    }
                }
                finally
                {
                    registerButton.Enabled = true;
                }
            }
        }

        private bool ValidateInput()
        {
            // Check if all required fields are filled
            if (string.IsNullOrWhiteSpace(usernameTextBox.Text) ||
                string.IsNullOrWhiteSpace(passwordTextBox.Text) ||
                string.IsNullOrWhiteSpace(confirmPasswordTextBox.Text) ||
                string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                statusLabel.Text = "Please fill in all required fields.";
                return false;
            }

            // Check if passwords match
            if (passwordTextBox.Text != confirmPasswordTextBox.Text)
            {
                statusLabel.Text = "Passwords do not match.";
                return false;
            }

            // Check password length
            if (passwordTextBox.Text.Length < 6)
            {
                statusLabel.Text = "Password must be at least 6 characters long.";
                return false;
            }

            return true;
        }
    }

}