using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using RideMatchProject.Services;
using RideMatchProject.UI;

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
            var titleLabel = ControlFactory.CreateLabel(
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
            Controls.Add(ControlFactory.CreateLabel("Username:", new Point(50, 80), new Size(100, 20)));
            _usernameTextBox = ControlFactory.CreateTextBox(new Point(150, 80), new Size(200, 20));
            Controls.Add(_usernameTextBox);
        }

        private void AddPasswordField()
        {
            Controls.Add(ControlFactory.CreateLabel("Password:", new Point(50, 110), new Size(100, 20)));
            _passwordTextBox = ControlFactory.CreateTextBox(new Point(150, 110), new Size(200, 20));
            _passwordTextBox.PasswordChar = '*';
            Controls.Add(_passwordTextBox);
        }

        private void AddLoginButton()
        {
            _loginButton = ControlFactory.CreateButton(
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
            _statusLabel = ControlFactory.CreateLabel(
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

    public class RegistrationForm : Form
    {
        private readonly DatabaseService _dbService;
        private FormInputCollection _inputs;
        private Button _registerButton;
        private Button _cancelButton;
        private Label _statusLabel;

        public string Username { get; private set; }

        public RegistrationForm(DatabaseService dbService)
        {
            _dbService = dbService;
            InitializeComponent();
            _inputs = new FormInputCollection();
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
            AddTitleLabel();
            CreateInputFields();
            AddButtons();
            AddStatusLabel();

            // Set enter key to trigger register
            AcceptButton = _registerButton;
        }

        private void AddTitleLabel()
        {
            var titleLabel = ControlFactory.CreateLabel(
                "New User Registration",
                new Point(50, 20),
                new Size(350, 30),
                new Font("Arial", 16, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            );
            Controls.Add(titleLabel);
        }

        private void CreateInputFields()
        {
            int y = 70;
            int labelWidth = 120;
            int inputWidth = 230;
            int spacing = 30;

            // Create input fields
            AddInputField("Username:", ref y, labelWidth, inputWidth, spacing, FieldType.Username);
            AddInputField("Password:", ref y, labelWidth, inputWidth, spacing, FieldType.Password);
            AddInputField("Confirm Password:", ref y, labelWidth, inputWidth, spacing, FieldType.ConfirmPassword);
            AddInputField("Name:", ref y, labelWidth, inputWidth, spacing, FieldType.Name);
            AddInputField("Email:", ref y, labelWidth, inputWidth, spacing, FieldType.Email);
            AddInputField("Phone:", ref y, labelWidth, inputWidth, spacing, FieldType.Phone);

            // User Type dropdown
            AddUserTypeComboBox(y, labelWidth, inputWidth);
        }

        private void AddInputField(string label, ref int y, int labelWidth, int inputWidth, int spacing, FieldType fieldType)
        {
            Controls.Add(ControlFactory.CreateLabel(label, new Point(50, y), new Size(labelWidth, 20)));

            TextBox textBox = ControlFactory.CreateTextBox(new Point(170, y), new Size(inputWidth, 20));
            if (fieldType == FieldType.Password || fieldType == FieldType.ConfirmPassword)
            {
                textBox.PasswordChar = '*';
            }

            Controls.Add(textBox);
            _inputs.AddField(fieldType, textBox);

            y += spacing;
        }

        private void AddUserTypeComboBox(int y, int labelWidth, int inputWidth)
        {
            Controls.Add(ControlFactory.CreateLabel("User Type:", new Point(50, y), new Size(labelWidth, 20)));

            ComboBox userTypeComboBox = ControlFactory.CreateComboBox(
                new Point(170, y),
                new Size(inputWidth, 20),
                new string[] { "Passenger", "Driver"},
                0
            );

            Controls.Add(userTypeComboBox);
            _inputs.UserTypeComboBox = userTypeComboBox;
        }

        private void AddButtons()
        {
            int y = 280;

            // Register button
            _registerButton = ControlFactory.CreateButton(
                "Register",
                new Point(170, y),
                new Size(100, 30),
                async (s, e) => await RegisterAsync()
            );
            Controls.Add(_registerButton);

            // Cancel button
            _cancelButton = ControlFactory.CreateButton(
                "Cancel",
                new Point(280, y),
                new Size(100, 30),
                (s, e) => Close()
            );
            Controls.Add(_cancelButton);
        }

        private void AddStatusLabel()
        {
            _statusLabel = ControlFactory.CreateLabel(
                "",
                new Point(50, 320),
                new Size(350, 20),
                null,
                ContentAlignment.MiddleCenter
            );
            _statusLabel.ForeColor = Color.Red;
            Controls.Add(_statusLabel);
        }

        private async Task RegisterAsync()
        {
            ValidationResult validationResult = ValidateInput();

            if (!validationResult.IsValid)
            {
                _statusLabel.Text = validationResult.ErrorMessage;
                return;
            }

            await SubmitRegistration();
        }

        private ValidationResult ValidateInput()
        {
            // Check if all required fields are filled
            if (_inputs.HasEmptyRequiredFields())
            {
                return new ValidationResult(false, "Please fill in all required fields.");
            }

            // Check if passwords match
            if (!_inputs.PasswordsMatch())
            {
                return new ValidationResult(false, "Passwords do not match.");
            }

            // Check password length
            if (_inputs.GetPasswordLength() < 6)
            {
                return new ValidationResult(false, "Password must be at least 6 characters long.");
            }

            return new ValidationResult(true, string.Empty);
        }

        private async Task SubmitRegistration()
        {
            _registerButton.Enabled = false;
            _statusLabel.Text = "Registering...";

            try
            {
                // Convert user type to database format
                string userType = _inputs.GetSelectedUserType();

                // Add user to database
                int userId = await _dbService.AddUserAsync(
                    _inputs.GetUsername(),
                    _inputs.GetPassword(),
                    userType,
                    _inputs.GetName(),
                    _inputs.GetEmail(),
                    _inputs.GetPhone()
                );

                HandleRegistrationResult(userId);
            }
            catch (Exception ex)
            {
                HandleRegistrationError(ex);
            }
            finally
            {
                _registerButton.Enabled = true;
            }
        }

        private void HandleRegistrationResult(int userId)
        {
            if (userId > 0)
            {
                Username = _inputs.GetUsername();
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _statusLabel.Text = "Registration failed. Please try again.";
            }
        }

        private void HandleRegistrationError(Exception ex)
        {
            if (ex.Message.Contains("UNIQUE"))
            {
                _statusLabel.Text = "Username already exists. Please choose a different one.";
            }
            else
            {
                _statusLabel.Text = $"Error: {ex.Message}";
            }
        }
    }

    public static class ControlFactory
    {
        public static Label CreateLabel(
            string text,
            Point location,
            Size size,
            Font font = null,
            ContentAlignment textAlign = ContentAlignment.MiddleLeft)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                Size = size,
                TextAlign = textAlign
            };

            if (font != null)
            {
                label.Font = font;
            }

            return label;
        }

        public static TextBox CreateTextBox(Point location, Size size)
        {
            return new TextBox
            {
                Location = location,
                Size = size
            };
        }

        public static Button CreateButton(
            string text,
            Point location,
            Size size,
            EventHandler clickHandler)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = size
            };

            if (clickHandler != null)
            {
                button.Click += clickHandler;
            }

            return button;
        }

        public static ComboBox CreateComboBox(
            Point location,
            Size size,
            string[] items,
            int selectedIndex)
        {
            var comboBox = new ComboBox
            {
                Location = location,
                Size = size,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            if (items != null)
            {
                comboBox.Items.AddRange(items);
            }

            if (selectedIndex >= 0 && selectedIndex < comboBox.Items.Count)
            {
                comboBox.SelectedIndex = selectedIndex;
            }

            return comboBox;
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

    public class FormInputCollection
    {
        private readonly Dictionary<FieldType, TextBox> _fields;
        public ComboBox UserTypeComboBox { get; set; }

        public FormInputCollection()
        {
            _fields = new Dictionary<FieldType, TextBox>();
        }

        public void AddField(FieldType type, TextBox textBox)
        {
            _fields[type] = textBox;
        }

        public bool HasEmptyRequiredFields()
        {
            FieldType[] requiredFields = { FieldType.Username, FieldType.Password, FieldType.ConfirmPassword, FieldType.Name };

            foreach (FieldType field in requiredFields)
            {
                if (string.IsNullOrWhiteSpace(_fields[field].Text))
                {
                    return true;
                }
            }

            return false;
        }

        public bool PasswordsMatch()
        {
            return _fields[FieldType.Password].Text == _fields[FieldType.ConfirmPassword].Text;
        }

        public int GetPasswordLength()
        {
            return _fields[FieldType.Password].Text.Length;
        }

        public string GetUsername()
        {
            return _fields[FieldType.Username].Text;
        }

        public string GetPassword()
        {
            return _fields[FieldType.Password].Text;
        }

        public string GetName()
        {
            return _fields[FieldType.Name].Text;
        }

        public string GetEmail()
        {
            return _fields[FieldType.Email].Text ?? string.Empty;
        }

        public string GetPhone()
        {
            return _fields[FieldType.Phone].Text ?? string.Empty;
        }

        public string GetSelectedUserType()
        {
            return UserTypeComboBox.SelectedItem.ToString();
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        public ValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }
}