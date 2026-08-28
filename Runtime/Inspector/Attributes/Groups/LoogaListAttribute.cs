using System;
using UnityEngine;

namespace LoogaSoft.Inspector.Runtime
{
    /// <summary>
    /// Defines how Looga Inspector displays a list.
    /// </summary>
    public enum LoogaListMode
    {
        /// <summary>
        /// Lets the user expand or collapse the list.
        /// </summary>
        Collapsible,

        /// <summary>
        /// Always shows the list elements and hides the foldout control.
        /// </summary>
        AlwaysExpanded
    }

    /// <summary>
    /// Draws an array or list with the Looga Inspector list interface.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LoogaListAttribute : PropertyAttribute, ILoogaAttribute
    {
        public LoogaListAttribute(LoogaListMode mode = LoogaListMode.Collapsible)
        {
            Mode = mode;
        }

        /// <summary>
        /// Gets the display mode declared for the list.
        /// </summary>
        public LoogaListMode Mode { get; }
    }
}
