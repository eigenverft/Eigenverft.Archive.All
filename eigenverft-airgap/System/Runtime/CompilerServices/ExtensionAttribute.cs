using System;
using System.Collections.Generic;
using System.Text;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Shim für <see cref="System.Runtime.CompilerServices.ExtensionAttribute"/> zur Unterstützung von Extension Methods,
    /// wenn gegen .NET 2.0 kompiliert wird und <c>System.Core</c> nicht verfügbar ist.
    /// </summary>
    /// <remarks>
    /// Nur einbinden, wenn die echte Definition nicht vorhanden ist. In neueren Targets entfernen oder
    /// per Conditional Compilation ausschließen, um Typkonflikte zu vermeiden.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Beispiel: einfache Extension Method
    /// public static class StringExtensions
    /// {
    ///     // Der C#-Compiler markiert die Methode automatisch mit ExtensionAttribute.
    ///     public static bool IsNullOrEmpty(this string value)
    ///     {
    ///         // [Implementation elided]
    ///         return value == null || value.Length == 0;
    ///     }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class ExtensionAttribute : Attribute
    {
        // Reviewer note: No members required; the compiler and reflection only need the type identity.
    }
}
