using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace RideMatchProject.Utilities
{
    /// <summary>
    /// Comprehensive utility class for thread management operations
    /// </summary>
    public static class ThreadUtils
    {
        private static readonly object _syncLock = new object();

        /// <summary>
        /// Executes an action on the UI thread associated with the control
        /// </summary>
        public static void ExecuteOnUIThread(Control control, Action action)
        {
            if (control == null || control.IsDisposed)
            {
                Debug.WriteLine("Warning: Attempted to execute UI operation on null or disposed control");
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
                    Debug.WriteLine("Warning: Control was disposed during invoke");
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine($"UI thread operation failed: {ex.Message}");
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
                Debug.WriteLine("Warning: Attempted to execute UI operation on null or disposed control");
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
                    Debug.WriteLine("Warning: Control was disposed during invoke");
                    return default;
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine($"UI thread operation failed: {ex.Message}");
                    return default;
                }
            }
            else
            {
                return func();
            }
        }

        /// <summary>
        /// Executes an async task on the UI thread
        /// </summary>
        public static async Task ExecuteOnUIThreadAsync(Control control, Func<Task> asyncAction)
        {
            if (control == null || control.IsDisposed)
            {
                Debug.WriteLine("Warning: Attempted to execute async UI operation on null or disposed control");
                return;
            }

            if (control.InvokeRequired)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();

                try
                {
                    control.Invoke(new Action(async () =>
                    {
                        try
                        {
                            await asyncAction();
                            taskCompletionSource.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            taskCompletionSource.SetException(ex);
                        }
                    }));

                    await taskCompletionSource.Task;
                }
                catch (ObjectDisposedException)
                {
                    Debug.WriteLine("Warning: Control was disposed during async invoke");
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine($"Async UI thread operation failed: {ex.Message}");
                }
            }
            else
            {
                await asyncAction();
            }
        }

        /// <summary>
        /// Safely runs a task with proper error handling
        /// </summary>
        public static async void SafeTaskRun(Func<Task> taskFunc, Action<Exception> errorHandler = null)
        {
            try
            {
                await taskFunc();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Task error: {ex.Message}\n{ex.StackTrace}");
                errorHandler?.Invoke(ex);
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
        /// Shows a confirmation dialog on the UI thread and returns the result
        /// </summary>
        public static DialogResult ShowConfirmationDialog(Control parentControl, string message, string title = "Confirm")
        {
            return ExecuteOnUIThread(parentControl, () =>
                MessageBox.Show(parentControl, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            );
        }

        /// <summary>
        /// Updates control text on the UI thread
        /// </summary>
        public static void UpdateControlText(Control control, string text)
        {
            ExecuteOnUIThread(control, () => {
                control.Text = text;
            });
        }

        /// <summary>
        /// Updates control enabled state on the UI thread
        /// </summary>
        public static void UpdateControlEnabled(Control control, bool enabled)
        {
            ExecuteOnUIThread(control, () => {
                control.Enabled = enabled;
            });
        }

        /// <summary>
        /// Updates control visibility on the UI thread
        /// </summary>
        public static void UpdateControlVisibility(Control control, bool visible)
        {
            ExecuteOnUIThread(control, () => {
                control.Visible = visible;
            });
        }

        /// <summary>
        /// Properly awaits a task and handles exceptions
        /// </summary>
        public static async Task AwaitSafelyAsync(Task task, Control control = null, string errorMessage = "Operation failed")
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Task error: {ex.Message}\n{ex.StackTrace}");

                if (control != null)
                {
                    ShowErrorMessage(control, $"{errorMessage}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Properly awaits a task with result and handles exceptions
        /// </summary>
        public static async Task<T> AwaitSafelyAsync<T>(Task<T> task, T defaultValue = default, Control control = null, string errorMessage = "Operation failed")
        {
            try
            {
                return await task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Task error: {ex.Message}\n{ex.StackTrace}");

                if (control != null)
                {
                    ShowErrorMessage(control, $"{errorMessage}: {ex.Message}");
                }

                return defaultValue;
            }
        }
    }
}