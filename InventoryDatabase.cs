using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Xml.Schema;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace InfiniteInventories
{
	class InventoryDatabase
	{
		private IDbConnection database;

		public InventoryDatabase(IDbConnection db)
		{
			database = db;
			var table = new SqlTable("InfiniteInventories",
				new SqlColumn("id", MySqlDbType.Int32) {Primary = true, AutoIncrement = true},
				new SqlColumn("userID", MySqlDbType.Int32) {Unique = true},
				new SqlColumn("name", MySqlDbType.VarChar, 52) {Unique = true},
				new SqlColumn("inventory", MySqlDbType.Text));

			var creator = new SqlTableCreator(db,
											  db.GetSqlType() == SqlType.Sqlite
												? (IQueryBuilder)new SqliteQueryCreator()
												: new MysqlQueryCreator());
			creator.EnsureTableStructure(table);
		}

		//10,1,0~20,1,0~30,1,0~40,1,0~122,1,0~2,27,0~3,17,0~133,24,0~12,8,0~965,19,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~965,1,0~965,1,0~965,1,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~0,0,0~23,2,0
		public void LoadData(IIPlayer player)
		{
			try
			{
				using (var reader = database.QueryReader("SELECT * FROM InfiniteInventories WHERE userID=@0", player.Player.UserID))
				{
					player.Inventory.Clear();
					while (reader.Read())
					{
						String name = reader.Get<string>("name");
						NetItem[] inv = FromString(reader.Get<string>("inventory"));
						NetItem[] realInv = new NetItem[50];
						for (int i = 0; i < Math.Min(50, inv.Length); i++)
						{
							realInv[i] = inv[i];
						}
						player.Inventory.Add(name, realInv);
					}
				}
			}
			catch (SqlException ex)
			{
				TShock.Log.ConsoleError("Failed to load II for player {0}: {1}", player.Player.UserAccountName, ex.Message);
			}
		}

		public void InsertData(IIPlayer player)
		{
			try
			{
				foreach (var inventory in player.Inventory)
				{
					if (database.Query("INSERT OR IGNORE INTO InfiniteInventories (userID, name, inventory) VALUES (@0, @1, @2)", player.Player.UserID,
						inventory.Key, ToString(inventory.Value)) == 0)
					{
						database.Query("UPDATE InfiniteInventories SET inventory=@0 WHERE userID = @1 AND name = @2",
							ToString(inventory.Value), player.Player.UserID, inventory.Key);
					}
				}
			}
			catch (SqlException ex)
			{
				TShock.Log.ConsoleError("Failed to load II for player {0}: {1}", player.Player.UserAccountName, ex.Message);
			}
		}

		public void DeleteInventory(IIPlayer player, string name)
		{
			try
			{
				database.Query("DELETE FROM InfiniteInventories WHERE userID = @0 AND name = @1", player.Player.UserID, name);
				player.Inventory.Remove(name);
			}
			catch (SqlException ex)
			{
				TShock.Log.ConsoleError("Failed to delete {2} II for player {0}: {1}", player.Player.UserAccountName, ex.Message, name);
			}
		}

		public void RemoveAccount(User user)
		{
			try
			{
				database.Query("DELETE FROM InfiniteInventories WHERE userID = @0", user.ID);
			}
			catch (SqlException ex)
			{
				TShock.Log.ConsoleError("Failed to remove II for player {0}: {1}", user.Name, ex.Message);
			}
		}

		public string ToString(NetItem[] items)
		{
			StringBuilder builder = new StringBuilder();
			foreach (var item in items)
			{
				if (builder.Length != 0)
					builder.Append("~");

				if(item == null)
					builder.Append("0,0,0");
				else
					builder.Append(item.netID).Append(",").Append(item.stack).Append(",").Append(item.prefix);
			}
			return builder.ToString();
		}

		public NetItem[] FromString(string data)
		{
			NetItem[] inv = new NetItem[50];
			string[] items = data.Split('~');
			for (int i = 0; i < 50; i++)
			{
				if (i < items.Length)
				{
					var item = items[i].Split(',');
					inv[i] = new NetItem() {netID = int.Parse(item[0]), stack = int.Parse(item[1]), prefix = int.Parse(item[2])};
				}
				else
				{
					inv[i] = new NetItem();
				}
			}
			return inv;
		}
	}
}
