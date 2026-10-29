using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TennisPrototype.Editor
{
    public static class JetBrainsMonoTmpSetup
    {
        private const string SourceDirectory = "Assets/_MainProject/Art/Fonts/static";
        private const string OutputDirectory = "Assets/_MainProject/Art/Fonts/TMP";

        [MenuItem("Tools/Tennis/Set Up JetBrains Mono TMP Fonts")]
        private static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before creating TMP font assets.");

            if (!AssetDatabase.IsValidFolder(OutputDirectory))
                AssetDatabase.CreateFolder("Assets/_MainProject/Art/Fonts", "TMP");

            string[] sourcePaths = AssetDatabase.FindAssets("t:Font", new[] { SourceDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) &&
                    Path.GetDirectoryName(path)?.Replace('\\', '/') == SourceDirectory)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (sourcePaths.Length == 0)
                throw new InvalidOperationException("No imported JetBrains Mono font faces found.");

            int created = 0;
            TMP_FontAsset regular = GetOrCreate(SourceDirectory + "/JetBrainsMono-Regular.ttf", true, ref created);
            TMP_FontAsset bold = GetOrCreate(SourceDirectory + "/JetBrainsMono-Bold.ttf", true, ref created);

            foreach (string sourcePath in sourcePaths)
            {
                if (sourcePath.EndsWith("JetBrainsMono-Regular.ttf", StringComparison.Ordinal) ||
                    sourcePath.EndsWith("JetBrainsMono-Bold.ttf", StringComparison.Ordinal))
                    continue;
                TMP_FontAsset face = GetOrCreate(sourcePath, false, ref created);
                AddFallback(face, regular);
            }
            AddFallback(bold, regular);

            TMP_Settings.defaultFontAsset = regular;
            EditorUtility.SetDirty(TMP_Settings.instance);

            int assigned = 0;
            foreach (TMP_Text text in UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!text.gameObject.scene.IsValid() ||
                    text.gameObject.scene.path != "Assets/_MainProject/Scenes/TennisPrototype.unity")
                    continue;
                if (text.font == null || text.font.name != "Roboto-Bold SDF")
                    continue;
                text.font = bold;
                text.fontSharedMaterial = bold.material;
                EditorUtility.SetDirty(text);
                assigned++;
            }

            AssetDatabase.SaveAssets();
            if (assigned > 0)
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            return $"created={created}; faces={sourcePaths.Length}; sceneTexts={assigned}; default={regular.name}";
        }

        private static TMP_FontAsset GetOrCreate(string sourcePath, bool bakeCharacters, ref int created)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
                throw new InvalidOperationException("Font import failed: " + sourcePath);

            string faceName = Path.GetFileNameWithoutExtension(sourcePath) + " SDF";
            string assetPath = OutputDirectory + "/" + faceName + ".asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            int atlasSize = bakeCharacters ? 2048 : 1024;
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source,
                bakeCharacters ? 90 : 80, bakeCharacters ? 9 : 8,
                GlyphRenderMode.SDF16, atlasSize, atlasSize,
                AtlasPopulationMode.Dynamic, !bakeCharacters);
            if (asset == null)
                throw new InvalidOperationException("TMP could not create font asset: " + sourcePath);
            asset.name = faceName;

            if (bakeCharacters)
            {
                string characters = BuildCharacterSet(source);
                if (!asset.TryAddCharacters(characters, out string missing) || !string.IsNullOrEmpty(missing))
                    throw new InvalidOperationException("TMP atlas is missing glyphs for " + faceName + ": " + missing);
                asset.atlasPopulationMode = AtlasPopulationMode.Static;
            }
            else
            {
                SerializedObject serialized = new SerializedObject(asset);
                SerializedProperty clearOnBuild = serialized.FindProperty("m_ClearDynamicDataOnBuild");
                if (clearOnBuild != null)
                {
                    clearOnBuild.boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Material material = asset.material;
            Texture2D[] textures = asset.atlasTextures;
            AssetDatabase.CreateAsset(asset, assetPath);
            if (material != null && !AssetDatabase.Contains(material))
            {
                material.name = faceName + " Material";
                AssetDatabase.AddObjectToAsset(material, asset);
            }
            if (textures != null)
            {
                foreach (Texture2D texture in textures)
                {
                    if (texture == null || AssetDatabase.Contains(texture)) continue;
                    texture.name = faceName + " Atlas";
                    AssetDatabase.AddObjectToAsset(texture, asset);
                }
            }
            EditorUtility.SetDirty(asset);
            created++;
            return asset;
        }

        private static string BuildCharacterSet(Font source)
        {
            var characters = new StringBuilder();
            for (int code = 32; code <= 126; code++)
                if (source.HasCharacter((char)code)) characters.Append((char)code);
            for (int code = 0x0410; code <= 0x044F; code++)
                if (source.HasCharacter((char)code)) characters.Append((char)code);
            foreach (char character in "Ёё№₽€£•–—…«»„“”×÷←→↑↓")
                if (source.HasCharacter(character)) characters.Append(character);
            return characters.ToString();
        }

        private static void AddFallback(TMP_FontAsset face, TMP_FontAsset regular)
        {
            if (face == null || face == regular) return;
            if (face.fallbackFontAssetTable == null)
                face.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (face.fallbackFontAssetTable.Contains(regular)) return;
            face.fallbackFontAssetTable.Add(regular);
            EditorUtility.SetDirty(face);
        }
    }
}
