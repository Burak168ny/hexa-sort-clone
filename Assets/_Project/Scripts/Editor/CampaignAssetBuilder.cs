using System.IO;
using HexaSort.Core.Levels;
using HexaSort.Model;
using UnityEditor;
using UnityEngine;

namespace HexaSort.Editor
{
    /// <summary>Writes the built-in <see cref="Campaign"/> into LevelData assets plus the LevelDatabase.</summary>
    public static class CampaignAssetBuilder
    {
        public const string LevelsFolder = "Assets/_Project/Levels";
        public const string DatabasePath = LevelsFolder + "/LevelDatabase.asset";

        [MenuItem("Tools/HexaSort/Rebuild Campaign Levels", priority = 11)]
        public static void RebuildWithConfirmation()
        {
            if (!EditorUtility.DisplayDialog("Rebuild campaign levels",
                    $"Overwrite levels 1-{Campaign.LevelCount} in {LevelsFolder} with the built-in campaign?\n\n" +
                    "Edits made in the Level Editor to those levels will be lost. Extra levels are kept.",
                    "Rebuild", "Cancel"))
                return;
            Rebuild();
        }

        /// <summary>Returns the database, creating it (and the campaign assets) if it does not exist.</summary>
        public static LevelDatabase EnsureDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            return db != null ? db : Rebuild();
        }

        public static LevelDatabase Rebuild()
        {
            Directory.CreateDirectory(LevelsFolder);
            var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            Undo.RecordObject(db, "Rebuild campaign");
            foreach (var def in Campaign.CreateAll())
            {
                string path = LevelPath(def.Number);
                var data = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<LevelData>();
                    AssetDatabase.CreateAsset(data, path);
                }
                data.CopyFrom(def);
                EditorUtility.SetDirty(data);

                int index = def.Number - 1;
                while (db.Levels.Count <= index) db.Levels.Add(null);
                db.Levels[index] = data;
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HexaSort] Campaign written: {Campaign.LevelCount} levels in {LevelsFolder}.");
            return db;
        }

        public static string LevelPath(int number) => $"{LevelsFolder}/Level_{number:00}.asset";
    }
}
