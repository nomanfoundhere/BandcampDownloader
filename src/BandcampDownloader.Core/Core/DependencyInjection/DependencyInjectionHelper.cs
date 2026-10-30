using System;
using Microsoft.Extensions.DependencyInjection;

namespace BandcampDownloader.Core.DependencyInjection;

internal static class DependencyInjectionHelper
{
    private static Container _container;

    /// <summary>
    /// Initializes the dependency injection container with the core services and the specified
    /// platform-specific services.
    /// </summary>
    /// <param name="configurePlatformServices">Callback used to register platform-specific services.</param>
    public static IContainer InitializeContainer(Action<IServiceCollection> configurePlatformServices = null)
    {
        _container = new Container(configurePlatformServices);
        return _container;
    }

    /// <summary>
    /// DO NOT USE unless dependency injection is not possible.
    /// </summary>
    public static T GetService<T>()
    {
        return _container.GetService<T>();
    }
}
