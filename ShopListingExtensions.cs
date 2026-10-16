using Eco.Gameplay.Systems.Messaging.Notifications;
using Eco.Gameplay.Players;
using Eco.Shared.Localization;
using Eco.Shared.Services;
using Eco.Shared.Utils;
using System.Text;

namespace ShopListing
{
    /// <summary>Small helpers shared by the Shop Listing mod.</summary>
    public static class ShopListingExtensions
    {
        public static void TempServerMessage(this User user, StringBuilder message, NotificationCategory category = NotificationCategory.Notifications, NotificationStyle style = NotificationStyle.Chat)
        {
            TempServerMessage(user, message.ToStringLoc(), category, style);
        }

        public static void TempServerMessage(this User user, LocString message, NotificationCategory category = NotificationCategory.Notifications, NotificationStyle style = NotificationStyle.Chat)
        {
            NotificationManager.TemporaryServerMessageToPlayer(message, user, category, style);
        }
    }
}
