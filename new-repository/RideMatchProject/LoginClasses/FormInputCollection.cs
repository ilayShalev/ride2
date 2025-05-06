using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.LoginClasses
{
    /// <summary>
    /// A class that manages a collection of form input fields for a login or registration form.
    /// It stores TextBox controls associated with specific field types and provides methods to validate and retrieve input data.
    /// </summary>
    public class FormInputCollection
    {
        /// <summary>
        /// A private dictionary that maps <see cref="FieldType"/> values to corresponding <see cref="TextBox"/> controls.
        /// </summary>
        private readonly Dictionary<FieldType, TextBox> _fields;

        /// <summary>
        /// Gets or sets the ComboBox control used to select the user type.
        /// </summary>
        public ComboBox UserTypeComboBox { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FormInputCollection"/> class.
        /// Creates an empty dictionary to store field mappings.
        /// </summary>
        public FormInputCollection()
        {
            _fields = new Dictionary<FieldType, TextBox>();
        }

        /// <summary>
        /// Adds a TextBox control to the collection, associating it with a specified field type.
        /// </summary>
        /// <param name="type">The <see cref="FieldType"/> that identifies the field (e.g., Username, Password).</param>
        /// <param name="textBox">The <see cref="TextBox"/> control containing the user input for the field.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="textBox"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="type"/> is already associated with another TextBox.</exception>
        public void AddField(FieldType type, TextBox textBox)
        {
            if (textBox == null)
                throw new ArgumentNullException(nameof(textBox), "TextBox cannot be null.");

            if (_fields.ContainsKey(type))
                throw new ArgumentException($"Field type {type} is already associated with a TextBox.", nameof(type));

            _fields[type] = textBox;
        }

        /// <summary>
        /// Checks if any required fields are empty or contain only whitespace.
        /// Required fields include Username, Password, ConfirmPassword, and Name.
        /// </summary>
        /// <returns>
        /// <c>true</c> if any required field is empty or contains only whitespace; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="KeyNotFoundException">Thrown if a required field is not found in the collection.</exception>
        public bool HasEmptyRequiredFields()
        {
            FieldType[] requiredFields = { FieldType.Username, FieldType.Password, FieldType.ConfirmPassword, FieldType.Name };

            foreach (FieldType field in requiredFields)
            {
                if (!_fields.ContainsKey(field))
                    throw new KeyNotFoundException($"Required field {field} is not registered in the collection.");

                if (string.IsNullOrWhiteSpace(_fields[field].Text))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Verifies if the text in the Password and ConfirmPassword fields match.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the Password and ConfirmPassword fields contain the same text; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="KeyNotFoundException">Thrown if either Password or ConfirmPassword field is not found in the collection.</exception>
        public bool PasswordsMatch()
        {
            if (!_fields.ContainsKey(FieldType.Password) || !_fields.ContainsKey(FieldType.ConfirmPassword))
                throw new KeyNotFoundException("Password or ConfirmPassword field is not registered in the collection.");

            return _fields[FieldType.Password].Text == _fields[FieldType.ConfirmPassword].Text;
        }

        /// <summary>
        /// Gets the length of the text in the Password field.
        /// </summary>
        /// <returns>The number of characters in the Password field.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the Password field is not found in the collection.</exception>
        public int GetPasswordLength()
        {
            if (!_fields.ContainsKey(FieldType.Password))
                throw new KeyNotFoundException("Password field is not registered in the collection.");

            return _fields[FieldType.Password].Text.Length;
        }

        /// <summary>
        /// Retrieves the text entered in the Username field.
        /// </summary>
        /// <returns>The text in the Username field.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the Username field is not found in the collection.</exception>
        public string GetUsername()
        {
            if (!_fields.ContainsKey(FieldType.Username))
                throw new KeyNotFoundException("Username field is not registered in the collection.");

            return _fields[FieldType.Username].Text;
        }

        /// <summary>
        /// Retrieves the text entered in the Password field.
        /// </summary>
        /// <returns>The text in the Password field.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the Password field is not found in the collection.</exception>
        public string GetPassword()
        {
            if (!_fields.ContainsKey(FieldType.Password))
                throw new KeyNotFoundException("Password field is not registered in the collection.");

            return _fields[FieldType.Password].Text;
        }

        /// <summary>
        /// Retrieves the text entered in the Name field.
        /// </summary>
        /// <returns>The text in the Name field.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the Name field is not found in the collection.</exception>
        public string GetName()
        {
            if (!_fields.ContainsKey(FieldType.Name))
                throw new KeyNotFoundException("Name field is not registered in the collection.");

            return _fields[FieldType.Name].Text;
        }

        /// <summary>
        /// Retrieves the text entered in the Email field.
        /// </summary>
        /// <returns>The text in the Email field, or an empty string if the field is not registered or empty.</returns>
        public string GetEmail()
        {
            return _fields.ContainsKey(FieldType.Email) ? _fields[FieldType.Email].Text ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Retrieves the text entered in the Phone field.
        /// </summary>
        /// <returns>The text in the Phone field, or an empty string if the field is not registered or empty.</returns>
        public string GetPhone()
        {
            return _fields.ContainsKey(FieldType.Phone) ? _fields[FieldType.Phone].Text ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Retrieves the selected item from the UserTypeComboBox.
        /// </summary>
        /// <returns>The text of the selected item in the ComboBox.</returns>
        /// <exception cref="NullReferenceException">Thrown if <see cref="UserTypeComboBox"/> is null or no item is selected.</exception>
        public string GetSelectedUserType()
        {
            if (UserTypeComboBox == null || UserTypeComboBox.SelectedItem == null)
                throw new NullReferenceException("UserTypeComboBox is not set or no item is selected.");

            return UserTypeComboBox.SelectedItem.ToString();
        }
    }
}