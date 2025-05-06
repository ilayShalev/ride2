using RideMatchProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Base class for all tab controllers in the admin interface, providing common services and abstract methods
    /// for tab initialization and refreshing.
    /// </summary>
    public abstract class TabControllerBase
    {
        /// <summary>
        /// The database service used for data retrieval and storage operations.
        /// </summary>
        protected readonly DatabaseService DbService;

        /// <summary>
        /// The map service used for map-related operations, such as initializing GMap controls.
        /// </summary>
        protected readonly MapService MapService;

        /// <summary>
        /// The data manager responsible for handling admin-related data operations.
        /// </summary>
        protected readonly AdminDataManager DataManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TabControllerBase"/> class.
        /// </ summary>
        /// <param name="dbService">The database service used for data operations. Must not be null.</param>
        /// <param name="mapService">The map service used for map-related operations. Optional; can be null if not needed.</param>
        /// <param name="dataManager">The data manager for admin-related data operations. Optional; can be null if not needed.</param>
        protected TabControllerBase(DatabaseService dbService, MapService mapService = null, AdminDataManager dataManager = null)
        {
            DbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
            MapService = mapService;
            DataManager = dataManager;
        }

        /// <summary>
        /// Initializes the specified tab by creating and configuring its UI controls.
        /// </summary>
        /// <param name="tabPage">The <see cref="TabPage"/> to initialize with controls.</param>
        public abstract void InitializeTab(TabPage tabPage);

        /// <summary>
        /// Refreshes the tab's content asynchronously, updating data and UI as needed.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public abstract Task RefreshTabAsync();
    }
}