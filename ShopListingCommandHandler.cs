using Eco.Core.Plugins;
using Eco.Gameplay.Components;
using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.Messaging.Chat.Commands;
using Eco.Gameplay.Systems.TextLinks;
using Eco.Shared;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ShopListing
{
    [ChatCommandHandler]
    public static class ShopListingCommandHandler
    {
        private const int MaxSellersShown = 6;

        [ChatCommand("Shows the commands available from the Shop Listing mod", "sl")]
        public static void ShopListing() { }

        // 3.2.7: commands are fully synchronous; keep them void instead of fake-async.
        [ChatSubCommand(nameof(ShopListing), "Adds an item to your shopping list. Usage: /sl add <item> <amount> [targetPrice]", ChatAuthorizationLevel.User)]
        public static void Add(User user, string item, string amount, string targetPrice = "")
        {
            if (!float.TryParse(amount, NumberStyles.Float, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
            {
                user.TempServerMessage(Localizer.DoStr("Please provide a positive amount (e.g. /sl add Iron Bar 20)."));
                return;
            }

            float target = 0f;
            if (!string.IsNullOrWhiteSpace(targetPrice) &&
                (!float.TryParse(targetPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out target) || target <= 0))
            {
                user.TempServerMessage(Localizer.DoStr("The target price must be a positive number, or omit it to disable price alerts."));
                return;
            }

            var targetItem = Item.GetItemByString(user, item);
            if (targetItem == null)
            {
                user.TempServerMessage(Localizer.Do($"Could not find an item named \"{item}\"."));
                return;
            }

            // 3.2.1: entries are isolated per owner user. 3.3.2: mutate under the list lock.
            var entries = ShopListingData.Obj.Entries;
            ShoppingEntry? entry = null;
            lock (ShopListingData.Obj.SyncRoot)
            {
                entry = entries.FirstOrDefault(e => e.OwnerUserId == user.Id && e.ItemId == targetItem.TypeID);
                if (entry == null)
                {
                    entry = new ShoppingEntry { OwnerUserId = user.Id, ItemId = targetItem.TypeID, Amount = qty, TargetPrice = target };
                    entries.Add(entry);
                }
                else
                {
                    entry.Amount += qty;
                    if (target > 0f)
                        entry.TargetPrice = target;
                }
                StorageManager.Obj.MarkDirty(ShopListingData.Obj);
            }

            var sellers = ShopListingScanner.FindSellers(user, targetItem.TypeID);
            if (sellers.Count == 0)
            {
                user.TempServerMessage(Localizer.Do($"{targetItem.UILink()} x {Text.StyledNum(qty)} added to your shopping list. No store is currently selling it."));
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(Localizer.Do($"{targetItem.UILink()} x {Text.StyledNum(qty)} added to your shopping list. {Text.StyledNum(sellers.Count)} store(s) sell it:"));
            foreach (var s in sellers.Take(MaxSellersShown))
                sb.AppendLine(Localizer.Do($"- {s.StoreLink} — {Text.StyledNum(s.Price)} each, {QuantityText(s)} in stock, {Text.StyledNum((int)Math.Round(s.Distance))}m away at {s.Position}"));
            if (sellers.Count > MaxSellersShown)
                sb.AppendLine(Localizer.Do($"... and {Text.StyledNum(sellers.Count - MaxSellersShown)} more store(s)."));
            // 3.1.1: proactive price alert when the target price is met.
            TryFirePriceAlert(user, targetItem.UILink(), entry, sellers);
            user.TempServerMessage(sb);
        }

        [ChatSubCommand(nameof(ShopListing), "Lists your shopping list and shows in real time where each item is sold, at what price and how far away. Usage: /sl list", ChatAuthorizationLevel.User)]
        public static void List(User user)
        {
            var entries = ShopListingData.Obj.Entries.Where(e => e.OwnerUserId == user.Id).ToList();
            if (entries.Count == 0)
            {
                user.TempServerMessage(Localizer.DoStr("There is nothing on your shopping list yet. Add items with /sl add <item> <amount>."));
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(Localizer.DoStr("Your shopping list:"));
            // 3.2.4: a single world scan reused for every entry instead of N full scans.
            var stores = ShopListingScanner.AllEligibleStores(user).ToList();
            var totals = new Dictionary<string, float>(); // 3.1.2 per-currency total
            var alerts = new List<LocString>();              // 3.1.1 collected alerts
            foreach (var e in entries)
            {
                var item = Item.Get(e.ItemId);
                if (item == null)
                    continue;

                var sellers = ShopListingScanner.FindSellersFromStores(stores, user, e.ItemId);
                if (sellers.Count == 0)
                {
                    sb.AppendLine(Localizer.Do($"{item.UILink()} x {Text.StyledNum(e.Amount)} — no store is currently selling it"));
                    continue;
                }

                var best = sellers[0];
                sb.AppendLine(Localizer.Do($"{item.UILink()} x {Text.StyledNum(e.Amount)} — best: {best.StoreLink}, {Text.StyledNum(best.Price)} each, {QuantityText(best)} in stock, {Text.StyledNum((int)Math.Round(best.Distance))}m away at {best.Position} ({Text.StyledNum(sellers.Count)} store(s))"));
                if (!string.IsNullOrEmpty(best.CurrencyName))
                    totals[best.CurrencyName] = totals.GetValueOrDefault(best.CurrencyName) + best.Price * e.Amount;
                TryFirePriceAlert(user, item.UILink(), e, sellers, alerts);
            }
            // 3.1.2: estimated budget summary at the end of the list.
            if (totals.Count > 0)
            {
                sb.AppendLine(Localizer.DoStr("Estimated total:"));
                foreach (var kv in totals)
                    sb.AppendLine(Localizer.Do($"- {Text.StyledNum(kv.Value)} {kv.Key}"));
            }
            foreach (var alert in alerts)
                user.TempServerMessage(alert);
            sb.AppendLine(Localizer.DoStr("Run /sl add <item> <amount> to add more items."));
            user.TempServerMessage(sb);
        }

        [ChatSubCommand(nameof(ShopListing), "Removes an item from your shopping list by item name or list index (1-based). Usage: /sl remove <item|index>", ChatAuthorizationLevel.User)]
        public static void Remove(User user, string target)
        {
            var mine = ShopListingData.Obj.Entries.Where(e => e.OwnerUserId == user.Id).ToList();
            if (int.TryParse(target, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                if (index < 1 || index > mine.Count)
                {
                    user.TempServerMessage(Localizer.DoStr("That index is out of range."));
                    return;
                }

                var removed = mine[index - 1];
                var item = Item.Get(removed.ItemId);
                lock (ShopListingData.Obj.SyncRoot)
                {
                    ShopListingData.Obj.Entries.Remove(removed);
                    StorageManager.Obj.MarkDirty(ShopListingData.Obj);
                }
                user.TempServerMessage(Localizer.Do($"{(item != null ? item.UILink() : Localizer.DoStr("Entry"))} removed from your shopping list."));
                return;
            }

            var targetItem = Item.GetItemByString(user, target);
            if (targetItem == null)
            {
                user.TempServerMessage(Localizer.Do($"Could not find an item named \"{target}\"."));
                return;
            }

            var entry = mine.FirstOrDefault(e => e.ItemId == targetItem.TypeID);
            if (entry == null)
            {
                user.TempServerMessage(Localizer.Do($"{targetItem.UILink()} is not on your shopping list."));
                return;
            }

            lock (ShopListingData.Obj.SyncRoot)
            {
                ShopListingData.Obj.Entries.Remove(entry);
                StorageManager.Obj.MarkDirty(ShopListingData.Obj);
            }
            user.TempServerMessage(Localizer.Do($"{targetItem.UILink()} removed from your shopping list."));
        }

        [ChatSubCommand(nameof(ShopListing), "Clears your shopping list. Usage: /sl clear [--yes]", ChatAuthorizationLevel.User)]
        public static void Clear(User user, string confirm = "")
        {
            var mine = ShopListingData.Obj.Entries.Where(e => e.OwnerUserId == user.Id).ToList();
            if (mine.Count == 0)
            {
                user.TempServerMessage(Localizer.DoStr("Your shopping list is already empty."));
                return;
            }

            // 3.2.5: require an explicit confirmation so a typo cannot wipe a whole list.
            if (!string.Equals(confirm, "--yes", StringComparison.OrdinalIgnoreCase))
            {
                user.TempServerMessage(Localizer.Do($"Your shopping list has {Text.StyledNum(mine.Count)} item(s). Run /sl clear --yes to remove them."));
                return;
            }

            lock (ShopListingData.Obj.SyncRoot)
            {
                ShopListingData.Obj.Entries.RemoveAll(e => e.OwnerUserId == user.Id);
                StorageManager.Obj.MarkDirty(ShopListingData.Obj);
            }
            user.TempServerMessage(Localizer.DoStr("Your shopping list is empty after clearing."));
        }

        [ChatSubCommand(nameof(ShopListing), "Shows this help. Usage: /sl help", ChatAuthorizationLevel.User)]
        public static void Help(User user)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Localizer.DoStr("Welcome to the Shop Listing mod! Keep track of the items you want to buy and find in real time which stores sell them."));
            sb.AppendLine(Localizer.DoStr("- /sl add <item> <amount> [targetPrice] — add an item and see all current sellers (price, stock, distance); set targetPrice to get a proactive alert when someone sells below it"));
            sb.AppendLine(Localizer.DoStr("- /sl list — show the list with the best current deal per item and an estimated total"));
            sb.AppendLine(Localizer.DoStr("- /sl remove <item|index> — remove an entry by name or 1-based index"));
            sb.AppendLine(Localizer.DoStr("- /sl clear --yes — empty your list (requires --yes to confirm)"));
            sb.AppendLine(Localizer.DoStr("- /sl help — show this help"));
            user.TempServerMessage(sb);
        }

        // 3.2.3: special-requirement offers have no stack, display "N/A" instead of a fake 0.
        private static string QuantityText(SellerInfo s)
        {
            return s.HasQuantity ? Text.StyledNum(s.Quantity) : Localizer.DoStr("N/A").ToString();
        }

        // 3.1.1: price alert - notify the player as soon as an item they track has a seller
        // at or below their target price.
        private static void TryFirePriceAlert(User user, LocString itemLink, ShoppingEntry entry, List<SellerInfo> sellers, List<LocString>? collected = null)
        {
            if (entry.TargetPrice <= 0f)
                return;
            var best = sellers.FirstOrDefault(s => s.Price <= entry.TargetPrice);
            if (best == null)
                return;
            var message = Localizer.Do($"PRICE ALERT: {itemLink} is now {Text.StyledNum(best.Price)} at {best.StoreLink}, below your target of {Text.StyledNum(entry.TargetPrice)}.");
            if (collected != null)
                collected.Add(message);
            else
                user.TempServerMessage(message);
        }
    }
}
