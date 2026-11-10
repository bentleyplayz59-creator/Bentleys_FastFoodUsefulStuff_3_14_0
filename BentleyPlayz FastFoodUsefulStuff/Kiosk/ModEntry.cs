using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace BentleyPlayz.FastFoodUsefulStuff.Kiosk
{
    /// <summary>Right-click the placed Bentley's Restaurant Kiosk to choose Desserts or Food.
    /// Opens the existing shops from Content Patcher (nothing is duplicated) and gives
    /// three 10%-off purchases a day, only through the kiosk.</summary>
    public class ModEntry : Mod
    {
        private const string ModId = "BentleyPlayz.FastFoodUsefulStuff";
        private const string KioskQualifiedId = "(BC)" + ModId + "_RestaurantKiosk";
        private const string FoodShopId = ModId + "_BentleyShop";
        private const string DessertShopId = ModId + "_DessertKiosk";
        private const string DiscountDataKey = ModId + "/KioskDiscountDay";
        private const int DealsPerDay = 3;
        private const float DiscountFraction = 0.10f;
        private const float MaxReachTiles = 2.5f;

        private int lastOpenTick = -100;

        public override void Entry(IModHelper helper)
        {
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // Only act when the player is free to move (no menu, event or cutscene).
            if (!Context.IsWorldReady || !Context.IsPlayerFree || !e.Button.IsActionButton())
                return;

            GameLocation location = Game1.currentLocation;
            if (location == null)
                return;

            Vector2 tile = e.Cursor.GrabTile;
            if (!location.Objects.TryGetValue(tile, out StardewValley.Object obj) || obj == null || obj.QualifiedItemId != KioskQualifiedId)
                return;

            if (Vector2.Distance(Game1.player.Tile, tile) > MaxReachTiles)
                return;

            // This click belongs to the kiosk: stop the game from also acting on it.
            this.Helper.Input.Suppress(e.Button);

            // One click must open one menu.
            if (Game1.ticks - this.lastOpenTick < 20)
                return;
            this.lastOpenTick = Game1.ticks;

            Response[] choices =
            {
                new Response("Desserts", "Desserts"),
                new Response("Food", "Food"),
                new Response("Cancel", "Cancel")
            };
            location.createQuestionDialogue("Desserts or Food?", choices, this.OnChoice);
        }

        private void OnChoice(Farmer who, string answer)
        {
            if (who == null || !who.IsLocalPlayer)
                return;

            string shopId = answer switch
            {
                "Desserts" => DessertShopId,
                "Food" => FoodShopId,
                _ => null
            };
            if (shopId == null)
                return; // Cancel, or the dialogue was closed

            try
            {
                // The shop owner entries in Content Patcher use "AnyOrNone" with Bentley's portrait,
                // so this shows the same portrait and stock as the restaurant counters.
                if (!Utility.TryOpenShopMenu(shopId, (string)null, true))
                {
                    this.Monitor.Log($"Shop '{shopId}' could not be opened. Is the Content part installed?", LogLevel.Warn);
                    return;
                }

                if (Game1.activeClickableMenu is ShopMenu menu)
                    this.ApplyDeals(menu);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Could not open kiosk shop '{shopId}': {ex}", LogLevel.Error);
            }
        }

        // ---- Daily deals -----------------------------------------------------------------

        private int DealsLeft()
        {
            string today = Game1.Date.TotalDays.ToString(CultureInfo.InvariantCulture);
            if (Game1.player.modData.TryGetValue(DiscountDataKey, out string saved) && saved != null)
            {
                string[] parts = saved.Split(':');
                if (parts.Length == 2 && parts[0] == today && int.TryParse(parts[1], out int used))
                    return Math.Max(0, DealsPerDay - used);
            }
            return DealsPerDay;
        }

        private void SetDealsLeft(int left)
        {
            int used = DealsPerDay - Math.Max(0, Math.Min(DealsPerDay, left));
            string today = Game1.Date.TotalDays.ToString(CultureInfo.InvariantCulture);
            Game1.player.modData[DiscountDataKey] = today + ":" + used.ToString(CultureInfo.InvariantCulture);
        }

        private void ApplyDeals(ShopMenu menu)
        {
            int left = this.DealsLeft();
            if (left <= 0)
            {
                Game1.addHUDMessage(new HUDMessage("No kiosk deals left today. Come back tomorrow!", HUDMessage.newQuest_type));
                return;
            }

            // Remember the normal prices so they can be restored when the deals run out.
            var originalPrices = new Dictionary<ISalable, int>();
            foreach (ISalable item in menu.itemPriceAndStock.Keys.ToList())
            {
                ItemStockInformation info = menu.itemPriceAndStock[item];
                // Only discount plain money prices (skip trade-item prices).
                if (info.TradeItem != null || info.Price <= 0)
                    continue;
                originalPrices[item] = info.Price;
            }

            this.SetPrices(menu, originalPrices, discounted: true);
            Game1.addHUDMessage(new HUDMessage($"Kiosk deal: 10% off, {left} left today.", HUDMessage.newQuest_type));

            ShopMenu.OnPurchaseDelegate previous = menu.onPurchase;
            menu.onPurchase = (item, farmer, count, stockInfo) =>
            {
                bool result = previous != null && previous(item, farmer, count, stockInfo);
                try
                {
                    if (item != null && originalPrices.TryGetValue(item, out int original))
                    {
                        int dealsLeft = this.DealsLeft();
                        int discountedUnit = DiscountedPrice(original);
                        if (dealsLeft > 0 && count > 0)
                        {
                            int dealsUsed = Math.Min(count, dealsLeft);
                            int extraUnits = count - dealsUsed;
                            // Units beyond the remaining deals were charged the discounted price: give that part back.
                            if (extraUnits > 0)
                                farmer.Money += extraUnits * (original - discountedUnit);
                            this.SetDealsLeft(dealsLeft - dealsUsed);
                            if (dealsLeft - dealsUsed <= 0)
                            {
                                this.SetPrices(menu, originalPrices, discounted: false);
                                Game1.addHUDMessage(new HUDMessage("That was your last kiosk deal today.", HUDMessage.newQuest_type));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"Could not update kiosk deals: {ex}", LogLevel.Error);
                }
                return result;
            };
        }

        private void SetPrices(ShopMenu menu, Dictionary<ISalable, int> originalPrices, bool discounted)
        {
            foreach (KeyValuePair<ISalable, int> pair in originalPrices)
            {
                if (!menu.itemPriceAndStock.TryGetValue(pair.Key, out ItemStockInformation info))
                    continue;
                info.Price = discounted ? DiscountedPrice(pair.Value) : pair.Value;
                menu.itemPriceAndStock[pair.Key] = info;
            }
        }

        private static int DiscountedPrice(int price)
        {
            return Math.Max(1, (int)Math.Round(price * (1f - DiscountFraction), MidpointRounding.AwayFromZero));
        }
    }
}
