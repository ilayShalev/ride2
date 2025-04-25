using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.Services.DatabaseServiceClasses
{
    /// <summary>
    /// Inserts default data into the database
    /// </summary>
    public class DefaultDataInserter
    {
        private readonly SQLiteConnection _connection;
        private readonly SecurityHelper _securityHelper;

        public DefaultDataInserter(SQLiteConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _securityHelper = new SecurityHelper();
        }

        public void InsertDefaultData()
        {
            InsertDefaultAdmin();
            InsertDefaultDestination();
        }

        private void InsertDefaultAdmin()
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = @"
                    INSERT INTO Users (Username, Password, UserType, Name)
                    VALUES (@Username, @Password, @UserType, @Name)";
                cmd.Parameters.AddWithValue("@Username", "admin");
                cmd.Parameters.AddWithValue("@Password", _securityHelper.HashPassword("admin"));
                cmd.Parameters.AddWithValue("@UserType", "Admin");
                cmd.Parameters.AddWithValue("@Name", "Administrator");
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertDefaultDestination()
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = @"
                    INSERT INTO Destination (Name, Latitude, Longitude, TargetArrivalTime)
                    VALUES (@Name, @Latitude, @Longitude, @TargetArrivalTime)";
                cmd.Parameters.AddWithValue("@Name", "School");
                cmd.Parameters.AddWithValue("@Latitude", 32.0741);
                cmd.Parameters.AddWithValue("@Longitude", 34.7922);
                cmd.Parameters.AddWithValue("@TargetArrivalTime", "08:00:00");
                cmd.ExecuteNonQuery();
            }
        }
    }

}
