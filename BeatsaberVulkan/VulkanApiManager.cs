using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Zenject;

namespace BeatsaberVulkan;

internal class VulkanApiManager : IInitializable
{
    private const string ForceVulkanArgument = "-force-vulkan";

    private static readonly HashSet<string> ConflictingGraphicsApiArguments = new(StringComparer.OrdinalIgnoreCase)
    {
        "-force-d3d11",
        "-force-d3d12",
        "-force-directx11",
        "-force-directx12",
        "-force-glcore",
        "-force-gles",
        "-force-gles20",
        "-force-gles30",
        "-force-metal",
        "-force-opengl"
    };

    private readonly PluginConfig pluginConfig;

    public VulkanApiManager(PluginConfig pluginConfig)
    {
        this.pluginConfig = pluginConfig;
    }

    public bool IsAutoRelaunchEnabled
    {
        get => pluginConfig.ForceVulkanOnLaunch;
        set
        {
            pluginConfig.ForceVulkanOnLaunch = value;
            pluginConfig.Changed();
        }
    }

    public GraphicsDeviceType CurrentGraphicsApi => SystemInfo.graphicsDeviceType;

    public bool IsUsingVulkan => CurrentGraphicsApi == GraphicsDeviceType.Vulkan;

    public string StatusText =>
        IsUsingVulkan
            ? "Graphics API: Vulkan"
            : $"Graphics API: {CurrentGraphicsApi}. Relaunch required for Vulkan.";

    public void Initialize()
    {
        Plugin.Log.Info($"Current graphics API: {CurrentGraphicsApi}");

        if (!pluginConfig.ForceVulkanOnLaunch)
        {
            Plugin.Log.Info("Automatic Vulkan relaunch is disabled in the config.");
            return;
        }

        RelaunchIfNeeded();
    }

    public bool RelaunchIfNeeded()
    {
        if (IsUsingVulkan)
        {
            Plugin.Log.Info("Beat Saber is already running with Vulkan.");
            return false;
        }

        if (HasArgument(Environment.GetCommandLineArgs(), ForceVulkanArgument))
        {
            Plugin.Log.Warn(
                $"Beat Saber was started with {ForceVulkanArgument}, but Unity reports {CurrentGraphicsApi}. " +
                "Not relaunching again to avoid a loop.");
            return false;
        }

        return RelaunchWithVulkan();
    }

    public bool RelaunchWithVulkan()
    {
        var executablePath = GetExecutablePath();
        if (executablePath is null)
        {
            Plugin.Log.Error($"Could not find Beat Saber's executable path, so {ForceVulkanArgument} could not be applied.");
            return false;
        }

        var arguments = BuildRelaunchArguments(Environment.GetCommandLineArgs());
        var workingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            });

            Plugin.Log.Info($"Relaunched Beat Saber with {ForceVulkanArgument}. Closing this process.");
            Application.Quit();
            return true;
        }
        catch (Exception exception)
        {
            Plugin.Log.Error($"Failed to relaunch Beat Saber with {ForceVulkanArgument}: {exception}");
            return false;
        }
    }

    private static bool HasArgument(IEnumerable<string> arguments, string targetArgument) =>
        arguments.Any(argument => string.Equals(argument, targetArgument, StringComparison.OrdinalIgnoreCase));

    private static string BuildRelaunchArguments(IEnumerable<string> commandLineArguments)
    {
        var arguments = commandLineArguments
            .Skip(1)
            .Where(argument => !ConflictingGraphicsApiArguments.Contains(argument))
            .ToList();

        if (!HasArgument(arguments, ForceVulkanArgument))
        {
            arguments.Add(ForceVulkanArgument);
        }

        return string.Join(" ", arguments.Select(QuoteArgument));
    }

    private static string? GetExecutablePath()
    {
        try
        {
            var processPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                return processPath;
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.Debug($"Could not read the current process path from MainModule: {exception.Message}");
        }

        var firstCommandLineArgument = Environment.GetCommandLineArgs().FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstCommandLineArgument) && File.Exists(firstCommandLineArgument))
        {
            return Path.GetFullPath(firstCommandLineArgument);
        }

        return GetExecutablePathFromUnityDataPath();
    }

    private static string? GetExecutablePathFromUnityDataPath()
    {
        const string dataDirectorySuffix = "_Data";
        var dataPath = Application.dataPath;
        var dataDirectoryName = Path.GetFileName(dataPath);

        if (string.IsNullOrWhiteSpace(dataDirectoryName) ||
            !dataDirectoryName.EndsWith(dataDirectorySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var gameDirectory = Path.GetDirectoryName(dataPath);
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            return null;
        }

        var executableName = dataDirectoryName[..^dataDirectorySuffix.Length] + ".exe";
        var executablePath = Path.Combine(gameDirectory, executableName);
        return File.Exists(executablePath) ? executablePath : null;
    }

    private static string QuoteArgument(string argument)
    {
        if (argument.Length > 0 && argument.All(character => !char.IsWhiteSpace(character) && character != '"'))
        {
            return argument;
        }

        var quotedArgument = new StringBuilder("\"");
        var backslashCount = 0;

        foreach (var character in argument)
        {
            switch (character)
            {
                case '\\':
                    backslashCount++;
                    break;
                case '"':
                    quotedArgument.Append('\\', backslashCount * 2 + 1);
                    quotedArgument.Append(character);
                    backslashCount = 0;
                    break;
                default:
                    quotedArgument.Append('\\', backslashCount);
                    quotedArgument.Append(character);
                    backslashCount = 0;
                    break;
            }
        }

        quotedArgument.Append('\\', backslashCount * 2);
        quotedArgument.Append('"');
        return quotedArgument.ToString();
    }
}
