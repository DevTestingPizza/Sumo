using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.UI;
using static CitizenFX.Core.Native.API;

namespace Sumo
{
    static class Scoreboard
    {
        // Variables
        public static int _WIDTH = Screen.Resolution.Width;
        public static int _HEIGHT = Screen.Resolution.Height;

        /// <summary>
        /// Public method used to draw the scorebaord on screen.
        /// </summary>
        public static void DrawScoreboard()
        {

            DrawHeader(); // draw the header.

            var players = 1;
            foreach (Player p in new PlayerList()) // loop through players and draw a row for each player.
            {
                var r = 0;
                var g = 0;
                var b = 0;
                var a = 0;
                GetHudColour(28 + p.Handle, ref r, ref g, ref b, ref a);
                //GetHudColour(28 - 6 + 24 + p.Handle, ref r, ref g, ref b, ref a);
                //int wins = (5 * (p.Handle + 1) / NetworkGetNumConnectedPlayers() + 1);
                //int loses = 5 * (p.Handle + 4);
                //float wlratio = (float)Math.Round((double)wins / ((double)loses + 0.0001), 2);
                int wins = 0;
                int loses = 0;
                float wlratio = 0f;

                DrawRow(players, p.Name, wins, loses, wlratio, r, g, b);
                players++;
            }
        }

        /// <summary>
        /// Draws the header.
        /// </summary>
        private static void DrawHeader()
        {
            // Draw the header bg.
            DrawRect(GetX(_WIDTH / 2), GetY(80), GetWidth(800), GetHeight(40), 10, 10, 10, 195);

            // Draw the header text.
            DrawHeaderText();
        }

        /// <summary>
        /// Draw the header text.
        /// </summary>
        private static void DrawHeaderText()
        {
            // First column.
            DrawText("SUMO (ROUND " + SumoClient.Round.ToString() + ")", GetX((_WIDTH / 2) - 375), GetY(65), 0.37f, Font.ChaletLondon, Alignment.Left, false);

            var y = GetY(65);
            // Draw other coloumns (data columns)
            DrawText("ROUNDS WON", GetX((_WIDTH / 2) - 484 + (500 / 3) + 300), y, 0.37f, Font.ChaletLondon, Alignment.Center);
            DrawText("DEATHS", GetX((_WIDTH / 2) - 484 + (500 / 3 * 2) + 300), y, 0.37f, Font.ChaletLondon, Alignment.Center);
            DrawText("W/L RATIO", GetX((_WIDTH / 2) - 484 + (500 / 3 * 3) + 300), y, 0.37f, Font.ChaletLondon, Alignment.Center);
        }

        /// <summary>
        /// Draw a player row.
        /// </summary>
        /// <param name="rowNum">Row number.</param>
        /// <param name="title">The player name/row title.</param>
        /// <param name="data1">The first data column.</param>
        /// <param name="data2">The second data column.</param>
        /// <param name="data3">The third data column.</param>
        /// <param name="r">Background red.</param>
        /// <param name="g">Background green.</param>
        /// <param name="b">Background blue.</param>
        private static void DrawRow(int rowNum, string title, int data1, int data2, float data3, int r, int g, int b)
        {
            DrawRect(GetX((_WIDTH / 2) - 250), GetY(((42) * rowNum) + 85), GetWidth(299), GetHeight(40), r, g, b, 175);
            DrawRect(GetX((_WIDTH / 2) - 246), GetY(((42) * rowNum) + 85), GetWidth(290), GetHeight(40), 25, 25, 25, 100);

            DrawRect(GetX(((_WIDTH / 2) + 150) - 501 / 3), GetY(((42) * rowNum) + 85), GetWidth(500 / 3), GetHeight(40), 10, 80, 150, 175);
            DrawRect(GetX(((_WIDTH / 2) + 150) + 0), GetY(((42) * rowNum) + 85), GetWidth(500 / 3), GetHeight(40), 10, 80, 150, 175);
            DrawRect(GetX(((_WIDTH / 2) + 150) + 0), GetY(((42) * rowNum) + 85), GetWidth(500 / 3), GetHeight(40), 10, 10, 10, 50);
            DrawRect(GetX(((_WIDTH / 2) + 150) + 501 / 3), GetY(((42) * rowNum) + 85), GetWidth(500 / 3), GetHeight(40), 10, 80, 150, 175);

            var y = GetY(((42) * rowNum) + 65);
            DrawText(title, GetX((_WIDTH / 2) - 375), y, 0.55f, Font.ChaletComprimeCologne, Alignment.Left);

            y = GetY(((42) * rowNum) + 70);
            DrawText(data1.ToString(), GetX((_WIDTH / 2) - 480 + (500 / 3) + 300), y, 0.35f, Font.ChaletLondon, Alignment.Center);
            DrawText(data2.ToString(), GetX((_WIDTH / 2) - 480 + (500 / 3 * 2) + 300), y, 0.35f, Font.ChaletLondon, Alignment.Center);
            DrawText(data3.ToString(), GetX((_WIDTH / 2) - 480 + (500 / 3 * 3) + 300), y, 0.35f, Font.ChaletLondon, Alignment.Center);
        }

        /// <summary>
        /// Draw text at the specified location (0.0-1.0).
        /// </summary>
        /// <param name="message">The text to display.</param>
        /// <param name="x">The relative x position (0.0-1.0)</param>
        /// <param name="y">The relative y position (0.0-1.0)</param>
        /// <param name="scale">The size of the text.</param>
        /// <param name="font">The font to use for this text.</param>
        /// <param name="align">Align center, left or right.</param>
        /// <param name="outline">(Optional) draw outline for the text.</param>
        private static void DrawText(string message, float x, float y, float scale, Font font, Alignment align, bool outline = false)
        {
            SetTextFont((int)font);
            SetTextJustification((int)align);
            if (outline)
            {
                SetTextOutline();
            }
            if (align == Alignment.Right)
            {
                SetTextWrap(0f, x);
            }
            if (message == "0" || message == "0.00" || message == "0.00")
            {
                message = "-";
            }
            SetTextScale(1f, scale);
            BeginTextCommandDisplayText("STRING");
            AddTextComponentSubstringPlayerName(message);
            if (align == Alignment.Right)
            {
                EndTextCommandDisplayText(0f, y);
            }
            else
            {
                EndTextCommandDisplayText(x, y);
            }
        }

        #region real pixels to screen percentage calculations
        /// <summary>
        /// Converts real pixels into relative screen width float value.
        /// </summary>
        /// <param name="width">The width in real pixels.</param>
        /// <returns>The provided width in relative sizes.</returns>
        private static float GetWidth(int width)
        {
            return (float)width / Screen.Resolution.Width;
        }

        /// <summary>
        /// Converts real pixels into realtive screen height float value.
        /// </summary>
        /// <param name="height">The height in real pixels.</param>
        /// <returns>The provided height converted into relative height float value.</returns>
        private static float GetHeight(int height)
        {
            return (float)height / Screen.Resolution.Height;
        }

        /// <summary>
        /// Convert real x coordinate in pixels to relative gta screen display system.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        private static float GetX(int x)
        {
            return (float)x / Screen.Resolution.Width;
        }

        /// <summary>
        /// Convert real y coordinate in pixels to relative gta screen display system.
        /// </summary>
        /// <param name="y"></param>
        /// <returns></returns>
        private static float GetY(int y)
        {
            return (float)y / Screen.Resolution.Height;
        }
        #endregion
    }
}
