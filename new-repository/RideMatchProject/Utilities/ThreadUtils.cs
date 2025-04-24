using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.Utilities
{
    /// <summary>
    /// Utility class for thread management operations
    /// </summary>
    public static class ThreadUtils
    {
        /// <summary>
        /// Executes an action on the UI thread associated with the control
        /// </summary>
        public static void ExecuteOnUIThread(Control control, Action action)
        {
            if (control == null || control.IsDisposed)
            {
                return;
            }

            if (control.InvokeRequired)
            {
                try
                {
                    control.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                    // Control may have been disposed if form is closing
                }
                catch (InvalidOperationException ex)
                {
                    // Handle case where handle isn't created yet
                    Console.WriteLine($"UI operation failed: {ex.Message}");
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Executes a function on the UI thread and returns its result
        /// </summary>
        public static T ExecuteOnUIThread<T>(Control control, Func<T> func)
        {
            if (control == null || control.IsDisposed)
            {
                return default;
            }

            if (control.InvokeRequired)
            {
                try
                {
                    return (T)control.Invoke(func);
                }
                catch (ObjectDisposedException)
                {
                    // Control may have been disposed if form is closing
                    return default;
                }
                catch (InvalidOperationException ex)
                {
                    // Handle case where handle isn't created yet
                    Console.WriteLine($"UI operation failed: {ex.Message}");
                    return default;
                }
            }
            else
            {
                return func();
            }
        }

        /// <summary>
        /// Runs a task with proper error handling
        /// </summary>
        public static async void SafeTaskRun(Func<Task> taskFunc, Action<Exception> errorHandler = null)
        {
            try
            {
                await taskFunc();
            }
            catch (Exception ex)
            {
                errorHandler?.Invoke(ex);
                Console.WriteLine($"Task error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Shows an error message on the UI thread
        /// </summary>
        public static void ShowErrorMessage(Control parentControl, string message, string title = "Error")
        {
            ExecuteOnUIThread(parentControl, () => {
                MessageBox.Show(parentControl, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            });
        }

        /// <summary>
        /// Shows an information message on the UI thread
        /// </summary>
        public static void ShowInfoMessage(Control parentControl, string message, string title = "Information")
        {
            ExecuteOnUIThread(parentControl, () => {
                MessageBox.Show(parentControl, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// Updates a control property on the UI thread
        /// </summary>
        public static void UpdateControlProperty<T>(Control control, Action<T> propertyUpdater, T value)
        {
            ExecuteOnUIThread(control, () => propertyUpdater(value));
        }
    }
}