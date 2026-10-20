using Eco.Gameplay.Components;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Items;
using Eco.Gameplay.Objects;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.TextLinks;
using Eco.Shared;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Math;
using Eco.Shared.Networking;
using Eco.Shared.Utils;
using Eco.Shared.Voxel;
using System.Collections.Generic;
using System.Linq;

namespace ShopListing
{
    /// <summary>One store offer found while scanning the world for an item.</summary>
    public sealed class SellerInfo
    {
        public Item Item { get; init; } = null!;
        public LocString StoreLink { get; init; }
        public string StoreName { get; init; } = string.Empty;
        public string CurrencyName { get; init; } = string.Empty;
        public float Price { get; init; }
        public float Quantity { get; init; }
        // 3.2.3: special-requirement offers have no ItemStack, so quantity is unknown -> N/A.
        public bool HasQuantity { get; init; } = true;
        public float Distance { get; init; }
        // 3.1.3: store position for navigation / map integration.
        public WrappedPosition3 Position { get; init; }
    }

    /// <summary>
    /// Scans every player store in the world and returns the stores currently selling a given item,
    /// together with the price, available stock and the distance from the requesting player.
    /// </summary>
    public static class ShopListingScanner
    {
        // 3.2.4: cap the result set so a huge world cannot produce unbounded lists.
        private const int MaxSellersPerItem = 100;

        public static List<SellerInfo> FindSellers(User user, int itemId, int limit = MaxSellersPerItem)
        {
            var item = Item.Get(itemId);
            if (item == null)
                return new List<SellerInfo>();

            var playerPos = user.Player.WorldPos();
            return WorldObjectUtil.AllObjsWithComponent<StoreComponent>()
                .Where(s => IsEligibleStore(s, user))
                .SelectMany(s => s.StoreData!.SellOffers
                    .Where(o => MatchesItem(o, itemId))
                    .Select(o => BuildSellerInfo(item, s, o, playerPos)))
                .OrderBy(x => x.Price)
                .ThenBy(x => x.Distance)
                .Take(limit)
                .ToList();
        }

        /// <summary>3.2.4: variant that reuses a single world scan for several items (/sl list).</summary>
        public static List<SellerInfo> FindSellersFromStores(IEnumerable<StoreComponent> stores, User user, int itemId, int limit = MaxSellersPerItem)
        {
            var item = Item.Get(itemId);
            if (item == null)
                return new List<SellerInfo>();

            var playerPos = user.Player.WorldPos();
            return stores
                .Where(s => IsEligibleStore(s, user))
                .SelectMany(s => s.StoreData!.SellOffers
                    .Where(o => MatchesItem(o, itemId))
                    .Select(o => BuildSellerInfo(item, s, o, playerPos)))
                .OrderBy(x => x.Price)
                .ThenBy(x => x.Distance)
                .Take(limit)
                .ToList();
        }

        /// <summary>All stores the given user may buy from, in one world scan.</summary>
        public static IEnumerable<StoreComponent> AllEligibleStores(User user)
        {
            return WorldObjectUtil.AllObjsWithComponent<StoreComponent>().Where(s => IsEligibleStore(s, user));
        }

        // 3.2.2 / 3.3.1: filter stores that are disabled, have no currency or that the player
        // cannot consume from (mirrors the OpenNutriView / TradeAssistant store filtering).
        private static bool IsEligibleStore(StoreComponent store, User user)
        {
            return store.StoreData != null
                && store.Enabled
                && store.Currency != null
                && store.IsRPCAuthorized(user.Player, AccessType.ConsumerAccess, System.Array.Empty<object>());
        }

        private static SellerInfo BuildSellerInfo(Item item, StoreComponent store, TradeOffer offer, WrappedPosition3 playerPos)
        {
            var hasQuantity = offer.Stack != null;
            return new SellerInfo
            {
                Item = item,
                StoreName = store.Parent.Name,
                StoreLink = store.Parent.UILink(),
                CurrencyName = store.Currency?.Name ?? string.Empty,
                Price = offer.Price,
                Quantity = hasQuantity ? offer.Stack!.Quantity : 0,
                HasQuantity = hasQuantity,
                Distance = World.WrappedDistance(playerPos, store.Parent.WorldPos()),
                Position = store.Parent.WorldPos()
            };
        }

        private static bool MatchesItem(TradeOffer offer, int itemId)
        {
            if (offer.Stack?.Item != null)
                return offer.Stack.Item.TypeID == itemId;

            return offer.MeetsSpecialRequirements(Item.Get(itemId));
        }
    }
}
