using RideMatchProject.LoginClasses;
using RideMatchProject.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject
{
    public partial class RegistrationForm : Form
    {
        // Reference to the database service for user registration
        private readonly DatabaseService _dbService;

        // Collection to store input fields from the form
        private FormInputCollection _inputs;

        // Register and Cancel buttons
        private Button _registerButton;
        private Button _cancelButton;

        // Label to show status or error messages
        private Label _statusLabel;

        // Property to hold the newly registered username
        public string Username { get; private set; }

        // Constructor: initializes form components and sets up the UI
        public RegistrationForm(DatabaseService dbService)
        {
            _dbService = dbService;
            InitializeComponent();
            _inputs = new FormInputCollection();
            SetupUI();
        }

        // Sets up the entire user interface
        private void SetupUI()
        {
            AddTitleLabel();
            CreateInputFields();
            AddButtons();
            AddStatusLabel();

            // Pressing Enter will trigger the register button
            AcceptButton = _registerButton;
        }

        // Adds the main title label to the form
        private void AddTitleLabel()
        {
            var titleLabel = LoginControlFactory.CreateLabel(
                "New User Registration",
                new Point(50, 20),
                new Size(350, 30),
                new Font("Arial", 16, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            );
            Controls.Add(titleLabel);
        }

        // Creates all required input fields
        private void CreateInputFields()
        {
            int y = 70;
            int labelWidth = 120;
            int inputWidth = 230;
            int spacing = 30;

            // Create each input field with label
            AddInputField("Username:", ref y, labelWidth, inputWidth, spacing, FieldType.Username);
            AddInputField("Password:", ref y, labelWidth, inputWidth, spacing, FieldType.Password);
            AddInputField("Confirm Password:", ref y, labelWidth, inputWidth, spacing, FieldType.ConfirmPassword);
            AddInputField("Name:", ref y, labelWidth, inputWidth, spacing, FieldType.Name);
            AddInputField("Email:", ref y, labelWidth, inputWidth, spacing, FieldType.Email);
            AddInputField("Phone:", ref y, labelWidth, inputWidth, spacing, FieldType.Phone);

            // Add user type dropdown (Passenger/Driver)
            AddUserTypeComboBox(y, labelWidth, inputWidth);
        }

        // Helper method to create a single label + textbox field
        private void AddInputField(string label, ref int y, int labelWidth, int inputWidth, int spacing, FieldType fieldType)
        {
            Controls.Add(LoginControlFactory.CreateLabel(label, new Point(50, y), new Size(labelWidth, 20)));

            TextBox textBox = LoginControlFactory.CreateTextBox(new Point(170, y), new Size(inputWidth, 20));
            if (fieldType == FieldType.Password || fieldType == FieldType.ConfirmPassword)
            {
                textBox.PasswordChar = '*'; // Hide password characters
            }

            Controls.Add(textBox);
            _inputs.AddField(fieldType, textBox);

            y += spacing;
        }

        // Adds the user type selection combo box
        private void AddUserTypeComboBox(int y, int labelWidth, int inputWidth)
        {
            Controls.Add(LoginControlFactory.CreateLabel("User Type:", new Point(50, y), new Size(labelWidth, 20)));

            ComboBox userTypeComboBox = LoginControlFactory.CreateComboBox(
                new Point(170, y),
                new Size(inputWidth, 20),
                new string[] { "Passenger", "Driver" },
                0
            );

            Controls.Add(userTypeComboBox);
            _inputs.UserTypeComboBox = userTypeComboBox;
        }

        // Adds Register and Cancel buttons
        private void AddButtons()
        {
            int y = 280;

            // Register button
            _registerButton = LoginControlFactory.CreateButton(
                "Register",
                new Point(170, y),
                new Size(100, 30),
                async (s, e) => await RegisterAsync()
            );
            Controls.Add(_registerButton);

            // Cancel button
            _cancelButton = LoginControlFactory.CreateButton(
                "Cancel",
                new Point(280, y),
                new Size(100, 30),
                (s, e) => Close()
            );
            Controls.Add(_cancelButton);
        }

        // Adds a label for status messages (errors/success)
        private void AddStatusLabel()
        {
            _statusLabel = LoginControlFactory.CreateLabel(
                "",
                new Point(50, 320),
                new Size(350, 20),
                null,
                ContentAlignment.MiddleCenter
            );
            _statusLabel.ForeColor = Color.Red;
            Controls.Add(_statusLabel);
        }

        // Handles the registration process after validation
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

        // Validates all inputs before submitting
        private ValidationResult ValidateInput()
        {
            // Check for empty required fields
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

        // Submits the registration data to the database
        private async Task SubmitRegistration()
        {
            _registerButton.Enabled = false;
            _statusLabel.Text = "Registering...";

            try
            {
                // Get selected user type (Passenger/Driver)
                string userType = _inputs.GetSelectedUserType();

                // Add new user to the database
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

        // Handles the result of the registration attempt
        private void HandleRegistrationResult(int userId)
        {
            if (userId > 0)
            {
                // Success: store the username and close the form
                Username = _inputs.GetUsername();
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _statusLabel.Text = "Registration failed. Please try again.";
            }
        }

        // Handles any exceptions that occur during registration
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

        // Form load event (currently unused)
        private void RegistrationForm_Load(object sender, EventArgs e)
        {
        }
    }
}
