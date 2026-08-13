using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace LoogaSoft.Tools.Editor
{
    /// <summary>Assigns the Markdown importer to project Markdown files.</summary>
    [InitializeOnLoad]
    internal static class MarkdownTextAssetImporterRegistration
    {
        private const string ProjectAssetPrefix = "Assets/";

        private static readonly HashSet<string> PendingPaths = new(StringComparer.OrdinalIgnoreCase);

        static MarkdownTextAssetImporterRegistration()
        {
            EditorApplication.delayCall += RegisterExistingMarkdown;
        }

        private static void RegisterExistingMarkdown()
        {
            foreach (string assetPath in AssetDatabase.GetAllAssetPaths())
            {
                QueueIfMarkdown(assetPath);
            }

            ApplyPendingOverrides();
        }

        internal static void QueueImportedAssets(IEnumerable<string> assetPaths)
        {
            foreach (string assetPath in assetPaths)
            {
                QueueIfMarkdown(assetPath);
            }

            if (PendingPaths.Count == 0)
            {
                return;
            }

            EditorApplication.delayCall -= ApplyPendingOverrides;
            EditorApplication.delayCall += ApplyPendingOverrides;
        }

        private static void QueueIfMarkdown(string assetPath)
        {
            if (assetPath.StartsWith(ProjectAssetPrefix, StringComparison.Ordinal) &&
                string.Equals(Path.GetExtension(assetPath), ".md", StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.GetImporterOverride(assetPath) != typeof(MarkdownTextAssetImporter))
            {
                PendingPaths.Add(assetPath);
            }
        }

        private static void ApplyPendingOverrides()
        {
            if (PendingPaths.Count == 0)
            {
                return;
            }

            string[] assetPaths = new string[PendingPaths.Count];
            PendingPaths.CopyTo(assetPaths);
            PendingPaths.Clear();

            foreach (string assetPath in assetPaths)
            {
                if (AssetDatabase.GetImporterOverride(assetPath) != typeof(MarkdownTextAssetImporter))
                {
                    AssetDatabase.SetImporterOverride<MarkdownTextAssetImporter>(assetPath);
                }
            }
        }
    }

    /// <summary>Assigns the Markdown importer to Markdown files added during this editor session.</summary>
    internal sealed class MarkdownTextAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            MarkdownTextAssetImporterRegistration.QueueImportedAssets(importedAssets);
            MarkdownTextAssetImporterRegistration.QueueImportedAssets(movedAssets);
        }
    }

    /// <summary>Imports Markdown as a normal text asset so references remain compatible.</summary>
    [ScriptedImporter(2, null, new[] { "md" }, AllowCaching = true)]
    public sealed class MarkdownTextAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            string markdown = File.ReadAllText(context.assetPath);
            TextAsset asset = new(markdown)
            {
                name = Path.GetFileNameWithoutExtension(context.assetPath)
            };

            context.AddObjectToAsset("Markdown", asset);
            context.SetMainObject(asset);
        }
    }
}
