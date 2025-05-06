using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using RideMatchProject.Services;
using RideMatchProject.UI;
using RideMatchProject.LoginClasses;

namespace RideMatchProject
{
    public partial class LoginForm : Form
    {
        // Dependency to handle user authentication and database interaction
        private readonly DatabaseService _dbService;

        // UI controls
        private TextBox _usernameTextBox;
        private TextBox _passwordTextBox;
        private Button _loginButton;
        private LinkLabel _registerLinkLabel;
        private Label _statusLabel;

        // Properties to hold authenticated user information
        public int UserId { get; private set; }
        public string UserType { get; private set; }
        public string Username { get; private set; }

        // Constructor
        public LoginForm(DatabaseService dbService)
        {
            _dbService = dbService;
            InitializeComponent();
            SetupUI(); // Setup all visual components
        }

        // Creates all visual components of the login form
        private void SetupUI()
        {
            AddTitleLabel();
            AddUsernameField();
            AddPasswordField();
            AddLoginButton();
            AddRegistrationLink();
            AddStatusLabel();

            // Allows pressing Enter to trigger the login button
            AcceptButton = _loginButton;
        }

        // Adds the form title label
        private void AddTitleLabel()
        {
            var titleLabel = LoginControlFactory.CreateLabel(
                "RideMatch System",
                new Point(50, 20),
                new Size(300, 40),
                new Font("Arial", 20, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            );
            Controls.Add(titleLabel);
        }

        // Adds the username field and label
        private void AddUsernameField()
        {
            Controls.Add(LoginControlFactory.CreateLabel("Username:", new Point(50, 80), new Size(100, 20)));
            _usernameTextBox = LoginControlFactory.CreateTextBox(new Point(150, 80), new Size(200, 20));
            Controls.Add(_usernameTextBox);
        }

        // Adds the password field and label
        private void AddPasswordField()
        {
            Controls.Add(LoginControlFactory.CreateLabel("Password:", new Point(50, 110), new Size(100, 20)));
            _passwordTextBox = LoginControlFactory.CreateTextBox(new Point(150, 110), new Size(200, 20));
            _passwordTextBox.PasswordChar = '*'; // Hide characters
            Controls.Add(_passwordTextBox);
        }

        // Adds the login button and sets the login action
        private void AddLoginButton()
        {
            _loginButton = LoginControlFactory.CreateButton(
                "Login",
                new Point(150, 150),
                new Size(100, 30),
                async (s, e) => await LoginAsync()
            );
            Controls.Add(_loginButton);
        }

        // Adds a link to open the registration form
        private void AddRegistrationLink()
        {
            _registerLinkLabel = new LinkLabel
            {
                Text = "New User? Register here",
                Location = new Point(130, 190),
                Size = new Size(150, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _registerLinkLabel.LinkClicked += (s, e) => ShowRegistrationForm();
            Controls.Add(_registerLinkLabel);
        }

        // Adds a status label for displaying errors or status messages
        private void AddStatusLabel()
        {
            _statusLabel = LoginControlFactory.CreateLabel(
                "",
                new Point(50, 220),
                new Size(300, 20),
                null,
                ContentAlignment.MiddleCenter
            );
            _statusLabel.ForeColor = Color.Red;
            Controls.Add(_statusLabel);
        }

        // Handles the login process
        private async Task LoginAsync()
        {
            _loginButton.Enabled = false; // Disable to prevent multiple clicks
            _statusLabel.Text = "Logging in...";

            try
            {
                // Check if username or password is missing
                if (AreCredentialsEmpty())
                {
                    _statusLabel.Text = "Please enter both username and password.";
                    return;
                }

                // Proceed with authentication
                await AuthenticateUser();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
            }
            finally
            {
                _loginButton.Enabled = true; // Re-enable the button
            }
        }

        // Checks if either username or password fields are empty
        private bool AreCredentialsEmpty()
        {
            return string.IsNullOrWhiteSpace(_usernameTextBox.Text) ||
                   string.IsNullOrWhiteSpace(_passwordTextBox.Text);
        }

        // Authenticates the user using the database service
        private async Task AuthenticateUser()
        {
            var result = await _dbService.AuthenticateUserAsync(_usernameTextBox.Text, _passwordTextBox.Text);

            if (result.Success)
            {
                // Save user info for later use
                UserId = result.UserId;
                UserType = result.UserType;
                Username = _usernameTextBox.Text;
                CloseWithSuccess(); // Close form on success
            }
            else
            {
                _statusLabel.Text = "Invalid username or password.";
            }
        }

        // Closes the login form with OK result
        private void CloseWithSuccess()
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        // Opens the registration form and pre-fills username after successful registration
        private void ShowRegistrationForm()
        {
            using (var regForm = new RegistrationForm(_dbService))
            {
                if (regForm.ShowDialog() == DialogResult.OK)
                {
                    _usernameTextBox.Text = regForm.Username;
                    _statusLabel.Text = "Registration successful! You can now login.";
                }
            }
        }

        // Optional form load event handler (currently unused)
        private void LoginForm_Load(object sender, EventArgs e)
        {
            // Initialization code for LoginForm
        }
    }

    // Enum to identify different field types (used in registration form)
    public enum FieldType
    {
        Username,
        Password,
        ConfirmPassword,
        Name,
        Email,
        Phone
    }
}
