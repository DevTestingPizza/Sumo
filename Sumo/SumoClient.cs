using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace Sumo
{
    public class SumoClient : BaseScript
    {

        public struct SumoProp
        {
            public uint Hash;
            public Vector3 Position;
            public float heading;
            public Vector3 Rotation;
            public int Texture;
        }

        #region variables
        private bool FirstSetupDone = false;

        // Current round/game variables
        public static int Round { get; private set; } = 1;
        public enum GamePhase { WAITING, STARTING, STARTED, DEAD, RESTARTING, CHOOSING_VEHICLE };
        public static GamePhase _CurrentGamePhase = GamePhase.WAITING;
        private bool suddenDeath = false;
        private Vehicle currentVehicle = null;
        private string nextVehicle = "zentorno";
        private string lastMap = "";
        private List<SumoProp> props = new List<SumoProp>();
        private Vector3 mapCenterCoords = new Vector3(0f, 0f, 0f);
        private int matchTimer = 0; // used to end the game if the time is up.

        private int CountDownTimer = 3;
        private Scaleform countdownScale;
        private int timem = 3;
        private int times = 0;
        private string timeleft = "";

        // Spectating, camera and other GFX variables.
        private int camera = CreateCam("TIMED_SPLINE_CAMERA", true);
        private string spectating = null;
        private enum MessageType { WINNER, INFO, WASTED }; // message type for big onscreen messages.

        // Colors for scoreboard and other player-specific RGB needs.
        public static int playerR = 0;
        public static int playerG = 0;
        public static int playerB = 0;
        public static int playerA = 0;

        // Map variables.
        private float zDeathCoord = -1f;
        private bool dieInWater = false;
        private int worldHourOfDay = 12;

        // Variables used to keep track of repeating events, making sure they don't get called too quickly.
        private bool dontCountAsDeath = false;

        // Other misc variables.
        public static bool DebugMode { get; } = true;
        #endregion



        /// <summary>
        /// Constructor
        /// </summary>
        public SumoClient()
        {
            EventHandlers.Add("onClientMapStart", new Action(StartSetup));
        }

        #region core game functions
        /// <summary>
        /// Runs the setup on first join or on resource restart.
        /// </summary>
        private async void StartSetup()
        {
            // On first resource start only.
            if (!FirstSetupDone)
            {
                GetHudColour(28 + PlayerId(), ref playerR, ref playerG, ref playerB, ref playerA);
                //GetHudColour(28 + 24 - 6 + PlayerId(), ref playerR, ref playerG, ref playerB, ref playerA);

                FirstSetupDone = true;
                Exports["spawnmanager"].setAutoSpawn(false);

                EventHandlers.Add("Sumo:FinishGame", new Action<int, int>(EndRound));
                EventHandlers.Add("Sumo:RunSetup", new Action<int, bool>(RunSetup));
                EventHandlers.Add("Sumo:StartGame", new Action<string, float, float, float>(StartGame));
                EventHandlers.Add("Sumo:SetNextVehicle", new Action<string>(SetVehicle));
                EventHandlers.Add("Sumo:Whistle", new Action<string>(Whistle));
                EventHandlers.Add("Sumo:InProgress", new Action(AlreadyInProgress));
                Print("First map load: CALLING RunSetup FUNCTION AND THUS RESPAWNING!");
                RunSetup(firstJoin: true);
                await Delay(1000);

                RequestScriptAudioBank("DLC_STUNT/STUNT_RACE_01", false);
                RequestScriptAudioBank("DLC_STUNT/STUNT_RACE_02", false);
                RequestScriptAudioBank("DLC_STUNT/STUNT_RACE_03", false);
                RequestScriptAudioBank("DLC_LOW2/SUMO_01", false);

                Tick += OnTick;
                Tick += SuddenDeath;
            }
            else
            {
                //Print("Initial resource setup is already completed, no need to run it again!");
                Print("Initial resource setup is already completed, no need to run it again: CALLING RunSetup FUNCTION AND THUS RESPAWNING!");
                RunSetup(firstJoin: false);
                return;
            }
        }

        private async Task SuddenDeath()
        {
            var safeTimer = GetGameTimer();
            var timer = GetGameTimer();
            var radius = 30f;
            if (suddenDeath)
            {
                ShowMpMessageLarge("Sudden Death", "", MessageType.INFO, 3000);
            }
            while (suddenDeath && mapCenterCoords != null && !mapCenterCoords.IsZero)
            {
                await Delay(0);

                //var sec = 0;
                while (GetGameTimer() - safeTimer < 10000)
                {
                    await Delay(0);
                    //sec++;
                    var t = GetGameTimer() - safeTimer;
                    if (t > 0 && t < 1000)
                    {
                        timeleft = "~r~00:10";
                    }
                    else if (t > 999 && t < 2000)
                    {
                        timeleft = "~r~00:09";
                    }
                    else if (t > 1999 && t < 3000)
                    {
                        timeleft = "~r~00:08";
                    }
                    else if (t > 2999 && t < 4000)
                    {
                        timeleft = "~r~00:07";
                    }
                    else if (t > 3999 && t < 5000)
                    {
                        timeleft = "~r~00:06";
                    }
                    else if (t > 4999 && t < 6000)
                    {
                        timeleft = "~r~00:05";
                    }
                    else if (t > 5999 && t < 7000)
                    {
                        timeleft = "~r~00:04";
                    }
                    else if (t > 6999 && t < 8000)
                    {
                        timeleft = "~r~00:03";
                    }
                    else if (t > 7999 && t < 9000)
                    {
                        timeleft = "~r~00:02";
                    }
                    else if (t > 8999 && t < 10000)
                    {
                        timeleft = "~r~00:01";
                    }
                    else if (t > 9999)
                    {
                        timeleft = "00:00";
                    }

                    DrawMarker(28, mapCenterCoords.X, mapCenterCoords.Y, mapCenterCoords.Z, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, radius, radius, radius, 230, 240, 30, 75, false, false, 0, false, null, null, false);
                    ShowHelp("You have 10 seconds to get into the area at which point anybody outside of the area will be", false, false);
                }

                DrawMarker(28, mapCenterCoords.X, mapCenterCoords.Y, mapCenterCoords.Z, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, radius, radius, radius, 230, 240, 30, 75, false, false, 0, false, null, null, false);
                if (GetGameTimer() - timer >= 50)
                {
                    timer = GetGameTimer();
                    radius -= 0.02f;
                }
                if (_CurrentGamePhase == GamePhase.STARTED && Game.PlayerPed.IsInVehicle() && !GetVehicle().IsDead)
                {
                    var pos = currentVehicle.Position;
                    if (Vdist(pos.X, pos.Y, pos.Z, mapCenterCoords.X, mapCenterCoords.Y, mapCenterCoords.Z) > radius)
                    {
                        NetworkExplodeVehicle(currentVehicle.Handle, true, false, false);
                        TriggerServerEvent("Sumo:RemovePlayer");
                    }
                }
            }
        }

        /// <summary>
        /// This lets the client know that a game is already in progress when they joined, it will remove their car and mark them as dead.
        /// </summary>
        private async void AlreadyInProgress()
        {
            CitizenFX.Core.UI.Screen.ShowSubtitle("Game is already in progress, please wait for the next round!");
            if (currentVehicle != null)
            {
                currentVehicle.Delete();
            }
            else
            {
                if (GetVehicle() != null)
                {
                    GetVehicle().Delete();
                }
                else
                {
                    var p = Game.PlayerPed.Position;
                    ClearArea(p.X, p.Y, p.Z, 5f, true, false, false, false);
                }
            }
            Print("RESPAWNING BECAUSE THE GAME IS ALREADY IN PROGRESS.");
            await RespawnPlayer(false);
            _CurrentGamePhase = GamePhase.DEAD;
        }

        /// <summary>
        /// Pre-setup before reach new round. Handles everything that needs to happen before the round can begin.
        /// Triggers a the "Sumo:MarkReady" server event to let the server know when this client is ready for the next game.
        /// </summary>
        /// <param name="newTime"></param>
        /// <param name="firstJoin"></param>
        private async void RunSetup(int newTime = 12, bool firstJoin = false)
        {
            suddenDeath = false;
            DoScreenFadeOut(500);
            Vector3 playerPos = Game.PlayerPed.Position;
            ClearArea(playerPos.X, playerPos.Y, playerPos.Z, 100f, true, false, false, false);

            Print("RunSetup function called.");
            timem = 3;
            times = 0;
            timeleft = ((timem == 0 && times <= 10) ? "~r~" : "") + "0" + timem.ToString() + ":" + ((times < 10) ? ("0" + times.ToString()) : times.ToString());

            _CurrentGamePhase = GamePhase.RESTARTING;

            if (IsPedInAnyVehicle(PlayerPedId(), false))
            {
                Vehicle tmpveh = GetVehicle();
                tmpveh.Delete();
            }

            worldHourOfDay = newTime;
            await Delay(500);
            Print("RESPAWN FROM RUNSETUP FUNCTION.");
            await RespawnPlayer(!firstJoin); // only spawn in  in vehicle player if this is not the first spawn after a new map is loaded/player just joined.

            await Delay(500);

            _CurrentGamePhase = GamePhase.WAITING; // Set game to waiting for next round.

            timem = 3;
            times = 0;
            timeleft = ((timem == 0 && times <= 10) ? "~r~" : "") + "0" + timem.ToString() + ":" + ((times < 10) ? ("0" + times.ToString()) : times.ToString());

            TriggerServerEvent("Sumo:MarkReady"); // Tell the server that we're ready.
            DoScreenFadeIn(500);
        }

        /// <summary>
        /// Starts a new game/round. The provided map name will be used to check some map specific data.
        /// </summary>
        /// <param name="map">Map name for the next round.</param>
        private async void StartGame(string map, float x, float y, float z)
        {
            mapCenterCoords = new Vector3(x, y, z);
            props = new List<SumoProp>();
            var file = LoadResourceFile(map, "props.json");
            if (file != null)
            {
                Newtonsoft.Json.Linq.JArray jsonProps = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JArray>(file);
                foreach (var prop in jsonProps)
                {
                    Print("Hash: " + prop["hash"].ToString());
                    Print("x: " + prop["x"].ToString());
                    Print("y: " + prop["y"].ToString());
                    Print("z: " + prop["z"].ToString());
                    var tmpProp = new SumoProp()
                    {
                        Hash = (uint)int.Parse(prop["hash"].ToString()),
                        heading = float.Parse(prop["heading"].ToString()),
                        Position = new Vector3(float.Parse(prop["x"].ToString()), float.Parse(prop["y"].ToString()), float.Parse(prop["z"].ToString())),
                        Rotation = new Vector3(float.Parse(prop["vRot"]["x"].ToString()), float.Parse(prop["vRot"]["y"].ToString()), float.Parse(prop["vRot"]["z"].ToString())),
                        Texture = (int.Parse(prop["texture"].ToString() ?? "0"))
                    };
                    if (!HasModelLoaded(tmpProp.Hash))
                    {
                        RequestModel(tmpProp.Hash);
                        while (!HasModelLoaded(tmpProp.Hash))
                        {
                            await Delay(0);
                        }
                    }
                    props.Add(tmpProp);

                }
            }

            ClearArea(mapCenterCoords.X, mapCenterCoords.Y, mapCenterCoords.Z, 300f, true, false, false, false);

            foreach (SumoProp prop in props)
            {
                ClearArea(prop.Position.X, prop.Position.Y, prop.Position.Z, 100f, true, false, false, false);
                var closestObject = GetClosestObjectOfType(prop.Position.X, prop.Position.Y, prop.Position.Z, 100f, prop.Hash, false, false, false);
                DeleteObject(ref closestObject);
                SetEntityAsMissionEntity(closestObject, false, false);
                closestObject = GetClosestObjectOfType(prop.Position.X, prop.Position.Y, prop.Position.Z, 100f, prop.Hash, false, false, false);
                DeleteObject(ref closestObject);
            }
            await Delay(500);
            if (NetworkIsHost())
            {
                foreach (SumoProp prop in props)
                {
                    Print($"Prop created at {prop.Position.ToString()}");
                    var spawnedProp = CreateObjectNoOffset(prop.Hash, prop.Position.X, prop.Position.Y, prop.Position.Z, true, false, true);
                    SetEntityHeading(spawnedProp, prop.heading);
                    SetEntityRotation(spawnedProp, prop.Rotation.X, prop.Rotation.Y, prop.Rotation.Z, 0, true);
                    SetObjectTextureVariant(spawnedProp, prop.Texture);
                    //SetModelAsNoLongerNeeded(prop.Hash);
                    //SetEntityAsNoLongerNeeded(ref spawnedProp);
                }
            }
            if (map != lastMap)
            {
                // If the screen was faded out, fade it back in.
                if (IsScreenFadedOut())
                {
                    DoScreenFadeIn(250);
                }
                #region refactoring soon
                //var cam = CreateCam("DEFAULT_SCRIPTED_CAMERA", true);
                //var x = 3491.6821289063f;
                //var y = 2583.9228515625f;
                //var z = 14.000807762146f;
                //SetFocusArea(x, y, z, 0f, 0f, 0f);
                //SetCamCoord(cam, x + 20f, y + 25f, z + 35f);
                //PointCamAtCoord(cam, x, y, z);
                //_CurrentGamePhase = GamePhase.CHOOSING_VEHICLE;
                //var timer = GetGameTimer();
                //var t = 10;
                //while (GetGameTimer() - timer < 10000)
                //{
                //    if ((GetGameTimer() - timer) % 10 == 0)
                //    {
                //        t--;
                //    }
                //    await Delay(0);
                //    RenderScriptCams(true, false, 0, true, false);
                //    SetCamActive(cam, true);
                //    _CurrentGamePhase = GamePhase.CHOOSING_VEHICLE;
                //    ClearAllHelpMessages();
                //    ShowHelp($"Choose a vehicle! (TODO)~n~New round starting in: {t}", false, false);
                //}
                ////for (var i = 10; i > 0; i--)
                ////{

                ////    await Delay(1000);
                ////}
                //RenderScriptCams(false, false, 0, false, false);
                //SetCamActive(cam, false);
                //DestroyCam(cam, true);
                //_CurrentGamePhase = GamePhase.WAITING;
                #endregion
                lastMap = map;
            }

            // Fade out.
            //DoScreenFadeOut(500);
            //await Delay(500);

            if (_CurrentGamePhase == GamePhase.STARTED || _CurrentGamePhase == GamePhase.STARTING)
            {
                Print("Start aborted, game already starting or already started.");
                return;
            }

            dieInWater = (GetResourceMetadata(map, "dieInWater", 0) == "true"); // Should the player die when they touch water?

            // If the map type is set to "offroad", then set the default vehicle to monster.
            if (GetResourceMetadata(map, "vehicleType", 0) == "offroad")
            {
                var r = new Random().Next(0, 9);
                if (r == 0)
                {
                    nextVehicle = "BIFTA";
                }
                else if (r == 1)
                {
                    nextVehicle = "BRAWLER";
                }
                else if (r == 2)
                {
                    nextVehicle = "DUBSTA3";
                }
                else if (r == 3)
                {
                    nextVehicle = "DUNE";
                }
                else if (r == 4)
                {
                    nextVehicle = "MARSHALL";
                }
                else if (r == 5)
                {
                    nextVehicle = "MONSTER";
                }
                else if (r == 6)
                {
                    nextVehicle = "MESA3";
                }
                else if (r == 7)
                {
                    nextVehicle = "TROPHYTRUCK";
                }
                else if (r == 8)
                {
                    nextVehicle = "SANDKING";
                }
            }
            else // Otherwise, set it to a random car.
            {
                var r = new Random().Next(0, 6);
                if (r == 0)
                {
                    nextVehicle = "ADDER";
                }
                else if (r == 1)
                {
                    nextVehicle = "ENTITYXF";
                }
                else if (r == 2)
                {
                    nextVehicle = "TURISMOR";
                }
                else if (r == 3)
                {
                    nextVehicle = "T20";
                }
                else if (r == 4)
                {
                    nextVehicle = "VACCA";
                }
                else if (r == 5)
                {
                    nextVehicle = "ZENTORNO";
                }
            }

            // Each map requires a Z coordinate that will trigger the death event. Even if you have a map containing only water death zones.
            var validMap = float.TryParse(GetResourceMetadata(map, "deathCoordZ", 0), out zDeathCoord);

            if (!validMap)
            {
                TriggerEvent("chatMessage", "The current map is not setup correctly, aborting start.");
                return;
            }

            await Delay(1000);
            //currentVehicle.PlaceOnGround();
            // Respawn if the game was starting, but you're not in a vehicle somehow.
            if (currentVehicle == null || !IsPedInAnyVehicle(PlayerPedId(), false))
            {
                if (GetVehicle() == null || !IsPedInAnyVehicle(PlayerPedId(), false))
                {
                    Print("RESPAWN CAUSED BECAUSE THE PLAYER WAS NOT IN A VEHICLE AT ALL.");
                    await RespawnPlayer(true);
                }
                else
                {
                    currentVehicle = GetVehicle();
                }
            }
            // Do the same if you're in the air, you're below the death z coord or your car is dead/exploded.
            else if (Game.PlayerPed.Position.Z < zDeathCoord || currentVehicle.IsInAir || Game.PlayerPed.IsDead)
            {
                Print("RESPAWN CAUSED BY THE PLAYER BEING BELOW THE MAP, THE VEHICLE BEING IN THE AIR, OR THE PED BEING DEAD.");
                Print($"CAUSE BY POSITION: {(Game.PlayerPed.Position.Z < zDeathCoord ? "yes" : "no")}.");
                Print($"CAUSE BY IN-AIR: {(currentVehicle.IsInAir ? "yes" : "no")}.");
                Print($"CAUSE BY VEHICLE IS DEAD: {(Game.PlayerPed.IsDead ? "yes" : "no")}.");
                await RespawnPlayer(true);
            }

            //// Fade the screen back in.
            //DoScreenFadeIn(500);
            //await Delay(500);

            // Clear the leftover vehicles, vehicle parts, world damage etc from last round.
            var p = Game.PlayerPed.Position;
            //ClearArea(p.X, p.Y, p.Z, 300f, true, false, false, false);
            //ClearAreaOfEverything(p.X, p.Y, p.Z, 300f, true, false, false, false);
            //ClearAreaOfObjects(mapCenterCoords.X, mapCenterCoords.Y, mapCenterCoords.Z, 300f, 0);

            Print($"Starting a new round. Death z coord = {zDeathCoord.ToString()}");

            // Set the game to starting.
            _CurrentGamePhase = GamePhase.STARTING;

            // Create the new camera for the intro.
            DestroyCam(camera, true);
            camera = CreateCam("TIMED_SPLINE_CAMERA", true);
            // Show the camera + animation.
            ManageCamera();

            await Delay(1000);

            // Play the "shard" sound and display the next round shard message.
            PlaySoundFrontend(-1, "GO", "HUD_MINI_GAME_SOUNDSET", false);
            ShowMpMessageLarge("SUMO", $"Round {Round.ToString()}", MessageType.INFO, 2000);

            await Delay(2000);

            // Run the countdown.
            StartCountDown();

            while (true)
            {
                await Delay(0);
                ShowCountDown();
                if (CountDownTimer == -1)
                {
                    break;
                }
            }
            if (currentVehicle == null)
            {
                currentVehicle = GetVehicle();
            }

            _CurrentGamePhase = GamePhase.STARTED; // game has started.

            CountDownTimer = 3; // reset the countdown for next round.

            timem = 3; // reset game/round time
            times = 0;
        }

        /// <summary>
        /// Ends the current round, displaying the winner (or draw state/time up) and setting the new time for the next round.
        /// </summary>
        /// <param name="winner"></param>
        /// <param name="newTime"></param>
        private async void EndRound(int winner, int newTime = 12)
        {
            // Don't trigger the death event anymore when the game is already finished 
            // (otherwise you'd still get the wasted screen if you died after the round has finished).
            dontCountAsDeath = true;

            // Play the sound for the end of the round and start a screen effect for roughly 6 seconds.
            PlaySoundFrontend(-1, "Round_End", "DLC_LOW2_Sumo_Soundset", false);
            StartScreenEffect("MinigameEndNeutral", 6500, true);
            await Delay(200);

            if (winner == -2)
            {
                ShowMpMessageLarge("TIME'S UP!", "LOSER!", MessageType.WASTED, 2500);
            }
            else // Show who won.
            {
                await Delay(3500);
                if (PlayerId() == GetPlayerFromServerId(winner))
                {
                    var color = 28 + PlayerId();
                    //var color = 28 - 6 + 24 + PlayerId();
                    ShowMpMessageLarge("WINNER", "", color, 2500);
                    PlaySoundFrontend(-1, "Mission_Pass_Notify", "DLC_HEISTS_GENERAL_FRONTEND_SOUNDS", false);
                }
                else
                {
                    var color = $"~HUD_COLOUR_NET_PLAYER{(1 + (GetPlayerFromServerId(winner) != -1 ? GetPlayerFromServerId(winner) : 0))}~";
                    //var color = $"~HUD_COLOUR_NET_PLAYER{(24 - 5 + (GetPlayerFromServerId(winner) != -1 ? GetPlayerFromServerId(winner) : 0))}~";
                    ShowMpMessageLarge("LOSER", $" (Winner: {color}{GetPlayerName(GetPlayerFromServerId(winner))}~HUD_COLOUR_WHITE~)", MessageType.WASTED, 2500);
                    PlaySoundFrontend(-1, "LOSER", "HUD_AWARDS", false);
                }
            }

            Print("EndRound function called.");
            await Delay(3500);

            // Restart the game if the map is going to be switched. This prevents duplicate re-spawns.
            //if ((Round + 1) % 5 == 0)
            //{
            //    _CurrentGamePhase = GamePhase.RESTARTING;
            //    TriggerServerEvent("Sumo:MarkReady");
            //}
            //else
            //{
            // Otherwise just run the setup for the next round.
            Print("End of previous round: CALLING RunSetup FUNCTION AND THUS RESPAWNING!");
            RunSetup(newTime);
            //}

            // Increase the current round number.
            Round++;

            // Re-enable deaths.
            await Delay(1000);
            dontCountAsDeath = false;
        }

        /// <summary>
        /// Respawns the player, if inVehicle is true, a new vehicle will be spawned.
        /// Requires modified spawnmanager to disable the default screen-fading caused by respawning.
        /// </summary>
        /// <param name="inVehicle"></param>
        /// <returns></returns>
        private async Task RespawnPlayer(bool inVehicle)
        {
            DoScreenFadeOut(300);
            await Delay(300);
            Print("\r\nRespawnPlayer called, in vehicle: " + inVehicle.ToString() + "\r\n");

            var p = Game.PlayerPed.Position;
            ClearArea(p.X, p.Y, p.Z, 500f, true, false, false, false);

            Exports["spawnmanager"].spawnPlayer(PlayerId() + 1);
            //Exports["spawnmanager"].forceRespawn();

            if (inVehicle)
            {
                uint vehicleHash = IsModelAVehicle((uint)GetHashKey(nextVehicle)) ? (uint)GetHashKey(nextVehicle) : (uint)GetHashKey("zentorno");
                RequestModel(vehicleHash);

                while (!HasCollisionLoadedAroundEntity(PlayerPedId()) || !HasModelLoaded(vehicleHash))
                {
                    ClearPedTasksImmediately(PlayerPedId());
                    FreezeEntityPosition(PlayerPedId(), true);
                    await Delay(0);
                }

                await Delay(500);

                FreezeEntityPosition(PlayerPedId(), false);

                Vector3 coords = GetEntityCoords(PlayerPedId(), true);
                currentVehicle = new Vehicle(CreateVehicle(vehicleHash, coords.X, coords.Y, coords.Z, Game.PlayerPed.Heading, true, false))
                {
                    NeedsToBeHotwired = false,
                    DirtLevel = 0f,
                    IsDriveable = false,
                    IsEngineRunning = false,
                    IsPositionFrozen = true,
                };

                SetVehicleExtraColours(currentVehicle.Handle, 0, 0);
                SetVehiclePaintFade(currentVehicle.Handle, 1f);
                SetVehicleCustomPrimaryColour(currentVehicle.Handle, playerR, playerG, playerB);
                SetVehicleCustomSecondaryColour(currentVehicle.Handle, playerR, playerG, playerB);
                SetVehicleColourCombination(currentVehicle.Handle, 2);

                currentVehicle.PlaceOnGround();

                while (!Game.PlayerPed.IsSittingInVehicle(currentVehicle))
                {
                    Game.PlayerPed.SetIntoVehicle(currentVehicle, VehicleSeat.Driver);
                    await Delay(0);
                }

                currentVehicle.MarkAsNoLongerNeeded();
                currentVehicle.IsPersistent = false;
                currentVehicle.PreviouslyOwnedByPlayer = false;
            }
            //if (!inVehicle)
            //{
            //    currentVehicle.IsVisible = false;
            //    currentVehicle.IsPositionFrozen = true;
            //    currentVehicle.Position = currentVehicle.Position + new Vector3(0f, 0f, 50f);
            //    //currentVehicle.IsVisible = false;
            //    //foreach (Player pr in new PlayerList())
            //    //{
            //    //    currentVehicle.SetNoCollision(new Ped(GetPlayerPed(pr.Handle)), true);
            //    //}
            //}
        }
        #endregion

        /// <summary>
        /// Checks if the vehicle is outside the game-play area, or (if enabled) is in water.
        /// </summary>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        private bool IsVehicleOutOfBounds(Vehicle vehicle)
        {
            if (vehicle.Position.Z < zDeathCoord || (dieInWater && IsEntityInWater(currentVehicle.Handle)))
            {
                return true;
            }
            return false;
        }

        #region On Tick core game logic
        /// <summary>
        /// OnTick task running every tick to manage the game phases and core game logic.
        /// </summary>
        /// <returns></returns>
        private async Task OnTick()
        {
            if (!IsPedInAnyVehicle(PlayerPedId(), false))
            {
                Game.DisableControlThisFrame(0, Control.Attack);
                Game.DisableControlThisFrame(0, Control.Attack2);
                Game.DisableControlThisFrame(0, Control.MeleeAttack1);
                Game.DisableControlThisFrame(0, Control.MeleeAttack2);
                Game.DisableControlThisFrame(0, Control.MeleeAttackAlternate);
                Game.DisableControlThisFrame(0, Control.MeleeAttackHeavy);
                Game.DisableControlThisFrame(0, Control.MeleeAttackLight);
                Game.DisableControlThisFrame(0, Control.Aim);
                Game.DisableControlThisFrame(0, Control.AccurateAim);
                Game.DisableControlThisFrame(0, Control.VehicleAim);
                Game.DisableControlThisFrame(0, Control.VehicleCinCam);
                Game.DisableControlThisFrame(0, Control.VehiclePassengerAim);
                Game.DisableControlThisFrame(0, Control.VehiclePassengerAttack);
                ClearPedTasksImmediately(PlayerPedId());
                FreezeEntityPosition(PlayerPedId(), true);
            }

            #region world/game setup
            if (GetPlayerWantedLevel(PlayerId()) > 0)
            {
                SetPlayerWantedLevel(PlayerId(), 0, false);
                SetPlayerWantedLevelNow(PlayerId(), true);
            }

            SetPedDensityMultiplierThisFrame(0.0f);
            SetScenarioPedDensityMultiplierThisFrame(0.0f, 0.0f);
            SetParkedVehicleDensityMultiplierThisFrame(0.0f);
            SetRandomVehicleDensityMultiplierThisFrame(0.0f);
            SetVehicleDensityMultiplierThisFrame(0.0f);
            SetSomeVehicleDensityMultiplierThisFrame(0.0f);

            for (var i = 1; i < 16; i++)
            {
                EnableDispatchService(i, false);
            }

            ClearWeatherTypePersist();
            SetWeatherTypeNowPersist("EXTRASUNNY");
            NetworkOverrideClockTime(worldHourOfDay, 0, 0);

            Game.DisableControlThisFrame(0, Control.VehicleExit);
            Game.DisableControlThisFrame(0, Control.Enter);
            #endregion

            #region show scoreboard
            if (Game.IsControlPressed(0, Control.Phone))
            {
                Scoreboard.DrawScoreboard();
            }
            SetEntityVisible(PlayerPedId(), IsPedInAnyVehicle(PlayerPedId(), false), false);
            #endregion

            if (_CurrentGamePhase == GamePhase.STARTED || _CurrentGamePhase == GamePhase.DEAD)
            {
                if (matchTimer == 0)
                {
                    matchTimer = GetGameTimer();
                }
                if (GetGameTimer() - matchTimer >= 1000)
                {
                    if (!(times == 0 && timem == 0))
                    {
                        times--;
                        if (times < 0)
                        {
                            times = 59;
                            timem--;
                        }
                        if (times == 5 && timem == 0)
                        {
                            PlaySoundFrontend(0, "5s_To_Event_Start_Countdown", "GTAO_FM_Events_Soundset", false);
                        }
                    }
                    else
                    {
                        //TriggerServerEvent("Sumo:EndGameTimer");
                        suddenDeath = true;
                    }
                    matchTimer = GetGameTimer();
                }

                timeleft = ((timem == 0 && times <= 10) ? "~r~" : "") + "0" + timem.ToString() + ":" + ((times < 10) ? ("0" + times.ToString()) : times.ToString());
            }

            #region WAITING GAME PHASE
            if (_CurrentGamePhase == GamePhase.WAITING)
            {
                if (IsScreenFadedOut() && (!IsScreenFadingIn() || IsScreenFadingOut()))
                {
                    DoScreenFadeIn(300);
                }
                CitizenFX.Core.UI.Screen.Hud.HideComponentThisFrame(CitizenFX.Core.UI.HudComponent.AreaName);
                CitizenFX.Core.UI.Screen.Hud.HideComponentThisFrame(CitizenFX.Core.UI.HudComponent.StreetName);
                CitizenFX.Core.UI.Screen.Hud.HideComponentThisFrame(CitizenFX.Core.UI.HudComponent.VehicleName);
                ShowHelp($"Waiting for other players.", false, false);
                DisplayRadar(false);
            }
            #endregion
            #region STARTING GAME PHASE
            if (_CurrentGamePhase == GamePhase.STARTING)
            {
                ShowHelp($"~h~Sumo~h~ round #{Round.ToString()} is starting soon!", false, false);

                Game.DisableControlThisFrame(0, Control.LookLeft);
                Game.DisableControlThisFrame(0, Control.LookLeftRight);
                Game.DisableControlThisFrame(0, Control.LookLeftOnly);
                Game.DisableControlThisFrame(0, Control.LookRight);
                Game.DisableControlThisFrame(0, Control.LookRightOnly);
                Game.DisableControlThisFrame(0, Control.LookUp);
                Game.DisableControlThisFrame(0, Control.LookUpDown);
                Game.DisableControlThisFrame(0, Control.LookUpOnly);
                Game.DisableControlThisFrame(0, Control.LookDown);
                Game.DisableControlThisFrame(0, Control.LookDownOnly);
                Game.DisableControlThisFrame(0, Control.LookBehind);
                Game.DisableControlThisFrame(0, Control.VehicleLookBehind);
                DisplayRadar(false);
            }
            #endregion
            #region STARTED GAME PHASE
            if (_CurrentGamePhase == GamePhase.STARTED)
            {
                ShowStats();




                Subtitle("~z~Force the other players out ~s~of the area.");

                if (IsPedInAnyVehicle(PlayerPedId(), false))
                {
                    if (!currentVehicle.Exists())
                    {
                        currentVehicle = GetVehicle();
                    }
                    if (currentVehicle.IsEngineRunning == false)
                    {
                        currentVehicle.IsEngineRunning = true;
                        currentVehicle.IsDriveable = true;
                    }
                    if (IsVehicleOutOfBounds(currentVehicle))
                    {
                        NetworkExplodeVehicle(currentVehicle.Handle, true, false, false);
                    }
                    if (IsPlayerDead(PlayerId()) || IsEntityDead(currentVehicle.Handle))
                    {
                        if (!dontCountAsDeath)
                        {
                            TriggerServerEvent("Sumo:RemovePlayer");
                            _CurrentGamePhase = GamePhase.DEAD;
                            //PlaySoundFrontend(-1, "GO", "HUD_MINI_GAME_SOUNDSET", false);
                            ShowMpMessageLarge("KNOCKED OUT", "", MessageType.WASTED, 3000);
                            await Delay(3000);
                            if (_CurrentGamePhase == GamePhase.DEAD)
                            {
                                //Print("Player is still marked as dead, so the game has not restarted/finished in the mean time. Respawning player invisible.");
                                Print("RESPAWNING BECAUSE PLAYER DIED, AND ROUND HASN'T FINISHED YET. Time to spectate someone else.");
                                await RespawnPlayer(false);
                            }
                            DoScreenFadeIn(250);
                        }
                    }
                }
                else
                {
                    TriggerServerEvent("Sumo:RemovePlayer");
                    _CurrentGamePhase = GamePhase.DEAD;
                    DoScreenFadeIn(250);
                }
                DisplayRadar(IsRadarPreferenceSwitchedOn());
                FreezeEntityPosition(PlayerPedId(), false);
            }
            #endregion
            #region NOT-STARTED GAME PHASE
            else
            {
                Game.DisableControlThisFrame(0, Control.MoveLeft);
                Game.DisableControlThisFrame(0, Control.MoveLeftOnly);
                Game.DisableControlThisFrame(0, Control.MoveLeftRight);
                Game.DisableControlThisFrame(0, Control.MoveRight);
                Game.DisableControlThisFrame(0, Control.MoveRightOnly);
                Game.DisableControlThisFrame(0, Control.MoveUp);
                Game.DisableControlThisFrame(0, Control.MoveUpOnly);
                Game.DisableControlThisFrame(0, Control.MoveUpDown);
                Game.DisableControlThisFrame(0, Control.MoveDown);
                Game.DisableControlThisFrame(0, Control.MoveDownOnly);
                Game.DisableControlThisFrame(0, Control.VehicleMoveLeft);
                Game.DisableControlThisFrame(0, Control.VehicleMoveLeftOnly);
                Game.DisableControlThisFrame(0, Control.VehicleMoveLeftRight);
                Game.DisableControlThisFrame(0, Control.VehicleMoveRight);
                Game.DisableControlThisFrame(0, Control.VehicleMoveRightOnly);
                Game.DisableControlThisFrame(0, Control.VehicleMoveUp);
                Game.DisableControlThisFrame(0, Control.VehicleMoveUpOnly);
                Game.DisableControlThisFrame(0, Control.VehicleMoveUpDown);
                Game.DisableControlThisFrame(0, Control.VehicleMoveDown);
                Game.DisableControlThisFrame(0, Control.VehicleMoveDownOnly);


                foreach (Player p in new PlayerList())
                {
                    if (p != Game.Player)
                    {
                        var ped = GetPlayerPed(p.Handle);
                        var existingBlip = GetBlipFromEntity(ped);
                        if (DoesBlipExist(existingBlip))
                        {
                            SetBlipDisplay(existingBlip, 4);
                        }
                    }
                }
            }
            #endregion
            #region DEAD GAME PHASE
            if (_CurrentGamePhase == GamePhase.DEAD)
            {
                if (IsPedInAnyVehicle(PlayerPedId(), false))
                {
                    SetEntityVisible(GetVehicle().Handle, false, false);
                    FreezeEntityPosition(GetVehicle().Handle, true);
                    foreach (Player p in new PlayerList())
                    {
                        var ped = GetPlayerPed(p.Handle);
                        var veh = GetVehiclePedIsIn(ped, false);
                        if (DoesEntityExist(veh) && IsEntityAVehicle(veh) && !IsEntityDead(veh))
                        {
                            SetEntityNoCollisionEntity(veh, GetVehicle().Handle, false);
                        }
                    }
                }

                ShowStats();
                var pl = new PlayerList();
                foreach (Player p in pl)
                {
                    if (IsPedInAnyVehicle(GetPlayerPed(p.Handle), false) && !p.IsDead)
                    {
                        NetworkSetActivitySpectator(true);
                        NetworkSetInSpectatorMode(true, GetPlayerPed(p.Handle));
                        spectating = p.Name;
                        break;
                    }
                }

                if (spectating != null)
                {
                    ShowHelp($"You are out! Please wait for the next round!~n~Spectating: ~b~<C>{spectating}</C>", false, false);
                }
                else
                {
                    ShowHelp("You are out! Please wait for the next round!", false, false);
                }

                FreezeEntityPosition(PlayerPedId(), true);
                SetEntityCollision(PlayerPedId(), false, false);

                DisplayRadar(false);
            }
            #endregion
            #region NOT-DEAD GAME PHASE
            else
            {
                NetworkSetActivitySpectator(false);
                NetworkSetInSpectatorMode(false, PlayerPedId());
                //SetEntityVisible(PlayerPedId(), true, false);
                FreezeEntityPosition(PlayerPedId(), false);
                SetEntityCollision(PlayerPedId(), true, true);
                SetPedCanBeKnockedOffVehicle(PlayerPedId(), 1);

                if (!(CountDownTimer > 0 && (_CurrentGamePhase == GamePhase.RESTARTING || _CurrentGamePhase == GamePhase.STARTING || _CurrentGamePhase == GamePhase.WAITING)))
                {
                    RenderScriptCams(false, true, 2000, true, false);
                }
            }
            #endregion
            #region RESTARTING GAME PHASE
            if (_CurrentGamePhase == GamePhase.RESTARTING)
            {
                ShowHelp("Loading next round, please wait.", false, false);
                DisplayRadar(false);
            }
            #endregion

            #region Manage blips
            foreach (Player p in new PlayerList())
            {
                if (p != Game.Player)
                {
                    var ped = GetPlayerPed(p.Handle);
                    if (!IsPedInAnyVehicle(ped, false) && currentVehicle != null)
                    {
                        SetEntityNoCollisionEntity(ped, currentVehicle.Handle, false);
                    }

                    if (IsEntityVisible(ped))
                    {
                        var existingBlip = GetBlipFromEntity(ped);
                        if (!DoesBlipExist(existingBlip))
                        {
                            existingBlip = AddBlipForEntity(GetPlayerPed(p.Handle));
                        }
                        SetBlipAlpha(existingBlip, 255);
                        SetBlipDisplay(existingBlip, 2);
                        SetBlipSprite(existingBlip, 1);
                        SetBlipAsShortRange(existingBlip, true);
                        SetBlipHighDetail(existingBlip, true);
                        ShowHeadingIndicatorOnBlip(existingBlip, true);
                        //SetBlipColour(existingBlip, p.Handle + 24);
                        SetBlipColour(existingBlip, p.Handle + 6);
                        SetBlipShrink(existingBlip, true);
                        SetBlipNameToPlayerName(existingBlip, p.Handle);
                    }
                    else
                    {
                        var existingBlip = GetBlipFromEntity(ped);
                        if (DoesBlipExist(existingBlip))
                        {
                            SetBlipAlpha(existingBlip, 0);
                            SetBlipDisplay(existingBlip, 4);
                        }
                    }
                }
            }

            #endregion
        }
        #endregion

        #region general/utility functions
        /// <summary>
        /// Shows a help message on screen.
        /// </summary>
        /// <param name="msg">The message to display.</param>
        /// <param name="bleep">Should the message bleep once when showing for the first time.</param>
        /// <param name="saveToBrief">Should the message be saved to the pause menu brief.</param>
        private void ShowHelp(string msg, bool bleep, bool saveToBrief)
        {
            if (suddenDeath)
            {
                BeginTextCommandDisplayHelp("TWOSTRINGS");
                AddTextComponentSubstringPlayerName(msg);
                AddTextComponentSubstringPlayerName("eliminated immediately.");
            }
            else
            {
                BeginTextCommandDisplayHelp("STRING");
                AddTextComponentSubstringPlayerName(msg);
            }


            EndTextCommandDisplayHelp(0, saveToBrief, bleep, -1);
        }

        /// <summary>
        /// Sets the vehicle used in the next rounds.
        /// </summary>
        /// <param name="vname"></param>
        private void SetVehicle(string vname)
        {
            // If the new vehicle is null, keep the old one.
            nextVehicle = vname ?? nextVehicle;
        }

        /// <summary>
        /// Starts the new round countdown timer.
        /// </summary>
        private async void StartCountDown()
        {
            for (int x = CountDownTimer; x > 0; x--)
            {
                //PlaySoundFrontend(-1, "Countdown_1", "DLC_Stunt_Race_Frontend_Sounds", false);
                PlaySoundFrontend(-1, "MP_AWARD", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
                //PlaySoundFrontend(-1, "Enemy_Pick_Up", "HUD_FRONTEND_MP_COLLECTABLE_SOUNDS", false);
                var timer = GetGameTimer();
                while (GetGameTimer() - timer < 1000)
                {
                    await Delay(0);
                }
                CountDownTimer--;
            }
            PlaySoundFrontend(-1, "Round_Start", "DLC_LOW2_Sumo_Soundset", true);
            await Delay(1000);
            CountDownTimer--;
        }

        /// <summary>
        /// Gets the updated scaleform handle for the current timer.
        /// </summary>
        /// <returns></returns>
        private Scaleform GetCountdownScaleform()
        {
            if (countdownScale == null)
            {
                countdownScale = new Scaleform("countdown");
            }
            else
            {
                countdownScale.Dispose();
                countdownScale = new Scaleform("countdown");
            }

            var text = (CountDownTimer > 1) ? CountDownTimer.ToString() : (CountDownTimer == 1) ? "1" : "GO";
            //countdownScale.CallFunction("FADE_MP", text, 200, 0, 0, 255);
            if (CountDownTimer <= 0)
            {
                //countdownScale.CallFunction("FADE_MP", text, 50, 195, 50, 255);
                //countdownScale.CallFunction("FADE_MP", text, 144, 211, 109, 255);
                countdownScale.CallFunction("FADE_MP", text, 36, 200, 36, 175);
            }
            else
            {
                countdownScale.CallFunction("FADE_MP", text, 255, 207, 60, 240);
            }

            return countdownScale;
        }

        /// <summary>
        /// Shows the countdown scaleform on screen.
        /// </summary>
        private void ShowCountDown()
        {
            var scale = GetCountdownScaleform();
            scale.Render2D();
        }

        /// <summary>
        /// Gets the player's current vehicle.
        /// </summary>
        /// <returns></returns>
        private Vehicle GetVehicle()
        {
            return new Vehicle(GetVehiclePedIsIn(PlayerPedId(), false));
        }

        /// <summary>
        /// Shows a help message in the top left corner of the screen.
        /// </summary>
        /// <param name="msg"></param>
        //private void ShowHelp(string msg)
        //{
        //    CitizenFX.Core.UI.Screen.DisplayHelpTextThisFrame(msg);
        //}

        /// <summary>
        /// Plays the whistle!
        /// </summary>
        private void Whistle(string deadPlayerServerId = null)
        {
            if (deadPlayerServerId != null)
            {
                var p = new Player(GetPlayerFromServerId(int.Parse(deadPlayerServerId)));
                string color = $"HUD_COLOUR_NET_PLAYER{(p.Handle + 1)}";
                //string color = $"HUD_COLOUR_NET_PLAYER{(p.Handle + 1 + 24 - 6)}";

                CitizenFX.Core.UI.Screen.ShowNotification($"~{color}~<C>{GetPlayerName(p.Handle)}</C> ~s~was knocked out.");
            }
            //PlaySoundFrontend(-1, "Whistle", "DLC_TG_Running_Back_Sounds", false);
            PlaySoundFrontend(-1, "Vehicle_Destroyed", "DLC_LOW2_Sumo_Soundset", false);
        }

        /// <summary>
        /// Manages the camera intro move.
        /// </summary>
        private async void ManageCamera()
        {
            //DoScreenFadeOut(100);
            await Delay(50);
            Vector3 gameCamPos = GameplayCamera.Position;
            var gameCam = GameplayCamera.FieldOfView;

            var fov = GameplayCamera.FieldOfView;
            Vector3 pointAt = GameplayCamera.Rotation;

            var playerPos = Game.PlayerPed.Position;
            SetCamCoord(camera, playerPos.X, playerPos.Y, playerPos.Z);

            Vector3 pos = GetEntityCoords(PlayerPedId(), true);
            PointCamAtCoord(camera, pos.X, pos.Y, pos.Z + 0.5f);
            SetCamFov(camera, fov);
            DoScreenFadeOut(200);
            await Delay(200);
            RenderScriptCams(true, false, 0, true, false);

            DoScreenFadeIn(500);
            //await Delay(500);

            SetCamFov(camera, fov);
            SetFollowVehicleCamZoomLevel(1);
            SetFollowVehicleCamViewMode(2);
            Vector3 offPos = GetOffsetFromEntityInWorldCoords(PlayerPedId(), 12f, 8f, 0.3f);
            AddCamSplineNode(camera, offPos.X, offPos.Y, offPos.Z, 0f, 0f, 0f, 120, 200, 0);

            offPos = GetOffsetFromEntityInWorldCoords(PlayerPedId(), 0.2f, -10f, 1.2f);
            SetGameplayCamRelativeHeading(0f);
            //gameCamPos = GameplayCamera.Position;
            gameCamPos = offPos;
            AddCamSplineNode(camera, offPos.X, offPos.Y, gameCamPos.Z, 0f, 0f, 0f, 220, 300, 0);

            SetCamSplineDuration(camera, 5000);
            RenderScriptCams(true, true, 5000, true, false);
        }

        /// <summary>
        /// Displays a subtitle with the provided message for the specified amount of time in ms.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="duration"></param>
        private void Subtitle(string message, int duration = 2500)
        {
            CitizenFX.Core.UI.Screen.ShowSubtitle(message, duration);
        }

        /// <summary>
        /// Shows a message on screen.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="description"></param>
        /// <param name="type"></param>
        /// <param name="time"></param>
        private async void ShowMpMessageLarge(string message, string description, MessageType type, int time = 5000)
        {
            PlaySoundFrontend(-1, "MP_WAVE_COMPLETE", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
            Scaleform sc = new Scaleform("MP_BIG_MESSAGE_FREEMODE");
            while (!sc.IsLoaded)
            {
                await Delay(0);
            }
            int bg = 2;
            int txt = 0;
            if (type == MessageType.WINNER)
            {
                txt = 9;
            }
            else if (type == MessageType.INFO)
            {
                txt = 0;
            }
            else if (type == MessageType.WASTED)
            {
                txt = 6;
            }
            sc.CallFunction("SHOW_SHARD_CENTERED_MP_MESSAGE", message, description.ToUpper(), txt, bg);
            sc.CallFunction("TRANSITION_IN");
            var timer = GetGameTimer();
            while (true)
            {
                if (GetGameTimer() - timer > time)
                {
                    PlaySoundFrontend(-1, "ARM_WRESTLING_WHOOSH_MASTER", "", false);
                    sc.CallFunction("TRANSITION_OUT");
                    sc.Render2D();
                    sc.Dispose();
                    break;
                }
                sc.Render2D();
                await Delay(0);
            }
        }

        /// <summary>
        /// Shows a message on screen.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="description"></param>
        /// <param name="textHudColor"></param>
        /// <param name="time"></param>
        private async void ShowMpMessageLarge(string message, string description, int textHudColor, int time = 5000)
        {
            Scaleform sc = new Scaleform("MP_BIG_MESSAGE_FREEMODE");
            while (!sc.IsLoaded)
            {
                await Delay(0);
            }
            int bg = 2;

            sc.CallFunction("SHOW_SHARD_CENTERED_MP_MESSAGE", message, description.ToUpper(), textHudColor, bg);
            sc.CallFunction("TRANSITION_IN");
            var timer = GetGameTimer();
            while (true)
            {
                if (GetGameTimer() - timer > time)
                {
                    sc.CallFunction("TRANSITION_OUT");
                    sc.Dispose();
                    break;
                }
                sc.Render2D();
                await Delay(0);
            }

        }

        /// <summary>
        /// Show the stats.
        /// </summary>
        private void ShowStats()
        {
            var actualWidth = 350.0f;
            var actualHeight = 40.0f;

            var width = actualWidth / 1920.0f;
            var height = actualHeight / 1080.0f;
            var x = (1920.0f - (actualWidth / 2f) - (5f + actualHeight) * 1f) / 1920.0f;
            var y = (1080.0f - (actualHeight / 2f) - (5f + actualHeight) * 1f) / 1080.0f;
            var y2 = (1080.0f - (actualHeight / 2f) - (5f + actualHeight) * 2f) / 1080.0f;
            var y3 = (1080.0f - (actualHeight / 2f) - (5f + actualHeight) * 3f) / 1080.0f;

            if (!suddenDeath)
            {
                DrawSprite("social_club2_g0", "social_club2", x, y, width, height, 0.0f, 0, 0, 0, 180);
                DrawText("TIME", x - ((width - 0.01f) / 2f), y - ((height - 0.01f) / 2f), false);
                DrawText(timeleft, x + ((width - 0.01f) / 2f), y - ((height - 0.01f) / 2f), true);
            }

            DrawSprite("social_club2_g0", "social_club2", x, y2, width, height, 0.0f, 0, 0, 0, 180);
            DrawText("PLAYERS", x - ((width - 0.01f) / 2f), y2 - ((height - 0.01f) / 2f), false);
            DrawText($"{NetworkGetNumConnectedPlayers()}/32", x + ((width - 0.01f) / 2f), y2 - ((height - 0.01f) / 2f), true);

            DrawSprite("social_club2_g0", "social_club2", x, y3, width, height, 0.0f, 0, 0, 0, 180);
            DrawText("ROUND", x - ((width - 0.01f) / 2f), y3 - ((height - 0.01f) / 2f), false);
            DrawText($"{Round}", x + ((width - 0.01f) / 2f), y3 - ((height - 0.01f) / 2f), true);
        }

        /// <summary>
        /// Draw text on screen.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="right"></param>
        private void DrawText(string text, float x, float y, bool right = false)
        {
            if (right)
            {
                SetTextFont(5);
                SetTextScale(1.0f, 0.38f);
            }
            else
            {
                SetTextFont(8);
                SetTextScale(1.0f, 0.35f);
            }

            SetTextColour(195, 195, 195, 255);
            if (right)
            {
                SetTextJustification(2);
                SetTextWrap(0.0f, x);
            }
            else
            {
                SetTextJustification(1);
            }
            BeginTextCommandDisplayText("STRING");
            AddTextComponentSubstringPlayerName(text);
            EndTextCommandDisplayText(right ? 1f : x, right ? y - 0.001f : y);
        }

        /// <summary>
        /// Logs the provided data to the console.
        /// </summary>
        /// <param name="data">The data to print to the console.</param>
        public static void Print(dynamic data)
        {
            if (DebugMode)
            {
                Debug.WriteLine(data.ToString(), "");
            }
        }
        #endregion
    }
}
