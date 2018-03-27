using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace SumoServer
{
    public class SumoServer : BaseScript
    {
        // Variables
        private PlayerList players;
        private List<string> readyPlayers;
        private List<string> playersAlive;
        private bool gameStarted = false;
        private bool justReset = false;
        private int maxPlayers = 10;
        private int round = 0;
        private bool skipNextChange = false;

        /// <summary>
        /// Constructor
        /// </summary>
        public SumoServer()
        {
            players = new PlayerList();
            readyPlayers = new List<string>();
            playersAlive = new List<string>();
            maxPlayers = players.Count() > 2 ? players.Count() : 2;

            EventHandlers.Add("Sumo:MarkReady", new Action<Player>(MarkPlayerReady));
            EventHandlers.Add("playerDropped", new Action<Player>(RemovePlayer));
            EventHandlers.Add("Sumo:RemovePlayer", new Action<Player>(RemovePlayer));
            EventHandlers.Add("Sumo:EndGameTimer", new Action(EndGame));
            Tick += ManageNetworkGame;
            RegisterCommand("vehicle", new Action<int, List<object>, string>(SetVehicle), false);
        }

        /// <summary>
        /// Update the vehicle globally for the next round.
        /// </summary>
        /// <param name="source">Player source executing the command.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="rawCommand">The full command/chat message.</param>
        private void SetVehicle(int source, List<object> args, string rawCommand)
        {
            TriggerClientEvent("Sumo:SetNextVehicle", args[0].ToString() ?? "monster");
        }

        /// <summary>
        /// End the current round/game.
        /// </summary>
        private async void EndGame()
        {
            if (!justReset)
            {
                playersAlive = new List<string>();
                players = new PlayerList();
                readyPlayers = new List<string>();
                TriggerClientEvent("Sumo:FinishGame", -2, 12);
                gameStarted = false;
                playersAlive = new List<string>();
                players = new PlayerList();
                readyPlayers = new List<string>();
                justReset = true;
                await Delay(7500);
                justReset = false;
            }
        }

        /// <summary>
        /// Remove this player from the alive-players list.
        /// </summary>
        /// <param name="source"></param>
        private void RemovePlayer([FromSource]Player source)
        {
            if (gameStarted)
            {
                if (playersAlive.Contains(source.Handle))
                {
                    playersAlive.Remove(source.Handle);
                }
                TriggerClientEvent("Sumo:Whistle", source.Handle);
            }
        }

        /// <summary>
        /// Mark this player as ready to start the next round.
        /// </summary>
        /// <param name="player"></param>
        private void MarkPlayerReady([FromSource] Player player)
        {
            Debug.WriteLine($"Player {player.Name} is ready for round {round}.");
            if (gameStarted)
            {
                player.TriggerEvent("Sumo:InProgress");
            }
            else
            {
                if (!readyPlayers.Contains(player.Handle))
                {
                    readyPlayers.Add(player.Handle);
                }
            }
        }

        /// <summary>
        /// Loads the next map. 
        /// </summary>
        /// <returns></returns>
        private async Task LoadNextMap()
        {
            if (skipNextChange)
            {
                skipNextChange = false;
                gameStarted = false;
            }
            else if (round % 5 == 0 && !skipNextChange)
            {
                skipNextChange = true;
                gameStarted = false;
                playersAlive = new List<string>();
                players = new PlayerList();
                readyPlayers = new List<string>();
                string currentMap = Exports["mapmanager"].getCurrentMap();
                dynamic allMaps = Exports["mapmanager"].getMaps();

                List<string> maps = new List<string>();
                foreach (KeyValuePair<string, dynamic> mapData in allMaps)
                {
                    string map = mapData.Key;
                    if (Exports["mapmanager"].doesMapSupportGameType(Exports["mapmanager"].getCurrentGameType(), map) ?? false)
                    {
                        foreach (KeyValuePair<string, object> data in mapData.Value)
                        {
                            if (data.Key.IndexOf("maxPlayers") != -1)
                            {
                                string playerCount = data.Value.ToString();
                                int maxPlayers = int.Parse(playerCount);
                                if (new PlayerList().Count() <= maxPlayers)
                                {
                                    maps.Add(map);
                                }
                            }
                        }
                    }
                }
                int index = maps.ToList().IndexOf(currentMap);
                string newMap = currentMap;
                if (maps.Count() > 0)
                {
                    if (maps.Count() <= index + 2)
                    {
                        newMap = maps[0];
                    }
                    else if (maps.Count > index + 1)
                    {
                        newMap = maps[index + 1];
                    }
                }
                //string newMap = "sumo-rooftop-1";

                Debug.WriteLine("new map: " + newMap);

                if (newMap != currentMap)
                {
                    Exports["mapmanager"].changeMap(newMap);
                    await Delay(500);
                    return;
                }

            }
        }

        /// <summary>
        /// OnTick, keeps track of dead/alive players in-game. Starts and stops the game when needed.
        /// </summary>
        /// <returns></returns>
        private async Task ManageNetworkGame()
        {
            maxPlayers = players.Count() > 2 ? players.Count() : 2;

            if (gameStarted)
            {
                if (playersAlive.Count <= 1 || new PlayerList().Count() < 2)
                {
                    var newTime = new Random().Next(0, 2) == 1 ? 12 : 0;
                    TriggerClientEvent("Sumo:FinishGame", playersAlive[0], newTime);
                    gameStarted = false;
                    playersAlive = new List<string>();
                    players = new PlayerList();
                    readyPlayers = new List<string>();
                }
            }
            else
            {
                players = new PlayerList();

                if (readyPlayers.Count() >= players.Count() && players.Count() == maxPlayers)
                {
                    foreach (Player p in players)
                    {
                        if (!readyPlayers.Contains(p.Handle))
                        {
                            Debug.WriteLine("Not enough players, somehow...");
                            return;
                        }
                    }

                    await LoadNextMap();

                    if (!gameStarted)
                    {
                        await Delay(1000);
                        var map = Exports["mapmanager"].getCurrentMap();
                        string suddenDeathCenter = GetResourceMetadata(map, "sudden_death_coords_data_extra", 0) ?? "{\"x\":0.0,\"z\":0.0,\"y\":0.0";

                        suddenDeathCenter = suddenDeathCenter.Replace('}', ',');
                        float x = float.Parse(suddenDeathCenter.Split('x')[1].Split(':')[1].Split(',')[0]);
                        float y = float.Parse(suddenDeathCenter.Split('y')[1].Split(':')[1].Split(',')[0]);
                        float z = float.Parse(suddenDeathCenter.Split('z')[1].Split(':')[1].Split(',')[0]);
                        Debug.Write($"x:{x}\r\ny:{y}\r\nz:{z}\r\n");
                        TriggerClientEvent("Sumo:StartGame", map, x, y, z);
                        round++;
                        await Delay(4500);
                        gameStarted = true;
                        foreach (Player p in players)
                        {
                            playersAlive.Add(p.Handle);
                        }
                    }
                }
                await Delay(0);
            }
        }

    }
}
