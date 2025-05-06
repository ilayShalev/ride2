using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Utility class for standard message dialogs.
    /// Provides static methods to display information, warning, error, and confirmation dialogs.
    /// </summary>
    public static class MessageDisplayer
    {
        /// <summary>
        /// Displays an information message dialog.
        /// </summary>
        /// <param name="message">The message to display in the dialog.</param>
        /// <param name="title">The title of the dialog (optional, default is "Information").</param>
        public static void ShowInfo(string message, string title = "Information")
        {
            MessageBox.Show(
                message,           // The message content to be shown in the dialog box.
                title,             // The title of the dialog box.
                MessageBoxButtons.OK,  // Specifies that only an "OK" button is displayed.
                MessageBoxIcon.Information // Specifies that the dialog box will display an information icon.
            );
        }

        /// <summary>
        /// Displays a warning message dialog.
        /// </summary>
        /// <param name="message">The message to display in the dialog.</param>
        /// <param name="title">The title of the dialog (optional, default is "Warning").</param>
        public static void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(
                message,           // The message content to be shown in the dialog box.
                title,             // The title of the dialog box.
                MessageBoxButtons.OK,  // Specifies that only an "OK" button is displayed.
                MessageBoxIcon.Warning // Specifies that the dialog box will display a warning icon.
            );
        }

        /// <summary>
        /// Displays an error message dialog.
        /// </summary>
        /// <param name="message">The message to display in the dialog.</param>
        /// <param name="title">The title of the dialog (optional, default is "Error").</param>
        public static void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(
                message,           // The message content to be shown in the dialog box.
                title,             // The title of the dialog box.
                MessageBoxButtons.OK,  // Specifies that only an "OK" button is displayed.
                MessageBoxIcon.Error // Specifies that the dialog box will display an error icon.
            );
        }

        /// <summary>
        /// Displays a confirmation dialog with Yes/No options.
        /// </summary>
        /// <param name="message">The message to display in the dialog.</param>
        /// <param name="title">The title of the dialog (optional, default is "Confirm").</param>
        /// <returns>Returns a <see cref="DialogResult"/> representing the user's choice (Yes/No).</returns>
        public static DialogResult ShowConfirmation(string message, string title = "Confirm")
        {
            return MessageBox.Show(
                message,           // The message content to be shown in the dialog box.
                title,             // The title of the dialog box.
                MessageBoxButtons.YesNo,  // Specifies that both "Yes" and "No" buttons will be displayed.
                MessageBoxIcon.Question // Specifies that the dialog box will display a question icon.
            );
        }
    }
}
