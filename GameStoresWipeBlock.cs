// #define TESTING

using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace Oxide.Plugins
{
	[Info("GameStores Wipe Block", "Mevent", "1.0.3")]
	public class GameStoresWipeBlock : RustPlugin
	{
		#region Fields
		
		private BlockedCache _blockedCache = new BlockedCache();
		
		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Time Indent (seconds)")]
			public float Indent;

			[JsonProperty(PropertyName = "Blocked Items", ObjectCreationHandling = ObjectCreationHandling.Replace)] 
			public Dictionary<ulong, BlockedItem> BlockedItems = new Dictionary<ulong, BlockedItem>
			{
				[600] = new BlockedItem
				{
					ProductIDs = new HashSet<string>
					{
						"0000000"
					},
					ItemIDs = new HashSet<long>
					{
						11111111
					},
					BlueprintItemIDs = new HashSet<long>
					{
						22222222
					}
				},
				[3600] = new BlockedItem
				{
					ProductIDs = new HashSet<string>
					{
						"0000000"
					},
					ItemIDs = new HashSet<long>
					{
						11111111
					},
					BlueprintItemIDs = new HashSet<long>
					{
						22222222
					}
				},
				[7200] = new BlockedItem
				{
					ProductIDs = new HashSet<string>
					{
						"0000000"
					},
					ItemIDs = new HashSet<long>
					{
						11111111
					},
					BlueprintItemIDs = new HashSet<long>
					{
						22222222
					}
				}
			};
		}

		private class BlockedItem
		{
			#region Fields

			[JsonProperty(PropertyName = "Product ID", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public HashSet<string> ProductIDs = new HashSet<string>();
			
			[JsonProperty(PropertyName = "Item ID", ObjectCreationHandling = ObjectCreationHandling.Replace)] 
			public HashSet<long> ItemIDs = new HashSet<long>();
			
			[JsonProperty(PropertyName = "Blueprint Item ID", ObjectCreationHandling = ObjectCreationHandling.Replace)] 
			public HashSet<long> BlueprintItemIDs = new HashSet<long>();

			#endregion

			#region Methods

			[JsonIgnore] public ulong cooldown;

			public void Init(ulong cd)
			{
				cooldown = cd;
			}

			#endregion
		}
		
		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null) throw new Exception();
				SaveConfig();
			}
			catch (Exception ex)
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
				Debug.LogException(ex);
			}
		}

		protected override void SaveConfig() => Config.WriteObject(_config);

		protected override void LoadDefaultConfig() => _config = new Configuration();

		#endregion

		#region Hooks

		private void OnServerInitialized()
		{
			CacheBlockedItems();
		}

		#endregion
		
		#region Commands

		[ConsoleCommand("gs.wipeblock.indent")]
		private void CmdWipeBlockIndent(ConsoleSystem.Arg arg)
		{
			if (!arg.IsServerside) return;
			
			var newIndent = arg.GetFloat(0);
			if (newIndent <= 0)
			{
				arg.ReplyWith("Invalid indent value. Usage: gs.wipeblock.indent <indent>");
				return;
			}
			
			_config.Indent = newIndent;
			SaveConfig();
			
			arg.ReplyWith($"Indent set to: {newIndent} seconds");
			
			CacheBlockedItems();
		}
		
		#endregion

		#region Utils

		private void CacheBlockedItems()
		{
			_blockedCache.Clear();

			var secondsAfterWipe = SecondsFromWipe();

			foreach (var blockedItem in _config.BlockedItems)
			{
				if (blockedItem.Key < secondsAfterWipe) continue;

				foreach (var productID in blockedItem.Value.ProductIDs)
				{
					if (_blockedCache.productIDs.ContainsKey(productID))
					{
						_blockedCache.productIDs[productID] =
							Math.Max(_blockedCache.productIDs[productID], blockedItem.Key);
					}
					else
					{
						_blockedCache.productIDs.TryAdd(productID, blockedItem.Key);
					}
				}

				foreach (var itemID in blockedItem.Value.ItemIDs)
				{
					if (_blockedCache.itemIDs.ContainsKey(itemID))
					{
						_blockedCache.itemIDs[itemID] = Math.Max(_blockedCache.itemIDs[itemID], blockedItem.Key);
					}
					else
					{
						_blockedCache.itemIDs.TryAdd(itemID, blockedItem.Key);
					}
				}

				foreach (var blueprintItemID in blockedItem.Value.BlueprintItemIDs)
				{
					if (_blockedCache.blueprintIDs.ContainsKey(blueprintItemID))
					{
						_blockedCache.blueprintIDs[blueprintItemID] =
							Math.Max(_blockedCache.blueprintIDs[blueprintItemID], blockedItem.Key);
					}
					else
					{
						_blockedCache.blueprintIDs.TryAdd(blueprintItemID, blockedItem.Key);
					}
				}
			}
		}

		#region Classes
		
		private class BlockedCache
		{
			public Dictionary<string, ulong> productIDs = new Dictionary<string, ulong>();
			
			public Dictionary<long, ulong> itemIDs = new Dictionary<long, ulong>();
			
			public Dictionary<long, ulong> blueprintIDs = new Dictionary<long, ulong>();

			public void Clear()
			{
				productIDs.Clear();
				
				itemIDs.Clear();
				
				blueprintIDs.Clear();
			}
		}

		#endregion
		
		private double SecondsFromWipe()
		{
#if TESTING
			return 3600;
#endif
			
			return DateTime.UtcNow.Subtract(SaveRestore.SaveCreatedTime.ToUniversalTime().AddSeconds(_config.Indent)).TotalSeconds;
		}
		
		private bool IsBlocked(string productId, int ItemID, bool isBlueprint)
		{
			var blockedTime = GetBlockedItem(productId, ItemID, isBlueprint);
			if (blockedTime == 0) return false;

			return SecondsFromWipe() < blockedTime;
		}

		private double GetLeftTime(string productId, int ItemID, bool isBlueprint)
		{
			var blockedTime = GetBlockedItem(productId, ItemID, isBlueprint);
			if (blockedTime == 0) return 0;

			return LeftTime(blockedTime);
		}
		
		private double LeftTime(ulong blockedTime)
		{
			var seconds = blockedTime - SecondsFromWipe();
			return seconds < 0 ? 0 : seconds;
		}
		
		private ulong GetBlockedItem(string productId, int ItemID, bool isBlueprint)
		{
			ulong productBlockTime = 0, itemBlockTime = 0;
			_blockedCache.productIDs.TryGetValue(productId, out productBlockTime);

			if (isBlueprint)
				_blockedCache.blueprintIDs.TryGetValue(ItemID, out itemBlockTime);
			else
				_blockedCache.itemIDs.TryGetValue(ItemID, out itemBlockTime);
			
			return Math.Max(productBlockTime, itemBlockTime);
		}

		#endregion

		#region Testing Functions

#if TESTING
		private static void SayDebug(string message)
		{
			Debug.Log($"[GameStoresWipeBlock] {message}");
		}
#endif

		#endregion
	}
}