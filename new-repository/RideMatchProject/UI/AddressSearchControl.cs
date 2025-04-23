using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using RideMatchProject.Services;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;

namespace RideMatchProject.UI
{
    /// <summary>
    /// Main user control for address search functionality
    /// </summary>
    public class AddressSearchControl : UserControl
    {
        private readonly MapService _mapService;
        private readonly GMapControl _mapControl;
        private readonly UIComponentManager _componentManager;
        private readonly AddressSearchManager _searchManager;
        private readonly StatusDisplayManager _statusManager;

        public event EventHandler<AddressFoundEventArgs> AddressFound;

        public string Address
        {
            get => _componentManager.AddressText;
            set => _componentManager.AddressText = value;
        }

        public AddressSearchControl(MapService mapService, GMapControl mapControl)
        {
            _mapService = mapService ?? throw new ArgumentNullException(nameof(mapService));
            _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));

            _componentManager = new UIComponentManager();
            _statusManager = new StatusDisplayManager();
            _searchManager = new AddressSearchManager(_mapService, _mapControl);

            _searchManager.AddressFound += OnAddressFoundInternal;
            _searchManager.StatusChanged += OnStatusChanged;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _componentManager.CreateControls(this);
            _componentManager.SetupEventHandlers(SearchAddressAsync);
            _statusManager.SetStatusLabel(_componentManager.StatusLabel);
        }

        public async Task SearchAddressAsync()
        {
            string address = _componentManager.AddressText;

            if (string.IsNullOrWhiteSpace(address))
            {
                _statusManager.ShowStatus("Please enter an address", false);
                return;
            }

            try
            {
                _componentManager.SetSearchingState(true);
                _statusManager.ShowStatus("Searching...", false);

                await _searchManager.SearchAddressAsync(address);
            }
            catch (Exception ex)
            {
                _statusManager.ShowStatus($"Error: {ex.Message}", false);
            }
            finally
            {
                _componentManager.SetSearchingState(false);
            }
        }

        private void OnAddressFoundInternal(object sender, AddressFoundEventArgs e)
        {
            _componentManager.AddressText = e.FormattedAddress;
            OnAddressFound(e);
        }

        private void OnStatusChanged(object sender, StatusChangedEventArgs e)
        {
            _statusManager.ShowStatus(e.Message, e.Success);
        }

        protected virtual void OnAddressFound(AddressFoundEventArgs e)
        {
            AddressFound?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Manages UI components and their state
    /// </summary>
    public class UIComponentManager
    {
        private TextBox _addressTextBox;
        private Button _searchButton;
        private Label _statusLabel;

        public string AddressText
        {
            get => _addressTextBox?.Text ?? string.Empty;
            set
            {
                if (_addressTextBox != null)
                    _addressTextBox.Text = value;
            }
        }

        public Label StatusLabel => _statusLabel;

        public void CreateControls(Control parent)
        {
            CreateAddressLabel(parent);
            CreateAddressTextBox(parent);
            CreateSearchButton(parent);
            CreateStatusLabel(parent);

            parent.Size = new Size(350, 50);
        }

        private void CreateAddressLabel(Control parent)
        {
            var addressLabel = new Label
            {
                Text = "Address:",
                Location = new Point(0, 5),
                Size = new Size(70, 20)
            };
            parent.Controls.Add(addressLabel);
        }

        private void CreateAddressTextBox(Control parent)
        {
            _addressTextBox = new TextBox
            {
                Location = new Point(70, 3),
                Size = new Size(200, 20)
            };
            parent.Controls.Add(_addressTextBox);
        }

        private void CreateSearchButton(Control parent)
        {
            _searchButton = new Button
            {
                Text = "Search",
                Location = new Point(280, 1),
                Size = new Size(70, 24)
            };
            parent.Controls.Add(_searchButton);
        }

        private void CreateStatusLabel(Control parent)
        {
            _statusLabel = new Label
            {
                Text = "",
                Location = new Point(0, 30),
                Size = new Size(350, 20),
                ForeColor = Color.Red,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };
            parent.Controls.Add(_statusLabel);
        }

        public void SetupEventHandlers(Func<Task> searchAction)
        {
            _addressTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    searchAction().ConfigureAwait(false);
                }
            };

            _searchButton.Click += async (s, e) => await searchAction();
        }

        public void SetSearchingState(bool searching)
        {
            _searchButton.Enabled = !searching;
            _addressTextBox.Enabled = !searching;
        }
    }

    /// <summary>
    /// Manages search operations and interactions with the map
    /// </summary>
    public class AddressSearchManager
    {
        private readonly MapService _mapService;
        private readonly GMapControl _mapControl;

        public event EventHandler<AddressFoundEventArgs> AddressFound;
        public event EventHandler<StatusChangedEventArgs> StatusChanged;

        public AddressSearchManager(MapService mapService, GMapControl mapControl)
        {
            _mapService = mapService;
            _mapControl = mapControl;
        }

        public async Task SearchAddressAsync(string address)
        {
            var result = await _mapService.GeocodeAddressAsync(address);

            if (!result.HasValue)
            {
                OnStatusChanged("Address not found", false);
                return;
            }

            double latitude = result.Value.Latitude;
            double longitude = result.Value.Longitude;

            string formattedAddress = await GetFormattedAddress(latitude, longitude, address);

            UpdateMapPosition(latitude, longitude);
            AddTemporaryMarker(latitude, longitude);

            OnAddressFound(latitude, longitude, formattedAddress);
            OnStatusChanged("Address found", true);
        }

        private async Task<string> GetFormattedAddress(double latitude, double longitude, string fallbackAddress)
        {
            string formattedAddress = await _mapService.ReverseGeocodeAsync(latitude, longitude);
            return formattedAddress ?? fallbackAddress;
        }

        private void UpdateMapPosition(double latitude, double longitude)
        {
            _mapControl.Position = new PointLatLng(latitude, longitude);
            _mapControl.Zoom = 15;
        }

        private void AddTemporaryMarker(double latitude, double longitude)
        {
            var overlay = new GMapOverlay("searchResult");
            var marker = new GMarkerGoogle(
                new PointLatLng(latitude, longitude),
                GMarkerGoogleType.yellow);

            overlay.Markers.Add(marker);
            _mapControl.Overlays.Add(overlay);

            ScheduleMarkerRemoval(overlay);
        }

        private void ScheduleMarkerRemoval(GMapOverlay overlay)
        {
            Task.Delay(5000).ContinueWith(t =>
            {
                if (_mapControl.IsDisposed)
                    return;

                _mapControl.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _mapControl.Overlays.Remove(overlay);
                        _mapControl.Refresh();
                    }
                    catch
                    {
                        /* Ignore errors during cleanup */
                    }
                }));
            });
        }

        protected virtual void OnAddressFound(double latitude, double longitude, string formattedAddress)
        {
            AddressFound?.Invoke(this, new AddressFoundEventArgs(latitude, longitude, formattedAddress));
        }

        protected virtual void OnStatusChanged(string message, bool success)
        {
            StatusChanged?.Invoke(this, new StatusChangedEventArgs(message, success));
        }
    }

    /// <summary>
    /// Manages the display of status messages
    /// </summary>
    public class StatusDisplayManager
    {
        private Label _statusLabel;

        public void SetStatusLabel(Label statusLabel)
        {
            _statusLabel = statusLabel;
        }

        public void ShowStatus(string message, bool success)
        {
            if (_statusLabel == null)
                return;

            UpdateStatusAppearance(message, success);
            ScheduleStatusHiding();
        }

        private void UpdateStatusAppearance(string message, bool success)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = success ? Color.Green : Color.Red;
            _statusLabel.Visible = true;
        }

        private void ScheduleStatusHiding()
        {
            Task.Delay(5000).ContinueWith(t =>
            {
                if (_statusLabel.IsDisposed)
                    return;

                _statusLabel.BeginInvoke(new Action(() =>
                {
                    _statusLabel.Visible = false;
                }));
            });
        }
    }

    /// <summary>
    /// Event arguments for address found events
    /// </summary>
    public class AddressFoundEventArgs : EventArgs
    {
        public double Latitude { get; }
        public double Longitude { get; }
        public string FormattedAddress { get; }

        public AddressFoundEventArgs(double latitude, double longitude, string formattedAddress)
        {
            Latitude = latitude;
            Longitude = longitude;
            FormattedAddress = formattedAddress;
        }
    }

    /// <summary>
    /// Event arguments for status changed events
    /// </summary>
    public class StatusChangedEventArgs : EventArgs
    {
        public string Message { get; }
        public bool Success { get; }

        public StatusChangedEventArgs(string message, bool success)
        {
            Message = message;
            Success = success;
        }
    }
}