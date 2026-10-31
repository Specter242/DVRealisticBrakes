using System.Collections.Generic;
using System.Linq;
using DV.Items;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using RealisticBrakes.Controls;
using UnityEngine;

namespace RealisticBrakes.Patches
{
 [HarmonyPatch(typeof(StartingItemsController), "GetStartingItemsPrefabs")]
    [OptionalPatch]
    internal static class BookletStartingItemPatch
    {
        /// <summary>The save's record that it has been given the booklet; the game keeps the keys it doesn't know as it saves.</summary>
        internal const string GivenKey = "RealisticBrakes_BookletGiven";

        private static void Postfix(SaveGameData data, ref List<StartingItems.StartingItem> __result)
        {
            if (data == null || data.GetBool(GivenKey) == true || __result == null)
                return;
            var template = BrakeBooklet.Template;
            var spec = template != null ? template.GetComponent<InventoryItemSpec>() : null;
            if (spec == null)
                return;
            data.SetBool(GivenKey, true);
            if (__result.Any(i => i.item != null && i.item.ItemPrefabName == BrakeBooklet.PrefabName))
                return;
            // A copy: the list can be the starting items asset's own.
            __result = new List<StartingItems.StartingItem>(__result)
            {
                new StartingItems.StartingItem { item = spec, preferredRelativeSlot = 0, backpackPriority = true, allowDuplicates = true },
            };
        }
    }

    /// <summary>
    /// The game makes a saved item from its prefab by name through Resources, which can't find the booklet: this
    /// makes the booklet from its template as the game makes any saved item, and leaves every other item to the
    /// game. A prefix rather than a transpiler, so that Realistic Firing's redirect of the same load for its own
    /// items still finds it.
    /// </summary>
    [HarmonyPatch(typeof(StartingItemsController), "InstantiateItem")]
    [OptionalPatch]
    internal static class BookletLoadingPatch
    {
        private static readonly AccessTools.FieldRef<StartingItemsController, int> made =
            AccessTools.FieldRefAccess<StartingItemsController, int>("instantiatedItemCount");

        private static bool Prefix(StartingItemsController __instance, StorageItemData itemData,
            HashSet<(ItemSaveData itemSaveData, JObject itemState)> itemSaveDataCollection, HashSet<Rigidbody> itemRigidbodies, ref GameObject __result)
        {
            if (itemData == null || itemData.itemPrefabName != BrakeBooklet.PrefabName || BrakeBooklet.Template == null)
                return true;
            // As the game makes a saved item: high above the world, each apart from the last, held still until all are placed.
            var at = Static("ITEM_INSTANTIATION_SAFETY_POSITION", new Vector3(0f, 5000f, 0f))
                + Static("ITEM_INSTANTIATION_SAFETY_OFFSET", new Vector3(0f, 1.5f, 0f)) * made(__instance)++;
            var booklet = BrakeBooklet.Make(at, Quaternion.identity);
            booklet.GetComponent<InventoryItemSpec>().BelongsToPlayer = itemData.belongsToPlayer;
            var body = booklet.GetComponent<Rigidbody>();
            if (body != null)
            {
                itemRigidbodies.Add(body);
                body.isKinematic = true;
            }
            var saved = booklet.GetComponent<ItemSaveData>();
            if (saved != null)
                itemSaveDataCollection.Add((saved, itemData.state));
            __result = booklet;
            return false;
        }

        private static Vector3 Static(string name, Vector3 otherwise) =>
            AccessTools.Field(typeof(StartingItemsController), name)?.GetValue(null) is Vector3 value ? value : otherwise;
    }
}
