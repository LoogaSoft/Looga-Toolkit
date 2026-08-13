using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace LoogaSoft.Tools.Editor
{
    /// <summary>Renders formatted Markdown without Unity's nested imported-object inspector.</summary>
    [CustomEditor(typeof(MarkdownTextAssetImporter))]
    public sealed class MarkdownTextAssetEditor : ScriptedImporterEditor
    {
        private const int HorizontalInset = 4;
        private const float ImageMaximumHeight = 360f;

        private static readonly Regex LinkPattern = new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex ImagePattern = new(@"^!\[([^\]]*)\]\(([^)]+)\)\s*$", RegexOptions.Compiled);
        private static readonly Regex OrderedListPattern = new(@"^(\s*)(\d+)[.)]\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex UnorderedListPattern = new(@"^(\s*)[-+*]\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex TableDividerPattern = new(
            @"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$",
            RegexOptions.Compiled);

        private readonly Dictionary<string, Texture2D> _imageCache = new(StringComparer.Ordinal);

        private static GUIStyle BodyStyle => MarkdownStyles.Body;
        private static GUIStyle CodeStyle => MarkdownStyles.Code;

        public override bool showImportedObject => false;
        protected override bool needsApplyRevert => false;

        public override void OnEnable()
        {
            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            string assetPath = ((MarkdownTextAssetImporter)target).assetPath;
            string markdown = File.Exists(assetPath) ? File.ReadAllText(assetPath) : string.Empty;

            bool wasEnabled = GUI.enabled;
            GUI.enabled = true;
            try
            {
                DrawMarkdown(markdown, assetPath);
            }
            finally
            {
                GUI.enabled = wasEnabled;
            }
        }

        private void DrawMarkdown(string markdown, string assetPath)
        {
            string normalized = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            for (int index = 0; index < lines.Length;)
            {
                string line = lines[index];
                string trimmed = line.Trim();

                if (trimmed.Length == 0)
                {
                    EditorGUILayout.Space(4f);
                    index++;
                    continue;
                }

                if (TryDrawFence(lines, ref index) ||
                    TryDrawHeading(line, ref index) ||
                    TryDrawRule(trimmed, ref index) ||
                    TryDrawImage(trimmed, assetPath, ref index) ||
                    TryDrawTable(lines, ref index) ||
                    TryDrawQuote(lines, ref index) ||
                    TryDrawList(line, ref index))
                {
                    continue;
                }

                DrawParagraph(lines, ref index);
            }

            EditorGUILayout.EndVertical();
        }

        private static bool TryDrawFence(string[] lines, ref int index)
        {
            string trimmed = lines[index].TrimStart();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal) &&
                !trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                return false;
            }

            string fence = trimmed[..3];
            StringBuilder code = new();
            index++;
            while (index < lines.Length && !lines[index].TrimStart().StartsWith(fence, StringComparison.Ordinal))
            {
                if (code.Length > 0)
                    code.AppendLine();

                code.Append(lines[index]);
                index++;
            }

            if (index < lines.Length)
                index++;

            DrawSelectableBlock(code.ToString(), CodeStyle);
            EditorGUILayout.Space(4f);
            return true;
        }

        private static bool TryDrawHeading(string line, ref int index)
        {
            string trimmed = line.TrimStart();
            int level = 0;
            while (level < trimmed.Length && level < 6 && trimmed[level] == '#')
                level++;

            if (level == 0 || level >= trimmed.Length || !char.IsWhiteSpace(trimmed[level]))
                return false;

            GUIStyle style = level switch
            {
                1 => MarkdownStyles.Heading1,
                2 => MarkdownStyles.Heading2,
                3 => MarkdownStyles.Heading3,
                _ => MarkdownStyles.Heading4
            };
            DrawRichLabel(trimmed[(level + 1)..].Trim(), style);
            index++;
            return true;
        }

        private static bool TryDrawRule(string trimmed, ref int index)
        {
            string compact = trimmed.Replace(" ", string.Empty);
            if (compact.Length < 3 || !IsRepeated(compact, '-') && !IsRepeated(compact, '*') && !IsRepeated(compact, '_'))
                return false;

            Rect rect = EditorGUILayout.GetControlRect(false, 9f);
            rect.y += 4f;
            rect.height = EditorGUIUtility.pixelsPerPoint > 1f ? 1f : 1f / EditorGUIUtility.pixelsPerPoint;
            EditorGUI.DrawRect(rect, MarkdownStyles.RuleColor);
            index++;
            return true;
        }

        private bool TryDrawImage(string trimmed, string assetPath, ref int index)
        {
            Match match = ImagePattern.Match(trimmed);
            if (!match.Success)
                return false;

            string altText = match.Groups[1].Value;
            string location = match.Groups[2].Value.Trim();
            Texture2D texture = ResolveImage(assetPath, location);
            if (texture != null)
            {
                float availableWidth = Math.Max(32f, EditorGUIUtility.currentViewWidth - 36f);
                float height = Math.Min(ImageMaximumHeight, availableWidth * texture.height / Math.Max(1f, texture.width));
                Rect imageRect = EditorGUILayout.GetControlRect(false, Math.Max(32f, height));
                GUI.DrawTexture(imageRect, texture, ScaleMode.ScaleToFit, true);
                if (!string.IsNullOrWhiteSpace(altText))
                    DrawRichLabel(altText, MarkdownStyles.Caption);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(EditorGUIUtility.IconContent("console.infoicon"), GUILayout.Width(22f));
                    GUILayout.Label(string.IsNullOrWhiteSpace(altText) ? location : altText, BodyStyle);
                    if (IsWebLink(location) && GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(48f)))
                        Application.OpenURL(location);
                }
            }

            index++;
            return true;
        }

        private static bool TryDrawTable(string[] lines, ref int index)
        {
            if (index + 1 >= lines.Length || !lines[index].Contains('|') ||
                !TableDividerPattern.IsMatch(lines[index + 1]))
            {
                return false;
            }

            string[] headers = SplitTableRow(lines[index]);
            DrawTableRow(headers, true);
            index += 2;
            while (index < lines.Length && lines[index].Contains('|') && !string.IsNullOrWhiteSpace(lines[index]))
            {
                DrawTableRow(SplitTableRow(lines[index]), false);
                index++;
            }

            EditorGUILayout.Space(4f);
            return true;
        }

        private static bool TryDrawQuote(string[] lines, ref int index)
        {
            if (!lines[index].TrimStart().StartsWith(">", StringComparison.Ordinal))
                return false;

            StringBuilder quote = new();
            while (index < lines.Length && lines[index].TrimStart().StartsWith(">", StringComparison.Ordinal))
            {
                string line = lines[index].TrimStart()[1..].TrimStart();
                if (quote.Length > 0)
                    quote.Append(' ');
                quote.Append(line);
                index++;
            }

            Rect rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), MarkdownStyles.QuoteColor);
            DrawRichLabel(quote.ToString(), MarkdownStyles.Quote);
            EditorGUILayout.EndVertical();
            return true;
        }

        private static bool TryDrawList(string line, ref int index)
        {
            Match ordered = OrderedListPattern.Match(line);
            Match unordered = UnorderedListPattern.Match(line);
            if (!ordered.Success && !unordered.Success)
                return false;

            Match match = ordered.Success ? ordered : unordered;
            int spaces = match.Groups[1].Value.Replace("\t", "    ").Length;
            int depth = Math.Min(4, spaces / 2);
            string marker = ordered.Success ? $"{match.Groups[2].Value}." : "\u2022";
            string content = ordered.Success ? match.Groups[3].Value : match.Groups[2].Value;
            float markerWidth = ordered.Success ? 28f : 14f;
            float indentation = HorizontalInset + depth * 14f;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(indentation);
                GUILayout.Label(marker, MarkdownStyles.ListMarker, GUILayout.Width(markerWidth));
                DrawRichLabel(content, BodyStyle, false, indentation + markerWidth);
            }

            index++;
            return true;
        }

        private static void DrawParagraph(string[] lines, ref int index)
        {
            StringBuilder paragraph = new();
            while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]) &&
                   (paragraph.Length == 0 || !StartsBlock(lines, index)))
            {
                if (paragraph.Length > 0)
                    paragraph.Append(' ');
                paragraph.Append(lines[index].Trim());
                index++;
            }

            DrawRichLabel(paragraph.ToString(), BodyStyle);
        }

        private static bool StartsBlock(string[] lines, int index)
        {
            string line = lines[index];
            string trimmed = line.TrimStart();
            return trimmed.StartsWith("#", StringComparison.Ordinal) ||
                   trimmed.StartsWith("```", StringComparison.Ordinal) ||
                   trimmed.StartsWith("~~~", StringComparison.Ordinal) ||
                   trimmed.StartsWith(">", StringComparison.Ordinal) ||
                   ImagePattern.IsMatch(trimmed) ||
                   OrderedListPattern.IsMatch(line) ||
                   UnorderedListPattern.IsMatch(line) ||
                   index + 1 < lines.Length && line.Contains('|') && TableDividerPattern.IsMatch(lines[index + 1]);
        }

        private static void DrawRichLabel(
            string markdown,
            GUIStyle style,
            bool expandWidth = true,
            float reservedHorizontalSpace = 0f)
        {
            string richText = FormatInline(markdown, out string link);
            GUIContent content = new(richText, string.IsNullOrWhiteSpace(link) ? string.Empty : link);
            float width = Math.Max(1f, EditorGUIUtility.currentViewWidth - 38f - reservedHorizontalSpace);
            float height = Math.Max(EditorGUIUtility.singleLineHeight, Mathf.Ceil(style.CalcHeight(content, width)) + 2f);
            Rect rect = GUILayoutUtility.GetRect(content, style, GUILayout.Height(height), GUILayout.ExpandWidth(expandWidth));
            GUI.Label(rect, content, style);

            if (string.IsNullOrWhiteSpace(link))
                return;

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            Event current = Event.current;
            if (current.type == EventType.MouseUp && current.button == 0 && rect.Contains(current.mousePosition))
            {
                OpenLink(link);
                current.Use();
            }
        }

        private static void DrawSelectableBlock(string text, GUIStyle style)
        {
            GUIContent content = new(text ?? string.Empty);
            float width = Math.Max(1f, EditorGUIUtility.currentViewWidth - 44f);
            float height = Math.Max(32f, style.CalcHeight(content, width) + 8f);
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            rect.xMin += 7f;
            rect.xMax -= 7f;
            rect.yMin += 4f;
            rect.yMax -= 4f;
            EditorGUI.SelectableLabel(rect, content.text, style);
        }

        private Texture2D ResolveImage(string markdownAssetPath, string location)
        {
            if (string.IsNullOrWhiteSpace(location) || IsWebLink(location))
                return null;

            string decoded = Uri.UnescapeDataString(location.Split('#')[0]).Replace('\\', '/');
            string candidate = decoded.StartsWith("Assets/", StringComparison.Ordinal) ||
                               decoded.StartsWith("Packages/", StringComparison.Ordinal)
                ? decoded
                : NormalizeAssetPath(Path.Combine(Path.GetDirectoryName(markdownAssetPath) ?? string.Empty, decoded));

            if (_imageCache.TryGetValue(candidate, out Texture2D cached))
                return cached;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(candidate);
            _imageCache[candidate] = texture;
            return texture;
        }

        private static string FormatInline(string markdown, out string firstLink)
        {
            firstLink = string.Empty;
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            string escaped = EscapeRichText(markdown);
            Match linkMatch = LinkPattern.Match(markdown);
            if (linkMatch.Success)
                firstLink = linkMatch.Groups[2].Value.Trim();

            escaped = LinkPattern.Replace(
                escaped,
                match => $"<color=#{ColorUtility.ToHtmlStringRGB(MarkdownStyles.LinkColor)}>{match.Groups[1].Value}</color>");
            string codeColor = ColorUtility.ToHtmlStringRGB(MarkdownStyles.InlineCodeColor);
            string emphasisColor = ColorUtility.ToHtmlStringRGB(MarkdownStyles.EmphasisColor);
            escaped = Regex.Replace(escaped, @"`([^`]+)`", $"<color=#{codeColor}><b>$1</b></color>");
            escaped = Regex.Replace(escaped, @"\*\*(.+?)\*\*", $"<color=#{emphasisColor}><b>$1</b></color>");
            escaped = Regex.Replace(escaped, @"__(.+?)__", $"<color=#{emphasisColor}><b>$1</b></color>");
            escaped = Regex.Replace(escaped, @"(?<!\*)\*([^*]+)\*(?!\*)", "<i>$1</i>");
            escaped = Regex.Replace(escaped, @"(?<!_)_([^_]+)_(?!_)", "<i>$1</i>");
            escaped = Regex.Replace(escaped, @"~~(.+?)~~", "<s>$1</s>");
            return escaped;
        }

        private static string EscapeRichText(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static void OpenLink(string location)
        {
            if (IsWebLink(location))
            {
                Application.OpenURL(location);
                return;
            }

            string normalized = NormalizeAssetPath(location);
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalized);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        private static void DrawTableRow(IReadOnlyList<string> cells, bool header)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUIStyle style = header ? MarkdownStyles.TableHeader : MarkdownStyles.TableCell;
                for (int index = 0; index < cells.Count; index++)
                    GUILayout.Label(FormatInline(cells[index], out _), style, GUILayout.ExpandWidth(true));
            }
        }

        private static string[] SplitTableRow(string line)
        {
            return line.Trim().Trim('|').Split(new[] { '|' }, StringSplitOptions.None);
        }

        private static bool IsRepeated(string value, char character)
        {
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] != character)
                    return false;
            }

            return true;
        }

        private static bool IsWebLink(string value)
        {
            return value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> resolved = new(segments.Length);
            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                if (segment == ".")
                    continue;

                if (segment == "..")
                {
                    if (resolved.Count > 0)
                        resolved.RemoveAt(resolved.Count - 1);
                    continue;
                }

                resolved.Add(segment);
            }

            return string.Join("/", resolved);
        }

        private static class MarkdownStyles
        {
            public static readonly Color LinkColor = EditorGUIUtility.isProSkin
                ? new Color(0.40f, 0.68f, 0.96f)
                : new Color(0.05f, 0.36f, 0.72f);
            public static readonly Color InlineCodeColor = EditorGUIUtility.isProSkin
                ? new Color(0.84f, 0.80f, 0.72f)
                : new Color(0.32f, 0.29f, 0.25f);
            public static readonly Color EmphasisColor = EditorGUIUtility.isProSkin
                ? new Color(0.92f, 0.92f, 0.92f)
                : new Color(0.15f, 0.15f, 0.15f);
            public static readonly Color QuoteColor = EditorGUIUtility.isProSkin
                ? new Color(0.32f, 0.55f, 0.78f)
                : new Color(0.16f, 0.40f, 0.66f);
            public static readonly Color RuleColor = EditorGUIUtility.isProSkin
                ? new Color(0.30f, 0.30f, 0.30f)
                : new Color(0.68f, 0.68f, 0.68f);

            public static readonly GUIStyle Body = CreateLabel(EditorStyles.label, 0, FontStyle.Normal, 3, 3);
            public static readonly GUIStyle Heading1 = CreateLabel(EditorStyles.boldLabel, 8, FontStyle.Bold, 10, 5);
            public static readonly GUIStyle Heading2 = CreateLabel(EditorStyles.boldLabel, 5, FontStyle.Bold, 8, 4);
            public static readonly GUIStyle Heading3 = CreateLabel(EditorStyles.boldLabel, 2, FontStyle.Bold, 6, 3);
            public static readonly GUIStyle Heading4 = CreateLabel(EditorStyles.boldLabel, 0, FontStyle.Bold, 5, 2);
            public static readonly GUIStyle Caption = CreateLabel(EditorStyles.centeredGreyMiniLabel, 0, FontStyle.Italic, 2, 5);
            public static readonly GUIStyle Quote = CreateLabel(EditorStyles.label, 0, FontStyle.Italic, 4, 4);
            public static readonly GUIStyle ListMarker = CreateLabel(EditorStyles.label, 0, FontStyle.Bold, 3, 3);
            public static readonly GUIStyle TableHeader = CreateLabel(EditorStyles.boldLabel, 0, FontStyle.Bold, 2, 2);
            public static readonly GUIStyle TableCell = CreateLabel(EditorStyles.label, 0, FontStyle.Normal, 2, 2);
            public static readonly GUIStyle Code = new(EditorStyles.label)
            {
                wordWrap = true,
                richText = false,
                font = EditorStyles.textArea.font,
                padding = new RectOffset(0, 0, 0, 0)
            };
            private static GUIStyle CreateLabel(
                GUIStyle source,
                int fontSizeIncrease,
                FontStyle fontStyle,
                int topMargin,
                int bottomMargin)
            {
                GUIStyle style = new(source)
                {
                    richText = true,
                    wordWrap = true,
                    fontStyle = fontStyle,
                    fontSize = Math.Max(1, source.fontSize + fontSizeIncrease),
                    margin = new RectOffset(HorizontalInset, HorizontalInset, topMargin, bottomMargin)
                };
                return style;
            }
        }
    }
}
