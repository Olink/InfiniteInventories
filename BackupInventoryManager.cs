using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using MySql.Data.MySqlClient;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace InfiniteInventories
{
	class BackupInventoryManager
	{
		private static BackupInventoryManager instance;
		private IDbConnection database;

		private BackupInventoryManager(IDbConnection db)
		{
			database = db;
			var table = new SqlTable("BackupInventory",
				new SqlColumn("id", MySqlDbType.Int32) { Primary = true, Unique = true },
				new SqlColumn("inventory", MySqlDbType.Text));

			var creator = new SqlTableCreator(db,
											  db.GetSqlType() == SqlType.Sqlite
												? (IQueryBuilder)new SqliteQueryCreator()
												: new MysqlQueryCreator());
			creator.EnsureTableStructure(table);
		}

		public static void Initialize(IDbConnection db)
		{
			if (instance == null)
			{
				instance = new BackupInventoryManager(db);
			}
		}

		private void BackupInventory(IIPlayer player, NetItem[] inv)
		{
			try
			{
				database.Query("INSERT OR IGNORE INTO BackupInventory (id, inventory) VALUES (@0, @1)", player.Player.UserID, NetItem.ToString(inv));
			}
			catch (SqlException ex)
			{
				TShock.Log.ConsoleError("Failed to insert backup data for {0}: {1}", player.Player.UserAccountName, ex.Message);
			}
		}

		private void RestoreInventory(IIPlayer player)
		{
			bool delete = false;
			try
			{
				using (var reader = database.QueryReader("SELECT * from BackupInventory WHERE id=@0", player.Player.UserID))
				{
					if (reader.Read())
					{
						NetItem[] inventory = NetItem.Parse(reader.Get<string>("inventory"));
						for (int i = 0; i < 50; i++)
						{
							player.Player.PlayerData.inventory[i] = inventory[i];
							var it = new Item();
							it.netDefaults(inventory[i].netID);
							if (it.netID != 0)
							{
								it.stack = inventory[i].stack;
								it.Prefix(inventory[i].prefix);
							}

							player.Player.TPlayer.inventory[i] = it;
						}
						player.Player.SaveServerCharacter();
						delete = true;
					}
				}
			}
			catch (SqlException ex)
			{
				TShock.Log.ConsoleError("Failed to restore backup data for {0}: {1}", player.Player.UserAccountName, ex.Message);
			}

			if (delete)
			{
				try
				{
					database.Query("DELETE FROM BackupInventory WHERE id=@0", player.Player.UserID);
				}
				catch (SqlException ex)
				{
					TShock.Log.ConsoleError("Failed to remove backup data for {0}: {1}", player.Player.UserAccountName, ex.Message);
				}
			}
		}

		public static void Backup(IIPlayer player, NetItem[] inv)
		{
			instance.BackupInventory(player, inv);
		}

		public static void Restore(IIPlayer player)
		{
			instance.RestoreInventory(player);
		}
	}
}
