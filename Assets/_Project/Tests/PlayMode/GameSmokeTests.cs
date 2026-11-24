using System;
using System.Collections;
using System.IO;
using HexaSort.Core;
using HexaSort.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HexaSort.Tests
{
    /// <summary>
    /// End-to-end checks on the generated Game scene: start a level and play a few drops through
    /// <see cref="GameManager.Drop"/>, the same entry point as dragging. Any exception fails the test.
    /// With HEXASORT_SHOTS set to a folder, it also renders 1080x1920 screenshots.
    /// </summary>
    public sealed class GameSmokeTests
    {
        private static string ShotFolder => Environment.GetEnvironmentVariable("HEXASORT_SHOTS");

        [UnityTest]
        public IEnumerator PlayAFewDrops()
        {
            yield return SceneManager.LoadSceneAsync("Game");
            var game = Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(game);
            yield return WaitFor(() => game.State == GameState.Home || game.State == GameState.Playing);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("home");

            game.Play();
            yield return new WaitForSeconds(0.8f);
            yield return Shot("level");

            for (int drop = 0; drop < 8 && game.State == GameState.Playing; drop++)
            {
                if (!TryFindDrop(game, out int slot, out HexCoord cell)) break;
                game.Drop(slot, cell);
                if (drop == 5)
                {
                    yield return new WaitForSeconds(0.35f);
                    yield return Shot("merge");
                }
                yield return WaitFor(() => game.State != GameState.Resolving);
                yield return new WaitForSeconds(0.2f);
            }
            yield return new WaitForSeconds(0.5f);
            yield return Shot("play");
        }

        /// <summary>Prefers a cell next to a stack with the same top colour, so merges happen.</summary>
        private static bool TryFindDrop(GameManager game, out int slot, out HexCoord cell)
        {
            var session = game.Session;
            var neighbours = new System.Collections.Generic.List<BoardCell>();
            slot = -1;
            cell = default;
            int bestScore = -1;
            for (int s = 0; s < session.Tray.SlotCount; s++)
            {
                var stack = session.Tray.Get(s);
                if (stack == null) continue;
                foreach (var c in session.Board.Cells)
                {
                    if (!game.CanDrop(s, c.Coord)) continue;
                    session.Board.GetNeighbors(c.Coord, neighbours);
                    int score = 0;
                    foreach (var n in neighbours)
                        if (n.Stack != null && n.Stack.TopColor == stack.TopColor) score++;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        slot = s;
                        cell = c.Coord;
                    }
                }
            }
            return slot >= 0;
        }

        private static IEnumerator Shot(string name)
        {
            var folder = ShotFolder;
            if (string.IsNullOrEmpty(folder)) yield break;
            Directory.CreateDirectory(folder);

            const int width = 1080, height = 1920;
            var cam = Camera.main;
            var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes = new RenderMode[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                if (!canvases[i].isRootCanvas || canvases[i].renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = cam;
                canvases[i].planeDistance = 1f;
            }

            cam.targetTexture = rt;
            yield return null; // camera fitter refits to the texture, canvases rebuild
            yield return null;
            cam.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());

            cam.targetTexture = null;
            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i] != null) canvases[i].renderMode = modes[i];
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeout = 10f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out.");
                yield return null;
            }
        }
    }
}
