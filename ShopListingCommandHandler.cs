using Eco.Core.Plugins;
using Eco.Gameplay.Items;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.Messaging.Chat.Commands;
using Eco.Gameplay.Systems.TextLinks;
using Eco.Shared;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Utils;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShopListing
{
    [ChatCommandHandler]
    public static class ShopListingCommandHandler
    {
        private const int MaxSellersShown = 6;

        [ChatCommand("Shows the commands available from the Shop Listing mod", "sl")]
        public static void ShopListing() { }

        [ChatSubCommand(nameof(ShopListing), "Adds an item to your shopping list. Usage: /sl add <item> <amount>", ChatAuthorizationLevel.User)]
        public static async Task Add(User user, string item, string amount)
        {
            if (!float.TryParse(amount, NumberStyles.Float, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
            {
                user.TempServerMessage(Localizer.DoStr("Please provide a positive amount (e.g. /sl add Iron Bar 20)."));
                return;
            }

            var target = Item.GetItemByString(user, item);
            if (target == null)
            {
                user.TempServerMessage(Localizer.Do($"Could not find an item named \"{item}\"."));
                return;
            }

            var entry = ShopListingData.Obj.Entries.FirstOrDefault(e => e.ItemId == target.TypeID);
            if (entry == null)
                ShopListingData.Obj.Entries.Add(new ShoppingEntry { ItemId = target.TypeID, Amount = qty });
            else
                entry.Amount += qty;

            StorageManager.Obj.MarkDirty(ShopListingData.Obj);

            var sellers = ShopListingScanner.FindSellers(user, target.TypeID);
            if (sellers.Count == 0)
            {
                user.TempServerMessage(Localizer.Do($"{target.UILink()} x {Text.StyledNum(qty)} added to your shopping list. No store is currently selling it."));
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(Localizer.Do($"{target.UILink()} x {Text.StyledNum(qty)} added to your shopping list. {Text.StyledNum(sellers.Count)} store(s) sell it:"));
            foreach (var s in sellers.Take(MaxSellersShown))
                sb.AppendLine(Localizer.Do($"- {s.StoreLink} — {Text.StyledNum(s.Price)} each, {Text.StyledNum(s.Quantity)} in stock, {Text.StyledNum((int)Math.Round(s.Distance))}m away"));
            if (sellers.Count > MaxSellersShown)
                sb.AppendLine(Localizer.Do($"... and {Text.StyledNum(sellers.Count - MaxSellersShown)} more store(s)."));
            user.TempServerMessage(sb);
        }

        [ChatSubCommand(nameof(ShopListing), "Lists your shopping list and shows in real time where each item is sold, at what price and how far away. Usage: /sl list", ChatAuthorizationLevel.User)]
        public static async Task List(User user)
        {
            if (ShopListingData.Obj.Entries.Count == 0)
            {
                user.TempServerMessage(Localizer.DoStr("There is nothing on your shopping list yet. Add items with /sl add <item> <amount>."));
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(Localizer.DoStr("Your shopping list:"));
            foreach (var e in ShopListingData.Obj.Entries)
            {
                var item = Item.Get(e.ItemId);
                if (item == null)
                    continue;

                var sellers = ShopListingScanner.FindSellers(user, e.ItemId);
                if (sellers.Count == 0)
                {
                    sb.AppendLine(Localizer.Do($"{item.UILink()} x {Text.StyledNum(e.Amount)} — no store is currently selling it"));
                    continue;
                }

                var best = sellers[0];
                sb.AppendLine(Localizer.Do($"{item.UILink()} x {Text.StyledNum(e.Amount)} — best: {best.StoreLink}, {Text.StyledNum(best.Price)} each, {Text.StyledNum(best.Quantity)} in stock, {Text.StyledNum((int)Math.Round(best.Distance))}m away ({Text.StyledNum(sellers.Count)} store(s))"));
            }
            sb.AppendLine(Localizer.DoStr("Run /sl add <item> <amount> to add more items."));
            user.TempServerMessage(sb);
        }

        [ChatSubCommand(nameof(ShopListing), "Removes an item from your shopping list by item name or list index (1-based). Usage: /sl remove <item|index>", ChatAuthorizationLevel.User)]
        public static async Task Remove(User user, string target)
        {
            if (int.TryParse(target, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                if (index < 1 || index > ShopListingData.Obj.Entries.Count)
                {
                    user.TempServerMessage(Localizer.DoStr("That index is out of range."));
                    return;
                }

                var removed = ShopListingData.Obj.Entries[index - 1];
                var item = Item.Get(removed.ItemId);
                ShopListingData.Obj.Entries.RemoveAt(index - 1);
                StorageManager.Obj.MarkDirty(ShopListingData.Obj);
                user.TempServerMessage(Localizer.Do($"{(item != null ? item.UILink() : Localizer.DoStr("Entry"))} removed from your shopping list."));
                return;
            }

            var targetItem = Item.GetItemByString(user, target);
            if (targetItem == null)
            {
                user.TempServerMessage(Localizer.Do($"Could not find an item named \"{target}\"."));
                return;
            }

            var entry = ShopListingData.Obj.Entries.FirstOrDefault(e => e.ItemId == targetItem.TypeID);
            if (entry == null)
            {
                user.TempServerMessage(Localizer.Do($"{targetItem.UILink()} is not on your shopping list."));
                return;
            }

            ShopListingData.Obj.Entries.Remove(entry);
            StorageManager.Obj.MarkDirty(ShopListingData.Obj);
            user.TempServerMessage(Localizer.Do($"{targetItem.UILink()} removed from your shopping list."));
        }

        [ChatSubCommand(nameof(ShopListing), "Clears your shopping list. Usage: /sl clear", ChatAuthorizationLevel.User)]
        public static async Task Clear(User user)
        {
            ShopListingData.Obj.Entries.Clear();
            StorageManager.Obj.MarkDirty(ShopListingData.Obj);
            user.TempServerMessage(Localizer.DoStr("Your shopping list is empty after clearing."));
        }

        [ChatSubCommand(nameof(ShopListing), "Shows this help. Usage: /sl help", ChatAuthorizationLevel.User)]
        public static async Task Help(User user)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Localizer.DoStr("Welcome to the Shop Listing mod! Keep track of the items you want to buy and find in real time which stores sell them."));
            sb.AppendLine(Localizer.DoStr("- /sl add <item> <amount> — add an item and see all current sellers (price, stock, distance)"));
            sb.AppendLine(Localizer.DoStr("- /sl list — show the list with the best current deal per item"));
            sb.AppendLine(Localizer.DoStr("- /sl remove <item|index> — remove an entry by name or 1-based index"));
            sb.AppendLine(Localizer.DoStr("- /sl clear — empty the list"));
            sb.AppendLine(Localizer.DoStr("- /sl help — show this help"));
            user.TempServerMessage(sb);
        }
    }
}
