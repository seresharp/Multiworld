using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultiWorldClient
{
    class GameInformation
    {
        uint PlayerID;
        Dictionary<string, PlayerItem> ItemLocations;

        public GameInformation(uint playerId)
        {
            PlayerID = playerId;
            ItemLocations = new Dictionary<string, PlayerItem>();
        }

        public void SetLocation(string location, string item, uint playerId)
        {
            PlayerItem playerItem;
            if (!ItemLocations.TryGetValue(location, out playerItem))
            {
                ItemLocations.Add(location, new PlayerItem(item, playerId));
            }
            else
            {
                //If the location already has an item we check if it matches what we already have
                //If so we silently ignore the double setting if not we throw an exception cause something went wrong
                if(playerId != playerItem.PlayerId || item != playerItem.Item)
                {
                    throw new InvalidOperationException(String.Format("Value for Location {0} already set to ({1},{2}). Trying to set to ({3}{4}).", location, playerItem.Item, playerItem.PlayerId, item, playerId));
                }
                else
                {
                    //Do nothing, we're just getting the same information again
                }
            }
        }
    }
}
