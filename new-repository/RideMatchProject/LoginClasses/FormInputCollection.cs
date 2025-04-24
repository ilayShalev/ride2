using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.LoginClasses
{
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
}
