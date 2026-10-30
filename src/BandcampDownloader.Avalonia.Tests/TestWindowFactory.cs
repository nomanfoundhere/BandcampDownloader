using System;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using BandcampDownloader.Core.DependencyInjection;

namespace BandcampDownloader.Avalonia.Tests;

/// <summary>
/// Constructs windows from the Avalonia head without hard-coding their constructor signatures
/// (the UI is being ported while the tests are written, so signatures may gain services).
/// </summary>
/// <remarks>
/// Each constructor parameter is resolved, in this order, from:
/// <list type="number">
/// <item>the explicit arguments passed to <see cref="Create" /> (matched by assignability);</item>
/// <item>the real dependency injection container (exactly like the application);</item>
/// <item>the parameter's default value, or the default value of its type.</item>
/// </list>
/// The constructor with the most parameters that can be satisfied is used; construction exceptions are
/// re-thrown with an informative message rather than silently swallowed.
/// </remarks>
internal static class TestWindowFactory
{
    private static readonly MethodInfo GetServiceMethod = typeof(DependencyInjectionHelper)
        .GetMethod("GetService", BindingFlags.Public | BindingFlags.Static)!;

    /// <summary>
    /// Creates a new instance of the specified window type.
    /// </summary>
    public static Window Create(Type windowType, params object?[] explicitArguments)
    {
        var constructors = windowType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .ToArray();

        if (constructors.Length == 0)
        {
            throw new InvalidOperationException($"{windowType.FullName} has no instance constructor");
        }

        var errors = new System.Collections.Generic.List<string>();

        foreach (var constructor in constructors)
        {
            try
            {
                var availableArguments = new System.Collections.Generic.List<object?>(explicitArguments);
                var arguments = constructor.GetParameters()
                    .Select(parameter => ResolveArgument(parameter, availableArguments))
                    .ToArray();

                var instance = constructor.Invoke(arguments);
                return (Window)instance;
            }
            catch (Exception exception)
            {
                var innerException = exception is TargetInvocationException { InnerException: not null } targetInvocationException
                    ? targetInvocationException.InnerException!
                    : exception;
                errors.Add($"{constructor}: {innerException.GetType().Name}: {innerException.Message}");
            }
        }

        throw new InvalidOperationException(
            $"Could not construct {windowType.FullName}:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    private static object? ResolveArgument(ParameterInfo parameter, System.Collections.Generic.List<object?> explicitArguments)
    {
        for (var i = 0; i < explicitArguments.Count; i++)
        {
            var explicitArgument = explicitArguments[i];
            if (explicitArgument is not null && parameter.ParameterType.IsInstanceOfType(explicitArgument))
            {
                explicitArguments.RemoveAt(i); // Each explicit argument is used at most once.
                return explicitArgument;
            }
        }

        var service = TryResolveService(parameter.ParameterType);
        if (service is not null)
        {
            return service;
        }

        if (parameter.HasDefaultValue)
        {
            return parameter.DefaultValue;
        }

        return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
    }

    private static object? TryResolveService(Type serviceType)
    {
        try
        {
            return GetServiceMethod.MakeGenericMethod(serviceType).Invoke(null, null);
        }
        catch (Exception)
        {
            // Not a registered service: fall through to the other resolution strategies.
            return null;
        }
    }
}
