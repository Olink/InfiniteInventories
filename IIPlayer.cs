using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;

namespace InfiniteInventories
{
	internal class IIPlayer
	{
		public readonly Dictionary<String, NetItem[]> Inventory = new Dictionary<String, NetItem[]>();
		public string currentInventory = "";
		public bool overflow = true;
		public string overflowInv = "";
		public TSPlayer Player { get; private set; }

		public NetItem[] CreateInventory()
		{
			var inv = new NetItem[50];
			for (int i = 0; i < 50; i++)
			{
				inv[i] = new NetItem();
			}
			return inv;
		}

		public IIPlayer(TSPlayer player)
		{
			Player = player;
		}

		private void SetTplayerInventory(NetItem item, int slot)
		{
			var it = new Item();
			it.netDefaults(item.netID);
			if (it.netID != 0)
			{
				it.stack = item.stack;
				it.Prefix(item.prefix);
			}

			Player.TPlayer.inventory[slot] = it;
		}

		public void SwapInventories(string name)
		{
			if (!String.IsNullOrWhiteSpace(currentInventory))
			{
				Save();
				currentInventory = "";
			}
			else
			{
				BackupInventoryManager.Backup(this, Player.PlayerData.inventory);
			}
			
			for(int i = 0; i < 50; i++)
				Player.PlayerData.inventory[i] = Inventory[name][i];

			RefreshInventory();

			currentInventory = name;
		}

		private void RefreshInventory()
		{
			for (int i = 0; i < 50; i++)
			{
				SetTplayerInventory(Player.PlayerData.inventory[i], i);
				Player.SendData(PacketTypes.PlayerSlot, "", Player.Index, (int)i);
			}
		}

		public void Save()
		{
			if (!String.IsNullOrWhiteSpace(currentInventory))
			{
				for (int i = 0; i < 50; i++)
				{
					Inventory[currentInventory][i] = Player.PlayerData.inventory[i];
				}
			}
		}

		public void RestoreToVanilla()
		{
			if (!String.IsNullOrWhiteSpace(currentInventory))
			{
				Save();
				currentInventory = "";
			}
			BackupInventoryManager.Restore(this);
			RefreshInventory();
		}

		public void SetOverflow(string invName)
		{
			overflow = true;
			overflowInv = invName;
		}

		public int GetOverflowSlot()
		{
			for (int i = 0; i < 50; i++)
			{
				if (Inventory[overflowInv][i].netID == 0)
					return i;
			}
			return -1;
		}
	}
}
