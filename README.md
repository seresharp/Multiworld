# Multiworld Randomizer
This Mod allows multiple people to cooperatively play a randomizer in Hollow Knight with items scattered across multiple worlds connected over the network.

# Install
1. Download the latest archive from Releases
2. Install the Modding API into Hollow Knight using https://radiance.host/mods/ModInstaller.exe
3. Then unzip the archive into "Steam\SteamApps\common\Hollow Knight"
4. Start the game so that the config file will be generated
5. Edit the MultiWorldMod.GlobalSettings.json file in "AppData\LocalLow\Team Cherry\Hollow Knight" to point to the servers IP address
6. Run the MultiworldServer.exe and potentially forward the 38281 port to allows others to connect to you.
7. Wait until everyone is connected and then start playing on a clear save file.

# Notes
The server should be able to handle disconnects as long as the Game hasn't crashed. If a players game crashes you might have to restart. We'll hopefully fix this later.
