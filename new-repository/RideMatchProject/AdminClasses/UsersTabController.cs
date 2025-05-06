using RideMatchProject.Services;
using RideMatchProject.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Controller for the Users tab in the admin interface, responsible for managing user-related UI elements
    /// and interactions, such as displaying, adding, editing, and deleting users.
    /// </summary>
    public class UsersTabController : TabControllerBase
    {
        private ListView _usersListView; // Displays the list of users in a tabular format
        private Button _refreshButton;   // Button to refresh the user list
        private Button _addButton;      // Button to add a new user
        private Button _editButton;     // Button to edit a selected user
        private Button _deleteButton;   // Button to delete a selected user

        /// <summary>
        /// Initializes a new instance of the <see cref="UsersTabController"/> class.
        /// </summary>
        /// <param name="dbService">The database service used for user data operations.</param>
        /// <param name="dataManager">The data manager responsible for loading and managing user data.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="dbService"/> or <paramref name="dataManager"/> is null.</exception>
        public UsersTabController(DatabaseService dbService, AdminDataManager dataManager)
            : base(dbService, null, dataManager)
        {
            // Validation is assumed to be handled in the base class constructor
        }

        /// <summary>
        /// Initializes the Users tab by creating and configuring the UI elements, including the user list view
        /// and action buttons (Refresh, Add, Edit, Delete).
        /// </summary>
        /// <param name="tabPage">The <see cref="TabPage"/> that will contain the UI elements.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="tabPage"/> is null.</exception>
        public override void InitializeTab(TabPage tabPage)
        {
            if (tabPage == null)
                throw new ArgumentNullException(nameof(tabPage));

            CreateListView(tabPage);      // Set up the ListView for displaying users
            CreateActionButtons(tabPage); // Set up action buttons
        }

        /// <summary>
        /// Creates and configures the ListView control to display user data in a tabular format.
        /// </summary>
        /// <param name="tabPage">The <see cref="TabPage"/> where the ListView will be placed.</param>
        private void CreateListView(TabPage tabPage)
        {
            _usersListView = new ListView
            {
                Location = new Point(10, 50),      // Position the ListView on the tab
                Size = new Size(1140, 650),       // Set the size of the ListView
                View = View.Details,              // Display in Details mode (table-like)
                FullRowSelect = true,             // Allow selecting entire rows
                GridLines = true,                 // Show grid lines for clarity
                MultiSelect = false               // Allow only single row selection
            };

            // Add columns to the ListView with specified headers and widths
            _usersListView.Columns.Add("ID", 50);
            _usersListView.Columns.Add("Username", 150);
            _usersListView.Columns.Add("User Type", 100);
            _usersListView.Columns.Add("Name", 200);
            _usersListView.Columns.Add("Email", 200);
            _usersListView.Columns.Add("Phone", 150);

            tabPage.Controls.Add(_usersListView); // Add the ListView to the tab
        }

        /// <summary>
        /// Creates and configures the action buttons (Refresh, Add, Edit, Delete) for user management.
        /// </summary>
        /// <param name="tabPage">The <see cref="TabPage"/> where the buttons will be placed.</param>
        private void CreateActionButtons(TabPage tabPage)
        {
            // Create Refresh Button to reload user data
            _refreshButton = AdminUIFactory.CreateButton(
                "Refresh Users",                     // Button text
                new Point(10, 10),                  // Button position
                new Size(120, 30),                  // Button size
                RefreshButtonClick                  // Event handler for click
            );

            // Create Add Button to open a form for adding a new user
            _addButton = AdminUIFactory.CreateButton(
                "Add User",
                new Point(140, 10),
                new Size(120, 30),
                AddButtonClick
            );

            // Create Edit Button to open a form for editing a selected user
            _editButton = AdminUIFactory.CreateButton(
                "Edit User",
                new Point(270, 10),
                new Size(120, 30),
                EditButtonClick
            );

            // Create Delete Button to delete a selected user
            _deleteButton = AdminUIFactory.CreateButton(
                "Delete User",
                new Point(400, 10),
                new Size(120, 30),
                DeleteButtonClick
            );

            // Add all buttons to the tab's control collection
            tabPage.Controls.Add(_refreshButton);
            tabPage.Controls.Add(_addButton);
            tabPage.Controls.Add(_editButton);
            tabPage.Controls.Add(_deleteButton);
        }

        /// <summary>
        /// Event handler for the Refresh button click. Triggers an asynchronous refresh of the user list.
        /// </summary>
        /// <param name="sender">The object that raised the event (the Refresh button).</param>
        /// <param name="e">The event arguments.</param>
        private async void RefreshButtonClick(object sender, EventArgs e)
        {
            await RefreshTabAsync(); // Refresh the user list asynchronously
        }

        /// <summary>
        /// Event handler for the Add button click. Opens a form to add a new user and refreshes
        /// the user list if the operation is successful.
        /// </summary>
        /// <param name="sender">The object that raised the event (the Add button).</param>
        /// <param name="e">The event arguments.</param>
        private void AddButtonClick(object sender, EventArgs e)
        {
            using (var regForm = new RegistrationForm(DbService))
            {
                // Show the registration form as a dialog
                if (regForm.ShowDialog() == DialogResult.OK)
                {
                    // If the user successfully adds a new user, refresh the list
                    TaskManager.ExecuteAsync(RefreshTabAsync);
                }
            }
        }

        /// <summary>
        /// Event handler for the Edit button click. Opens a form to edit the selected user's details
        /// and refreshes the user list if the operation is successful.
        /// </summary>
        /// <param name="sender">The object that raised the event (the Edit button).</param>
        /// <param name="e">The event arguments.</param>
        private void EditButtonClick(object sender, EventArgs e)
        {
            if (_usersListView.SelectedItems.Count == 0)
            {
                // Show a message if no user is selected
                MessageDisplayer.ShowInfo("Please select a user to edit.", "No Selection");
                return;
            }

            // Get details of the selected user from the ListView
            var item = _usersListView.SelectedItems[0];
            int userId = int.Parse(item.Text);              // User ID
            string username = item.SubItems[1].Text;        // Username
            string userType = item.SubItems[2].Text;        // User Type
            string name = item.SubItems[3].Text;            // Name
            string email = item.SubItems[4].Text;           // Email
            string phone = item.SubItems[5].Text;           // Phone

            // Open the edit form with the selected user's details
            using (var editForm = new UserEditForm(DbService, userId, username, userType, name, email, phone))
            {
                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    // If the user successfully edits the user, refresh the list
                    TaskManager.ExecuteAsync(RefreshTabAsync);
                }
            }
        }

        /// <summary>
        /// Event handler for the Delete button click. Prompts for confirmation and deletes the selected user.
        /// </summary>
        /// <param name="sender">The object that raised the event (the Delete button).</param>
        /// <param name="e">The event arguments.</param>
        private async void DeleteButtonClick(object sender, EventArgs e)
        {
            if (_usersListView.SelectedItems.Count == 0)
            {
                // Show a message if no user is selected
                MessageDisplayer.ShowInfo("Please select a user to delete.", "No Selection");
                return;
            }

            // Get details of the selected user
            var item = _usersListView.SelectedItems[0];
            int userId = int.Parse(item.Text);      // User ID
            string username = item.SubItems[1].Text; // Username

            // Show confirmation dialog for deletion
            var result = MessageDisplayer.ShowConfirmation(
                $"Are you sure you want to delete user {username}?",
                "Confirm Deletion"
            );

            if (result == DialogResult.Yes)
            {
                // Proceed with deletion if confirmed
                await DeleteUserAsync(userId, username);
            }
        }

        /// <summary>
        /// Asynchronously deletes a user from the database and refreshes the user list.
        /// </summary>
        /// <param name="userId">The ID of the user to delete.</param>
        /// <param name="username">The username of the user, used for user feedback.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task DeleteUserAsync(int userId, string username)
        {
            try
            {
                // Attempt to delete the user via the database service
                bool success = await DbService.DeleteUserAsync(userId);

                if (success)
                {
                    // Refresh the user list and show success message
                    await RefreshTabAsync();
                    MessageDisplayer.ShowInfo(
                        $"User {username} deleted successfully.",
                        "User Deleted"
                    );
                }
                else
                {
                    // Show error if deletion fails
                    MessageDisplayer.ShowError(
                        $"Could not delete user {username}. The user may not exist.",
                        "Deletion Failed"
                    );
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions during deletion
                MessageDisplayer.ShowError(
                    $"Error deleting user: {ex.Message}",
                    "Deletion Error"
                );
            }
        }

        /// <summary>
        /// Asynchronously refreshes the user list by reloading data from the database and updating the UI.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public override async Task RefreshTabAsync()
        {
            await DataManager.LoadUsersAsync(); // Load user data from the database
            await DisplayUsersAsync();         // Update the ListView with the loaded data
        }

        /// <summary>
        /// Asynchronously updates the ListView with the current list of users from the data manager.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task DisplayUsersAsync()
        {
            _usersListView.Items.Clear(); // Clear existing items in the ListView

            var users = DataManager.Users; // Get the list of users
            if (users == null)
            {
                return; // Exit if no users are available
            }

            // Populate the ListView with user data
            foreach (var user in users)
            {
                var item = new ListViewItem(user.Id.ToString()); // Create a new row with user ID
                item.SubItems.Add(user.Username);               // Add Username
                item.SubItems.Add(user.UserType);               // Add User Type
                item.SubItems.Add(user.Name);                   // Add Name
                item.SubItems.Add(user.Email);                  // Add Email
                item.SubItems.Add(user.Phone);                  // Add Phone

                _usersListView.Items.Add(item); // Add the row to the ListView
            }
        }
    }
}