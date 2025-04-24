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
    /// Controller for the Users tab
    /// </summary>
    public class UsersTabController : TabControllerBase
    {
        private ListView _usersListView;
        private Button _refreshButton;
        private Button _addButton;
        private Button _editButton;
        private Button _deleteButton;

        public UsersTabController(DatabaseService dbService, AdminDataManager dataManager)
            : base(dbService, null, dataManager)
        {
        }

        public override void InitializeTab(TabPage tabPage)
        {
            CreateListView(tabPage);
            CreateActionButtons(tabPage);
        }

        private void CreateListView(TabPage tabPage)
        {
            _usersListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 650),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            // Add columns
            _usersListView.Columns.Add("ID", 50);
            _usersListView.Columns.Add("Username", 150);
            _usersListView.Columns.Add("User Type", 100);
            _usersListView.Columns.Add("Name", 200);
            _usersListView.Columns.Add("Email", 200);
            _usersListView.Columns.Add("Phone", 150);

            tabPage.Controls.Add(_usersListView);
        }

        private void CreateActionButtons(TabPage tabPage)
        {
            // Create Refresh Button
            _refreshButton = AdminUIFactory.CreateButton(
                "Refresh Users",
                new Point(10, 10),
                new Size(120, 30),
                RefreshButtonClick
            );

            // Create Add Button
            _addButton = AdminUIFactory.CreateButton(
                "Add User",
                new Point(140, 10),
                new Size(120, 30),
                AddButtonClick
            );

            // Create Edit Button
            _editButton = AdminUIFactory.CreateButton(
                "Edit User",
                new Point(270, 10),
                new Size(120, 30),
                EditButtonClick
            );

            // Create Delete Button
            _deleteButton = AdminUIFactory.CreateButton(
                "Delete User",
                new Point(400, 10),
                new Size(120, 30),
                DeleteButtonClick
            );

            // Add all buttons to the tab
            tabPage.Controls.Add(_refreshButton);
            tabPage.Controls.Add(_addButton);
            tabPage.Controls.Add(_editButton);
            tabPage.Controls.Add(_deleteButton);
        }

        private async void RefreshButtonClick(object sender, EventArgs e)
        {
            await RefreshTabAsync();
        }

        private void AddButtonClick(object sender, EventArgs e)
        {
            using (var regForm = new RegistrationForm(DbService))
            {
                if (regForm.ShowDialog() == DialogResult.OK)
                {
                    TaskManager.ExecuteAsync(RefreshTabAsync);
                }
            }
        }

        private void EditButtonClick(object sender, EventArgs e)
        {
            if (_usersListView.SelectedItems.Count == 0)
            {
                MessageDisplayer.ShowInfo("Please select a user to edit.", "No Selection");
                return;
            }

            // Get selected user details
            var item = _usersListView.SelectedItems[0];
            int userId = int.Parse(item.Text);
            string username = item.SubItems[1].Text;
            string userType = item.SubItems[2].Text;
            string name = item.SubItems[3].Text;
            string email = item.SubItems[4].Text;
            string phone = item.SubItems[5].Text;

            // Open editor
            using (var editForm = new UserEditForm(
                DbService, userId, username, userType, name, email, phone))
            {
                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    TaskManager.ExecuteAsync(RefreshTabAsync);
                }
            }
        }

        private async void DeleteButtonClick(object sender, EventArgs e)
        {
            if (_usersListView.SelectedItems.Count == 0)
            {
                MessageDisplayer.ShowInfo("Please select a user to delete.", "No Selection");
                return;
            }

            // Get selected user
            var item = _usersListView.SelectedItems[0];
            int userId = int.Parse(item.Text);
            string username = item.SubItems[1].Text;

            // Confirm deletion
            var result = MessageDisplayer.ShowConfirmation(
                $"Are you sure you want to delete user {username}?",
                "Confirm Deletion"
            );

            if (result == DialogResult.Yes)
            {
                await DeleteUserAsync(userId, username);
            }
        }

        private async Task DeleteUserAsync(int userId, string username)
        {
            try
            {
                bool success = await DbService.DeleteUserAsync(userId);

                if (success)
                {
                    await RefreshTabAsync();
                    MessageDisplayer.ShowInfo(
                        $"User {username} deleted successfully.",
                        "User Deleted"
                    );
                }
                else
                {
                    MessageDisplayer.ShowError(
                        $"Could not delete user {username}. The user may not exist.",
                        "Deletion Failed"
                    );
                }
            }
            catch (Exception ex)
            {
                MessageDisplayer.ShowError(
                    $"Error deleting user: {ex.Message}",
                    "Deletion Error"
                );
            }
        }

        public override async Task RefreshTabAsync()
        {
            await DataManager.LoadUsersAsync();
            await DisplayUsersAsync();
        }

        private async Task DisplayUsersAsync()
        {
            _usersListView.Items.Clear();

            var users = DataManager.Users;
            if (users == null)
            {
                return;
            }

            foreach (var user in users)
            {
                var item = new ListViewItem(user.Id.ToString());
                item.SubItems.Add(user.Username);
                item.SubItems.Add(user.UserType);
                item.SubItems.Add(user.Name);
                item.SubItems.Add(user.Email);
                item.SubItems.Add(user.Phone);

                _usersListView.Items.Add(item);
            }
        }
    }
}
