using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using RideMatchProject.Services;

namespace RideMatchProject.UI
{
    /// <summary>
    /// Main form for editing user information
    /// </summary>
    public class UserEditForm : Form
    {
        private readonly FormUIManager _uiManager;
        private readonly FormDataController _dataController;
        private readonly FormValidator _validator;
        private readonly FormStatusManager _statusManager;

        public UserEditForm(DatabaseService dbService, int userId, string username,
            string userType, string name, string email, string phone)
        {
            _statusManager = new FormStatusManager();
            _validator = new FormValidator(_statusManager);
            _uiManager = new FormUIManager(username, userType, name, email, phone);
            _dataController = new FormDataController(dbService, userId, _uiManager, _statusManager);

            InitializeForm();
            _uiManager.InitializeUI(this, PasswordVisibilityChanged, SaveChangesAsync);
            _statusManager.Initialize(_uiManager.StatusLabel);
        }

        private void InitializeForm()
        {
            SuspendLayout();

            Text = "Edit User";
            Size = new Size(450, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AcceptButton = _uiManager.SaveButton;
            CancelButton = _uiManager.CancelButton;

            ResumeLayout(false);
        }

        private void PasswordVisibilityChanged(object sender, EventArgs e)
        {
            _uiManager.UpdatePasswordFieldsVisibility();
        }

        private async Task SaveChangesAsync()
        {
            if (!_validator.ValidateForm(_uiManager))
            {
                return;
            }

            _uiManager.SetSaveInProgress(true);

            try
            {
                bool success = await _dataController.SaveChangesAsync();

                if (success)
                {
                    MessageBox.Show("User information updated successfully",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (Exception ex)
            {
                _statusManager.ShowError($"Error: {ex.Message}");
            }
            finally
            {
                _uiManager.SetSaveInProgress(false);
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // UserEditForm
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "UserEditForm";
            this.Load += new System.EventHandler(this.UserEditForm_Load);
            this.ResumeLayout(false);

        }

        private void UserEditForm_Load(object sender, EventArgs e)
        {

        }
    }

    /// <summary>
    /// Manages the user interface elements
    /// </summary>
    public class FormUIManager
    {
        private TextBox _usernameTextBox;
        private TextBox _nameTextBox;
        private TextBox _emailTextBox;
        private TextBox _phoneTextBox;
        private TextBox _passwordTextBox;
        private TextBox _confirmPasswordTextBox;
        private ComboBox _userTypeComboBox;
        private CheckBox _changePasswordCheckBox;
        private Label _statusLabel;
        private Control _passwordLabel;
        private Control _confirmLabel;

        public Button SaveButton { get; private set; }
        public Button CancelButton { get; private set; }
        public Label StatusLabel => _statusLabel;

        private readonly string _originalUsername;
        private readonly string _originalUserType;
        private readonly string _originalName;
        private readonly string _originalEmail;
        private readonly string _originalPhone;

        public FormUIManager(string username, string userType, string name, string email, string phone)
        {
            _originalUsername = username;
            _originalUserType = userType;
            _originalName = name;
            _originalEmail = email;
            _originalPhone = phone;
        }

        public void InitializeUI(Form form, EventHandler passwordCheckChangedHandler,
            Func<Task> saveHandler)
        {
            CreateFormTitleLabel(form);
            CreateBasicFormFields(form);
            CreatePasswordFields(form, passwordCheckChangedHandler);
            CreateStatusAndButtons(form, saveHandler);
        }

        private void CreateFormTitleLabel(Form form)
        {
            form.Controls.Add(ControlExtensions.CreateLabel(
                "Edit User Information",
                new Point(20, 20),
                new Size(400, 30),
                new Font("Arial", 12, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            ));
        }

        private void CreateBasicFormFields(Form form)
        {
            int y = 65;
            int labelWidth = 120;
            int inputWidth = 260;
            int spacing = 35;

            // Username field
            form.Controls.Add(ControlExtensions.CreateLabel("Username:",
                new Point(20, y), new Size(labelWidth, 20)));
            _usernameTextBox = ControlExtensions.CreateTextBox(
                new Point(150, y), new Size(inputWidth, 20), _originalUsername, false, true);
            form.Controls.Add(_usernameTextBox);
            y += spacing;

            // User Type dropdown
            form.Controls.Add(ControlExtensions.CreateLabel("User Type:",
                new Point(20, y), new Size(labelWidth, 20)));
            _userTypeComboBox = ControlExtensions.CreateComboBox(
                new Point(150, y),
                new Size(inputWidth, 20),
                new string[] { "Admin", "Driver", "Passenger" }
            );
            _userTypeComboBox.SelectedItem = _originalUserType;
            form.Controls.Add(_userTypeComboBox);
            y += spacing;

            // Name field
            form.Controls.Add(ControlExtensions.CreateLabel("Name:",
                new Point(20, y), new Size(labelWidth, 20)));
            _nameTextBox = ControlExtensions.CreateTextBox(
                new Point(150, y), new Size(inputWidth, 20), _originalName);
            form.Controls.Add(_nameTextBox);
            y += spacing;

            // Email field
            form.Controls.Add(ControlExtensions.CreateLabel("Email:",
                new Point(20, y), new Size(labelWidth, 20)));
            _emailTextBox = ControlExtensions.CreateTextBox(
                new Point(150, y), new Size(inputWidth, 20), _originalEmail);
            form.Controls.Add(_emailTextBox);
            y += spacing;

            // Phone field
            form.Controls.Add(ControlExtensions.CreateLabel("Phone:",
                new Point(20, y), new Size(labelWidth, 20)));
            _phoneTextBox = ControlExtensions.CreateTextBox(
                new Point(150, y), new Size(inputWidth, 20), _originalPhone);
            form.Controls.Add(_phoneTextBox);
            y += spacing;

            // Change password checkbox
            _changePasswordCheckBox = ControlExtensions.CreateCheckBox(
                "Change Password",
                new Point(20, y),
                new Size(130, 20),
                false
            );
            form.Controls.Add(_changePasswordCheckBox);
        }

        private void CreatePasswordFields(Form form, EventHandler passwordCheckChangedHandler)
        {
            int y = 240;
            int labelWidth = 120;
            int inputWidth = 260;
            int spacing = 35;

            // Password field
            _passwordLabel = ControlExtensions.CreateLabel("New Password:",
                new Point(20, y), new Size(labelWidth, 20));
            _passwordTextBox = ControlExtensions.CreateTextBox(
                new Point(150, y), new Size(inputWidth, 20), "");
            _passwordTextBox.PasswordChar = '*';
            _passwordTextBox.Visible = false;
            form.Controls.Add(_passwordLabel);
            form.Controls.Add(_passwordTextBox);
            y += spacing;

            // Confirm password field
            _confirmLabel = ControlExtensions.CreateLabel("Confirm Password:",
                new Point(20, y), new Size(labelWidth, 20));
            _confirmPasswordTextBox = ControlExtensions.CreateTextBox(
                new Point(150, y), new Size(inputWidth, 20), "");
            _confirmPasswordTextBox.PasswordChar = '*';
            _confirmPasswordTextBox.Visible = false;
            form.Controls.Add(_confirmLabel);
            form.Controls.Add(_confirmPasswordTextBox);

            // Set up the checkbox change event
            _changePasswordCheckBox.CheckedChanged += passwordCheckChangedHandler;
        }

        private void CreateStatusAndButtons(Form form, Func<Task> saveHandler)
        {
            int y = 310;

            // Status label
            _statusLabel = ControlExtensions.CreateLabel(
                "",
                new Point(20, y),
                new Size(390, 20),
                null,
                ContentAlignment.MiddleCenter
            );
            _statusLabel.ForeColor = Color.Red;
            form.Controls.Add(_statusLabel);
            y += 30;

            // Save button
            SaveButton = ControlExtensions.CreateButton(
                "Save Changes",
                new Point(150, y),
                new Size(120, 30),
                async (s, e) => await saveHandler()
            );
            form.Controls.Add(SaveButton);

            // Cancel button
            CancelButton = ControlExtensions.CreateButton(
                "Cancel",
                new Point(280, y),
                new Size(120, 30),
                (s, e) => form.DialogResult = DialogResult.Cancel
            );
            form.Controls.Add(CancelButton);
        }

        public void UpdatePasswordFieldsVisibility()
        {
            bool showPasswordFields = _changePasswordCheckBox.Checked;

            _passwordLabel.Visible = showPasswordFields;
            _confirmLabel.Visible = showPasswordFields;
            _passwordTextBox.Visible = showPasswordFields;
            _confirmPasswordTextBox.Visible = showPasswordFields;

            if (!showPasswordFields)
            {
                _passwordTextBox.Text = "";
                _confirmPasswordTextBox.Text = "";
            }
        }

        public void SetSaveInProgress(bool inProgress)
        {
            SaveButton.Enabled = !inProgress;
        }

        public string GetName() => _nameTextBox.Text;
        public string GetEmail() => _emailTextBox.Text;
        public string GetPhone() => _phoneTextBox.Text;
        public string GetUserType() => _userTypeComboBox.SelectedItem.ToString();
        public string GetPassword() => _passwordTextBox.Text;
        public string GetConfirmPassword() => _confirmPasswordTextBox.Text;
        public bool IsChangingPassword() => _changePasswordCheckBox.Checked;
    }

    /// <summary>
    /// Validates form input data
    /// </summary>
    public class FormValidator
    {
        private readonly FormStatusManager _statusManager;

        public FormValidator(FormStatusManager statusManager)
        {
            _statusManager = statusManager;
        }

        public bool ValidateForm(FormUIManager uiManager)
        {
            if (!ValidateName(uiManager.GetName()))
            {
                return false;
            }

            if (uiManager.IsChangingPassword() && !ValidatePassword(
                uiManager.GetPassword(), uiManager.GetConfirmPassword()))
            {
                return false;
            }

            return true;
        }

        private bool ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _statusManager.ShowError("Name cannot be empty");
                return false;
            }
            return true;
        }

        private bool ValidatePassword(string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                _statusManager.ShowError("Password cannot be empty");
                return false;
            }

            if (password != confirmPassword)
            {
                _statusManager.ShowError("Passwords do not match");
                return false;
            }

            if (password.Length < 6)
            {
                _statusManager.ShowError("Password must be at least 6 characters");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Manages status messages in the form
    /// </summary>
    public class FormStatusManager
    {
        private Label _statusLabel;

        public void Initialize(Label statusLabel)
        {
            _statusLabel = statusLabel;
        }

        public void ShowError(string message)
        {
            if (_statusLabel == null)
            {
                return;
            }

            _statusLabel.Text = message;
            _statusLabel.ForeColor = Color.Red;
        }

        public void ShowInfo(string message)
        {
            if (_statusLabel == null)
            {
                return;
            }

            _statusLabel.Text = message;
            _statusLabel.ForeColor = Color.Blue;
        }

        public void ClearStatus()
        {
            if (_statusLabel == null)
            {
                return;
            }

            _statusLabel.Text = "";
        }
    }

    /// <summary>
    /// Handles data operations with the database service
    /// </summary>
    public class FormDataController
    {
        private readonly DatabaseService _dbService;
        private readonly int _userId;
        private readonly FormUIManager _uiManager;
        private readonly FormStatusManager _statusManager;

        public FormDataController(DatabaseService dbService, int userId,
            FormUIManager uiManager, FormStatusManager statusManager)
        {
            _dbService = dbService;
            _userId = userId;
            _uiManager = uiManager;
            _statusManager = statusManager;
        }

        public async Task<bool> SaveChangesAsync()
        {
            _statusManager.ShowInfo("Saving changes...");

            bool success = await UpdateUserProfile();

            if (success && _uiManager.IsChangingPassword())
            {
                success = await UpdatePassword();
            }

            if (!success)
            {
                _statusManager.ShowError("Failed to update user information");
            }

            return success;
        }

        private async Task<bool> UpdateUserProfile()
        {
            return await _dbService.UpdateUserProfileAsync(
                _userId,
                _uiManager.GetUserType(),
                _uiManager.GetName(),
                _uiManager.GetEmail(),
                _uiManager.GetPhone()
            );
        }

        private async Task<bool> UpdatePassword()
        {
            return await _dbService.ChangePasswordAsync(
                _userId,
                _uiManager.GetPassword()
            );
        }
    }
}