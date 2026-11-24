using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HexaSort.Editor
{
    /// <summary>Saves Game view screenshots into docs/media for the README (Play Mode only).</summary>
    public static class MediaCapture
    {
        private const string Folder = "docs/media";

        [MenuItem("Tools/HexaSort/Capture Screenshot _F12", priority = 30)]
        public static void Capture()
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, $"shot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log($"[HexaSort] Screenshot will be written to {Path.GetFullPath(path)} at the end of the frame.");
        }

        [MenuItem("Tools/HexaSort/Capture Screenshot _F12", true)]
        public static bool CanCapture() => EditorApplication.isPlaying;
    }
}
