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
    /// Base class for all tab controllers
    /// </summary>
    public abstract class TabControllerBase
    {
        protected readonly DatabaseService DbService;
        protected readonly MapService MapService;
        protected readonly AdminDataManager DataManager;

        protected TabControllerBase(DatabaseService dbService, MapService mapService = null, AdminDataManager dataManager = null)
        {
            DbService = dbService;
            MapService = mapService;
            DataManager = dataManager;
        }

        public abstract void InitializeTab(TabPage tabPage);
        public abstract Task RefreshTabAsync();
    }

}
