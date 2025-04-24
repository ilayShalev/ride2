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
        private readonly DatabaseService _dbService;
        private TextBox _usernameTextBox;
        private TextBox _passwordTextBox;
        private Button _loginButton;
        private LinkLabel _registerLinkLabel;
        private Label _statusLabel;

        public int UserId { get; private set; }
        public string UserType { get; private set; }
        public string Username { get; private set; }

        public LoginForm(DatabaseService dbService)
        {
            _dbService = dbService;
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            AddTitleLabel();
            AddUsernameField();
            AddPasswordField();
            AddLoginButton();
            AddRegistrationLink();
            AddStatusLabel();

            // Set enter key to trigger login
            AcceptButton = _loginButton;
        }

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

        private void AddUsernameField()
        {
            Controls.Add(LoginControlFactory.CreateLabel("Username:", new Point(50, 80), new Size(100, 20)));
            _usernameTextBox = LoginControlFactory.CreateTextBox(new Point(150, 80), new Size(200, 20));
            Controls.Add(_usernameTextBox);
        }

        private void AddPasswordField()
        {
            Controls.Add(LoginControlFactory.CreateLabel("Password:", new Point(50, 110), new Size(100, 20)));
            _passwordTextBox = LoginControlFactory.CreateTextBox(new Point(150, 110), new Size(200, 20));
            _passwordTextBox.PasswordChar = '*';
            Controls.Add(_passwordTextBox);
        }

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

        private async Task LoginAsync()
        {
            _loginButton.Enabled = false;
            _statusLabel.Text = "Logging in...";

            try
            {
                if (AreCredentialsEmpty())
                {
                    _statusLabel.Text = "Please enter both username and password.";
                    return;
                }

                await AuthenticateUser();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
            }
            finally
            {
                _loginButton.Enabled = true;
            }
        }

        private bool AreCredentialsEmpty()
        {
            return string.IsNullOrWhiteSpace(_usernameTextBox.Text) ||
                   string.IsNullOrWhiteSpace(_passwordTextBox.Text);
        }

        private async Task AuthenticateUser()
        {
            var result = await _dbService.AuthenticateUserAsync(_usernameTextBox.Text, _passwordTextBox.Text);

            if (result.Success)
            {
                UserId = result.UserId;
                UserType = result.UserType;
                Username = _usernameTextBox.Text;
                CloseWithSuccess();
            }
            else
            {
                _statusLabel.Text = "Invalid username or password.";
            }
        }

        private void CloseWithSuccess()
        {
            DialogResult = DialogResult.OK;
            Close();
        }

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

        private void LoginForm_Load(object sender, EventArgs e)
        {
            // Initialization code for LoginForm
        }
    }

 

  

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