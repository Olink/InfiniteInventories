using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace InfiniteInventories
{
	[ApiVersion(1,17)]
	public class InfiniteInventories : TerrariaPlugin
	{
		private readonly IIPlayer[] players = new IIPlayer[255];
		private InventoryDatabase inventoryManager = null;
		private string ConfigPath;
		private Config config = new Config();

		public override string Author
		{
			get { return "Olink"; }
		}

		public override string Description
		{
			get { return "Allows users to have an infinite number of inventories."; }
		}

		public override string Name
		{
			get { return "Infinite Inventories"; }
		}

		public override Version Version
		{
			get { return new Version(1, 0, 0, 0); }
		}

		public InfiniteInventories(Main game) : base(game)
		{
			Order = 2;
		}

		public override void Initialize()
		{
			ConfigPath = Path.Combine(TShock.SavePath, "iiConfig.json");
			if (File.Exists(ConfigPath))
			{
				config = Config.Read(ConfigPath);
			}
			config.Write(ConfigPath);

			if (!TShock.ServerSideCharacterConfig.Enabled)
			{
				throw new Exception("This plugin requires Server Side Characters to be enabled - Aborting!");
			}

			IDbConnection DB = null;
			if (TShock.Config.StorageType.ToLower() == "sqlite")
			{
				string sql = Path.Combine(TShock.SavePath, "infiniteinventories.sqlite");
				DB = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
			}
			else if (TShock.Config.StorageType.ToLower() == "mysql")
			{
				try
				{
					var hostport = TShock.Config.MySqlHost.Split(':');
					DB = new MySqlConnection();
					DB.ConnectionString =
						String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
							hostport[0],
							hostport.Length > 1 ? hostport[1] : "3306",
							"infiniteinventories",
							TShock.Config.MySqlUsername,
							TShock.Config.MySqlPassword
							);
				}
				catch (MySqlException ex)
				{
					ServerApi.LogWriter.PluginWriteLine(this, ex.ToString(), TraceLevel.Error);
					throw new Exception("MySql not setup correctly");
				}
			}

			inventoryManager = new InventoryDatabase(DB);
			BackupInventoryManager.Initialize(DB);

			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
			ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
			TShockAPI.GetDataHandlers.PlayerSlot += OnSlot;
			PlayerHooks.PlayerPostLogin += OnPostLogin;
			AccountHooks.AccountDelete += OnAccountDelete;

			Commands.ChatCommands.Add(new Command("infiniteinventory", SwapInventory, "ii"){AllowServer = false});
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
				ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
				TShockAPI.GetDataHandlers.PlayerSlot -= OnSlot;
				PlayerHooks.PlayerPostLogin -= OnPostLogin;
				AccountHooks.AccountDelete -= OnAccountDelete;
			}
			base.Dispose(disposing);
		}

		private void OnJoin(JoinEventArgs args)
		{
			players[args.Who] = new IIPlayer(TShock.Players[args.Who]);
		}

		private void OnLeave(LeaveEventArgs args)
		{
			var player = players[args.Who];
			if (player.Player.IsLoggedIn)
			{
				player.Save();
				inventoryManager.InsertData(player);
				player.RestoreToVanilla();
			}
			players[args.Who] = null;
		}

		private void OnSlot(object sender, GetDataHandlers.PlayerSlotEventArgs args)
		{
			var player = players[args.PlayerId];
			if (player != null)
			{
				if (player.Player.IsLoggedIn)
				{
					if (player.overflow)
					{
						if (args.Slot == 49)
						{
							args.Handled = true;
							int freeSlot = player.GetOverflowSlot();
							if (freeSlot > -1)
							{
								player.Inventory[player.overflowInv][freeSlot] = new NetItem()
								{
									netID = args.Type,
									prefix = args.Prefix,
									stack = args.Stack
								};
								player.Player.TPlayer.inventory[args.Slot].netDefaults(0);
								player.Player.SendData(PacketTypes.PlayerSlot, "", player.Player.Index, args.Slot);
							}
							else
							{
								player.Player.SendErrorMessage("Your overflow inventory is full, disabling overflow.");
								player.overflow = false;
								player.overflowInv = "";
							}
						}
					}
				}
			}
		}

		private void OnPostLogin(PlayerPostLoginEventArgs args)
		{
			BackupInventoryManager.Restore(players[args.Player.Index]);
			inventoryManager.LoadData(players[args.Player.Index]);
		}

		private void OnAccountDelete(AccountDeleteEventArgs args)
		{
			inventoryManager.RemoveUser(args.User.ID);
		}

		private void SwapInventory(CommandArgs args)
		{
			var player = players[args.Player.Index];
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Usage: /ii swap [name] - swap to inventory");
				args.Player.SendErrorMessage("       /ii reset - return to vanilla inventory");
				args.Player.SendErrorMessage("       /ii clear [name] - empty inventory");
				args.Player.SendErrorMessage("       /ii create [name] - create new inventory");
				args.Player.SendErrorMessage("       /ii delete [name] - delete the inventory");
				args.Player.SendErrorMessage("       /ii overflow [name] - overflow items to inventory, leave blank to turn off.");
				args.Player.SendErrorMessage("       /ii list - list all inventories and how many items in each.");
				args.Player.SendErrorMessage("       /ii find [name or id] - list all inventories that contain the item and how many in that inventory.");
				return;
			}
			switch (args.Parameters[0])
			{
				case "swap":
				{
					if (args.Parameters.Count > 1)
					{
						var invName = String.Join(" ", args.Parameters.ToArray(), 1, args.Parameters.Count - 1);
						if (player.Inventory.ContainsKey(invName))
						{
							player.SwapInventories(invName);
							args.Player.SendSuccessMessage("Your inventory has been swapped.");
						}
					}
					else
					{
						args.Player.SendErrorMessage("Usage: /ii swap [name] - swap to inventory");
					}
				}
				break;
				case "reset":
				{
					player.RestoreToVanilla();
					args.Player.SendSuccessMessage("Your inventory has been restored.");
				}
				break;
				case "clear":
				{
					if (args.Parameters.Count > 1)
					{
						var invName = String.Join(" ", args.Parameters.ToArray(), 1, args.Parameters.Count - 1);
						if (player.Inventory.ContainsKey(invName))
						{
							player.Inventory[invName] = player.CreateInventory();
							args.Player.SendSuccessMessage("Inventory {0} has been cleared.", invName);
						}
					}
					else
					{
						args.Player.SendErrorMessage("Usage: /ii clear [name] - clears inventory");
					}
				}
				break;
				case "create":
				{
					if (args.Parameters.Count > 1)
					{
						if (player.Inventory.Count >= config.MaxInventories)
						{
							args.Player.SendErrorMessage("You already have the maximum number of inventories availabe.");
							return;
						}
						var invName = String.Join(" ", args.Parameters.ToArray(), 1, args.Parameters.Count - 1);
						if (player.Inventory.ContainsKey(invName))
						{
							args.Player.SendErrorMessage("That inventory already exists.");
						}
						else
						{
							player.Inventory.Add(invName, player.CreateInventory());
							inventoryManager.InsertData(player);
							args.Player.SendSuccessMessage("Successfully created inventory.");
						}
					}
					else
					{
						args.Player.SendErrorMessage("Usage: /ii create [name] - create inventory");
					}
				}
				break;
				case "delete":
				{
					if (args.Parameters.Count > 1)
					{
						var invName = String.Join(" ", args.Parameters.ToArray(), 1, args.Parameters.Count - 1);
						if (invName == player.currentInventory)
						{
							args.Player.SendErrorMessage("You can not delete your current inventory, '/ii reset' first.");
							return;
						}
						if (player.Inventory.ContainsKey(invName))
						{
							player.Inventory.Remove(invName);
							inventoryManager.DeleteInventory(player, invName);
							args.Player.SendSuccessMessage("Successfully deleted inventory.");
							if (player.overflowInv == invName)
							{
								player.overflow = false;
								player.overflowInv = "";
								args.Player.SendErrorMessage("You have deleted your overflow inventory.  Overflow is turned off.");
							}
						}
						else
						{
							args.Player.SendErrorMessage("That inventory does not exist.");
						}
					}
					else
					{
						args.Player.SendErrorMessage("Usage: /ii delete [name] - delete inventory");
					}
				}
				break;
				case "overflow":
				{
					if (args.Parameters.Count > 1)
					{
						var invName = String.Join(" ", args.Parameters.ToArray(), 1, args.Parameters.Count - 1);
						if (player.Inventory.ContainsKey(invName))
						{
							player.SetOverflow(invName);
							args.Player.SendSuccessMessage("Overflow items will now be sent to {0}", invName);
						}
						else
						{
							args.Player.SendErrorMessage("That inventory does not exist.");
						}
					}
					else
					{
						if (player.overflow)
						{
							player.overflow = false;
							player.overflowInv = "";
							args.Player.SendSuccessMessage("Overflow has been disabled.");
						}
						else
						{
							args.Player.SendErrorMessage("Usage: /ii overflow [name] - overflow items to inventory, leave blank to turn off.");
						}
					}
				}
				break;
				case "list":
				{
					List<String> invs = player.GetInventoryNames();
					foreach (var invName in invs)
					{
						args.Player.SendInfoMessage("Inventory {0}: {1}/50", invName, player.GetInventoryUsage(invName));
					}
				}
				break;
				case "find":
				{
					if (args.Parameters.Count > 1)
					{
						var itemSearch = String.Join(" ", args.Parameters.ToArray(), 1, args.Parameters.Count - 1);
						List<Item> items = TShock.Utils.GetItemByIdOrName(itemSearch);
						if (items.Count > 1)
						{
							TShock.Utils.SendMultipleMatchError(args.Player, items.Select(i => i.name));
							return;
						}

						if (items.Count == 0)
						{
							args.Player.SendErrorMessage("Failed to find an item with id or name of '{0}'.", itemSearch);
							return;
						}

						Dictionary<String, int> found = player.FindItem(items[0]);
						if (found.Count > 0)
						{
							foreach (var f in found)
							{
								args.Player.SendInfoMessage("Found {0} {1} in {2}.", f.Value, itemSearch, f.Key);
							}
						}
						else
						{
							args.Player.SendErrorMessage("Could not find any {0} in your inventories.", items[0].name);
						}
					}
					else
					{
						args.Player.SendErrorMessage("Usage: /ii find [name or id] - list all inventories that contain the item and how many in that inventory.");
					}
				}
				break;
				default:
				{
					args.Player.SendErrorMessage("Usage: /ii swap [name] - swap to inventory");
					args.Player.SendErrorMessage("       /ii reset - return to vanilla inventory");
					args.Player.SendErrorMessage("       /ii clear [name] - empty inventory");
					args.Player.SendErrorMessage("       /ii create [name] - create new inventory");
					args.Player.SendErrorMessage("       /ii delete [name] - delete the inventory");
					args.Player.SendErrorMessage("       /ii overflow [name] - overflow items to inventory, leave blank to turn off.");
					args.Player.SendErrorMessage("       /ii list - list all inventories and how many items in each.");
					args.Player.SendErrorMessage("       /ii find [name or id] - list all inventories that contain the item and how many in that inventory.");
				}
				break;
			}
		}
	}
}
