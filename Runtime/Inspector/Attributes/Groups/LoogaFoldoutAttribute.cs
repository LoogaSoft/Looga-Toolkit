using System;

namespace LoogaSoft.Inspector.Runtime
{
    public enum LoogaBoxStyle
    {
        Small,
        Large
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LoogaFoldoutAttribute : Attribute, ILoogaAttribute
    {
        public readonly string Title;
        public readonly bool DefaultExpanded;

        public LoogaFoldoutAttribute(
            string title = null,
            bool defaultExpanded = false)
        {
            Title = title;
            DefaultExpanded = defaultExpanded;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LoogaFoldoutGroupAttribute : Attribute, ILoogaAttribute
    {
        public readonly string Title;
        public readonly bool DefaultExpanded;

        public LoogaFoldoutGroupAttribute(
            string title,
            bool defaultExpanded = true)
        {
            Title = title;
            DefaultExpanded = defaultExpanded;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LoogaFoldoutGroupEndAttribute : Attribute, ILoogaAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LoogaToggleFoldoutAttribute : Attribute, ILoogaAttribute
    {
        public readonly string Title;
        public readonly string TogglePropertyName;

        public LoogaToggleFoldoutAttribute(
            string title = null,
            string togglePropertyName = null)
        {
            Title = title;
            TogglePropertyName = togglePropertyName;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LoogaToggleFoldoutGroupAttribute : Attribute, ILoogaAttribute
    {
        public readonly string Title;

        public LoogaToggleFoldoutGroupAttribute(string title)
        {
            Title = title;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LoogaToggleFoldoutGroupEndAttribute : Attribute, ILoogaAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LoogaBoxAttribute : Attribute, ILoogaAttribute
    {
        public readonly string Title;
        public readonly LoogaBoxStyle Style;

        public LoogaBoxAttribute(
            string title = null,
            LoogaBoxStyle style = LoogaBoxStyle.Small)
        {
            Title = title;
            Style = style;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LoogaBoxGroupAttribute : Attribute, ILoogaAttribute
    {
        public readonly string Title;
        public readonly LoogaBoxStyle Style;

        public LoogaBoxGroupAttribute(
            string title,
            LoogaBoxStyle style = LoogaBoxStyle.Small)
        {
            Title = title;
            Style = style;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LoogaBoxGroupEndAttribute : Attribute, ILoogaAttribute
    {
    }
}

