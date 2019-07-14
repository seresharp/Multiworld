using System;
using System.Collections.Generic;
using System.Linq;
using RandomizerMod.Randomization;

namespace MultiWorldServer
{
    public static class MultiworldRandomizer
    {
        /// <summary>
        /// Creates a randomizad game with the given settings
        /// </summary>
        /// <param name="settings">The settings with which to randomize</param>
        /// <returns>A dictionary of item -> item location</returns>
        public static Dictionary<string, (int, string)>[] Randomize(ServerSettings settings)
        {
            int playerCount = settings.Players;

            if (playerCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount.ToString());
            }

            if (!LogicManager.Loaded)
            {
                LogicManager.ParseXML(
                    typeof(LogicManager).Assembly.GetManifestResourceStream("RandomizerMod.Resources.items.xml"));
            }

            Dictionary<string, (int, string)>[] placements = new Dictionary<string, (int, string)>[playerCount];

            // Init random
            Random rnd = new Random(settings.Seed);

            // Init player structs
            Player[] players = new Player[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                placements[i] = new Dictionary<string, (int, string)>();

                players[i] = new Player
                {
                    UnobtainedLocations = new List<string>(),
                    UnobtainedItems = LogicManager.ItemNames.ToList(),
                    ObtainedItems = new List<string>()
                };

                foreach (string itemName in LogicManager.ItemNames)
                {
                    if (LogicManager.GetItemDef(itemName).type != ItemType.Shop)
                    {
                        players[i].UnobtainedLocations.Add(itemName);
                    }
                }

                players[i].UnobtainedLocations.AddRange(LogicManager.ShopNames);

                if (settings.NoClaw)
                {
                    players[i].UnobtainedItems.Remove("Mantis_Claw");
                }
            }

            // Begin randomization
            bool progressionPlaced = false;
            while (true)
            {
                List<int> playersNeedingItems = new List<int>();
                List<int> playersWithLocations = new List<int>();
                for (int i = 0; i < playerCount; i++)
                {
                    if (!progressionPlaced)
                    {
                        players[i].UpdateProgression(settings);

                        if (players[i].Progression.Count > 0)
                        {
                            playersNeedingItems.Add(i);
                        }

                        if (players[i].ReachableLocations.Count > 0)
                        {
                            playersWithLocations.Add(i);
                        }
                    }
                    else
                    {
                        if (players[i].UnobtainedItems.Count > 0)
                        {
                            playersNeedingItems.Add(i);
                        }

                        if (players[i].UnobtainedLocations.Count > 0)
                        {
                            playersWithLocations.Add(i);
                        }
                    }
                }

                // Set flag for full randomness if no progression left
                if (!progressionPlaced && playersNeedingItems.Count == 0)
                {
                    Console.WriteLine("Done placing progression");
                    progressionPlaced = true;
                    continue;
                }

                // Exit loop if all items are placed
                if (playersNeedingItems.Count == 0)
                {
                    break;
                }

                // Choose item and location
                int playerToGiveItem =
                    playersNeedingItems[
                        InverseWeightedRandom(rnd, playersNeedingItems.Select(i => players[i]).ToArray())];

                // If no players have locations, all that's left is shops
                // At this point, just choose fully randomly
                int worldContainingItem = playersWithLocations.Count > 0
                    ? playersWithLocations.GetRandom(rnd)
                    : rnd.Next(playerCount);

                string itemToGive;
                string itemLocation;

                if (!progressionPlaced)
                {
                    itemToGive = players[playerToGiveItem].Progression.GetRandom(rnd);
                    itemLocation = players[worldContainingItem].ReachableLocations.GetRandom(rnd);
                }
                else
                {
                    itemToGive = players[playerToGiveItem].UnobtainedItems.GetRandom(rnd);

                    // If there's no available locations, pick a shop instead
                    itemLocation = players[worldContainingItem].UnobtainedLocations.Count > 0
                        ? players[worldContainingItem].UnobtainedLocations.GetRandom(rnd)
                        : LogicManager.ShopNames.GetRandom(rnd);
                }

                // Give item, remove location
                players[playerToGiveItem].GiveItem(itemToGive);
                players[worldContainingItem].TakeLocation(itemLocation);

                if (LogicManager.ShopNames.Contains(itemLocation))
                {
                    // Shop names aren't unique, loop until we have a unique index for this shop
                    // TODO: Make this better
                    int index = 0;
                    while (placements[worldContainingItem].ContainsKey(itemLocation + "_" + index))
                    {
                        index++;
                    }

                    itemLocation = itemLocation + "_" + index;
                }

                placements[worldContainingItem].Add(itemLocation, (playerToGiveItem, itemToGive));

                Console.WriteLine($"Placing {itemToGive} ({playerToGiveItem}) at {itemLocation} ({worldContainingItem})");
            }

            return placements;
        }

        private static int InverseWeightedRandom(Random rnd, Player[] players)
        {
            if (players.Length == 1)
            {
                return 0;
            }

            int lcm = MathHelper.LCM(players.Select(player => player.ReachableLocations.Count).ToArray());
            int[] weights = new int[players.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = lcm / players[i].ReachableLocations.Count;
            }

            int choice = rnd.Next(weights.Sum());

            for (int i = 0; i < players.Length; i++)
            {
                if ((choice -= weights[i]) < 0)
                {
                    return i;
                }
            }

            throw new ArithmeticException("Failed to calculate inverse weighted random");
        }

        private struct Player
        {
            public List<string> UnobtainedLocations;
            public List<string> UnobtainedItems;
            public List<string> ObtainedItems;

            public List<string> Progression;
            public List<string> ReachableLocations;

            public void GiveItem(string item)
            {
                ObtainedItems.Add(item);
                UnobtainedItems.Remove(item);
            }

            public void TakeLocation(string loc)
            {
                UnobtainedLocations.Remove(loc);
            }

            public void UpdateProgression(ServerSettings settings)
            {
                string[] obtained = new string[ObtainedItems.Count + 1];
                ObtainedItems.CopyTo(obtained);

                // Update reachable locations
                ReachableLocations = new List<string>();
                List<string> unobtainable = new List<string>();
                foreach (string loc in UnobtainedLocations)
                {
                    if (ParseLogic(loc, obtained, settings))
                    {
                        ReachableLocations.Add(loc);
                    }
                    else
                    {
                        unobtainable.Add(loc);
                    }
                }

                // Update progression items
                Progression = new List<string>();
                foreach (string item in UnobtainedItems)
                {
                    if (!LogicManager.GetItemDef(item).progression)
                    {
                        continue;
                    }

                    obtained[obtained.Length - 1] = item;

                    foreach (string loc in unobtainable)
                    {
                        if (ParseLogic(loc, obtained, settings))
                        {
                            Progression.Add(item);
                            break;
                        }
                    }
                }
            }

            private static bool ParseLogic(string item, string[] obtained, ServerSettings settings)
            {
                return LogicManager.ParseLogic(item, obtained, settings.ShadeSkips, settings.AcidSkips,
                    settings.SpikeTunnels, settings.MiscSkips, settings.FireballSkips, settings.MagSkips,
                    settings.NoClaw);
            }
        }
    }
}
