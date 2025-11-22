using System;

using Microsoft.Extensions.Configuration;

namespace Eigenverft.Routed.Core.Extensions.ConfigurationExtensions
{
    public static partial class ConfigurationExtensions
    {
        /// <summary>
        /// Clears existing configuration providers.
        /// </summary>
        /// <remarks>
        /// Removes all sources from <see cref="IConfigurationBuilder.Sources"/>.
        /// </remarks>
        /// <param name="configuration">The <see cref="IConfigurationBuilder"/> to modify.</param>
        /// <returns>The same <see cref="IConfigurationBuilder"/> instance.</returns>
        /// <example>
        /// <code>
        /// var builder = WebApplication.CreateBuilder(args);
        /// builder.Configuration.Clear();
        /// </code>
        /// </example>
        public static IConfigurationBuilder Clear(this IConfigurationBuilder configuration)
        {
            // Implementation intentionally omitted per review-guideline: focus on the public contract.
            configuration.Sources.Clear();
            return configuration;
        }
    }
}
