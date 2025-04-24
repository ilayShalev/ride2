using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Utility class for standard message dialogs
    /// </summary>
    public static class MessageDisplayer
    {
        public static void ShowInfo(string message, string title = "Information")
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        public static void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        public static void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        public static DialogResult ShowConfirmation(string message, string title = "Confirm")
        {
            return MessageBox.Show(
                message,
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
        }
    }
}
