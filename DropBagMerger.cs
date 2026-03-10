using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DropBagMerger", "OpenAI", "1.0.0")]
    [Description("Merges nearby dropped loot into one or more loot bags when too many loose items are on the ground.")]
    public class DropBagMerger : RustPlugin
    {
        private const string BagPrefab = "assets/prefabs/misc/item drop/item_drop_backpack.prefab";

        private Configuration _config;
        private readonly HashSet<uint> _scheduledDrops = new HashSet<uint>();

        private class Configuration
        {
            [JsonProperty(PropertyName = "Merge radius in meters")]
            public float MergeRadius = 2.5f;

            [JsonProperty(PropertyName = "Minimum loose dropped items before creating a bag")]
            public int MinimumLooseItemsForBag = 5;

            [JsonProperty(PropertyName = "Delay before checking a new dropped item (seconds)")]
            public float MergeDelaySeconds = 0.15f;

            [JsonProperty(PropertyName = "Try to merge into an existing nearby loot bag first")]
            public bool MergeIntoExistingBag = true;

            [JsonProperty(PropertyName = "Maximum number of extra bags to create in one merge pass")]
            public int MaxExtraBagsPerPass = 3;

            [JsonProperty(PropertyName = "Debug logging")]
            public bool Debug = false;
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                    throw new Exception("Config file was empty.");
            }
            catch
            {
                PrintWarning("Config is invalid. Creating a new default config file.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void OnServerInitialized()
        {
            if (_config.MergeRadius <= 0f)
                _config.MergeRadius = 2.5f;

            if (_config.MinimumLooseItemsForBag < 2)
                _config.MinimumLooseItemsForBag = 2;

            if (_config.MergeDelaySeconds < 0f)
                _config.MergeDelaySeconds = 0.15f;

            if (_config.MaxExtraBagsPerPass < 0)
                _config.MaxExtraBagsPerPass = 0;

            SaveConfig();
        }

        private void OnItemDropped(Item item, BaseEntity entity)
        {
            var droppedItem = entity as DroppedItem;
            if (item == null || droppedItem == null || droppedItem.IsDestroyed)
                return;

            uint netId = 0u;
            if (droppedItem.net != null)
                netId = droppedItem.net.ID;

            if (netId != 0u && !_scheduledDrops.Add(netId))
                return;

            timer.Once(_config.MergeDelaySeconds, () =>
            {
                if (netId != 0u)
                    _scheduledDrops.Remove(netId);

                TryMergeCluster(droppedItem);
            });
        }

        private void TryMergeCluster(DroppedItem seed)
        {
            if (seed == null || seed.IsDestroyed)
                return;

            Vector3 center = seed.transform.position;
            var nearbyEntities = new List<BaseEntity>();
            Vis.Entities(center, _config.MergeRadius, nearbyEntities);

            var looseItems = new List<DroppedItem>();
            DroppedItemContainer targetBag = null;

            foreach (BaseEntity entity in nearbyEntities)
            {
                if (entity == null || entity.IsDestroyed)
                    continue;

                var droppedItem = entity as DroppedItem;
                if (droppedItem != null)
                {
                    if (droppedItem.item != null && droppedItem.item.info != null)
                        looseItems.Add(droppedItem);
                    continue;
                }

                if (_config.MergeIntoExistingBag && targetBag == null)
                {
                    var droppedBag = entity as DroppedItemContainer;
                    if (droppedBag != null && droppedBag.inventory != null)
                        targetBag = droppedBag;
                }
            }

            if (looseItems.Count == 0)
                return;

            if (targetBag == null && looseItems.Count < _config.MinimumLooseItemsForBag)
                return;

            ulong ownerId = seed.OwnerID;

            int createdBagCount = 0;
            if (targetBag == null)
            {
                targetBag = CreateBag(center, ownerId, createdBagCount);
                createdBagCount++;
            }

            if (targetBag == null)
                return;

            int moved = 0;
            int maxBagsAllowed = 1 + _config.MaxExtraBagsPerPass;

            foreach (DroppedItem dropped in looseItems)
            {
                if (dropped == null || dropped.IsDestroyed || dropped.item == null || dropped.item.info == null)
                    continue;

                Item item = dropped.item;

                if (!TryMoveItemToBag(item, targetBag))
                {
                    if (createdBagCount >= maxBagsAllowed)
                        continue;

                    DroppedItemContainer extraBag = CreateBag(center, ownerId, createdBagCount);
                    if (extraBag == null)
                        continue;

                    createdBagCount++;
                    targetBag = extraBag;

                    if (!TryMoveItemToBag(item, targetBag))
                        continue;
                }

                if (!dropped.IsDestroyed)
                    dropped.Kill();

                moved++;
            }

            if (_config.Debug && moved > 0)
                Puts($"Merged {moved} dropped item entities into {createdBagCount} bag(s) near {center}.");
        }

        private bool TryMoveItemToBag(Item item, DroppedItemContainer bag)
        {
            if (item == null || item.info == null || bag == null || bag.IsDestroyed || bag.inventory == null)
                return false;

            if (!item.MoveToContainer(bag.inventory, -1, true))
                return false;

            bag.SendNetworkUpdateImmediate();
            return true;
        }

        private DroppedItemContainer CreateBag(Vector3 center, ulong ownerId, int bagIndex)
        {
            Vector3 spawnPos = center + new Vector3(0f, 0.15f, 0f);

            if (bagIndex > 0)
            {
                float angle = 45f * bagIndex;
                float radians = angle * Mathf.Deg2Rad;
                spawnPos += new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * 0.35f;
            }

            BaseEntity entity = GameManager.server.CreateEntity(BagPrefab, spawnPos, Quaternion.identity, true);
            if (entity == null)
            {
                PrintWarning("Failed to create dropped item container entity.");
                return null;
            }

            DroppedItemContainer bag = entity as DroppedItemContainer;
            if (bag == null)
            {
                entity.Kill();
                PrintWarning("Created entity was not a DroppedItemContainer. Check the prefab path.");
                return null;
            }

            bag.playerSteamID = ownerId;
            bag.Spawn();
            return bag;
        }
    }
}