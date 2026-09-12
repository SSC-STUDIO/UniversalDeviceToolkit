using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using UniversalDeviceToolkit.Abstractions.Localization;
using UniversalDeviceToolkit.CLI.Lib;

namespace UniversalDeviceToolkit.CLI;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            CliOutput.Json = args.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
            IpcClient.FastFail = CliOutput.Json;
            var culture = LocalizationRuntime.Initialize(ReadLanguageOverride(args), persist: false);
            Strings.ApplyCulture(culture);
            return await BuildCommandLine()
                .Parse(args)
                .InvokeAsync(new InvocationConfiguration
                {
                    EnableDefaultExceptionHandler = false
                }, default)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return OnException(ex);
        }
    }

    private static RootCommand BuildCommandLine()
    {
        var root = new RootCommand(Strings.Get(
            "CLI_Header_RootCommandDescription",
            "Utility that controls Universal Device Toolkit from command line.\n\n" +
            "Universal Device Toolkit must be running in the background and CLI setting must be " +
            "turned on for this utility to work."));
        root.Options.Add(new Option<string?>("--language")
        {
            Description = Strings.Get("CLI_Option_Language_Description", "Temporarily select the display language.")
        });
        var jsonOption = new Option<bool>("--json")
        {
            Description = Strings.Get("CLI_Option_Json_Description", "Write a single JSON object to stdout."),
            Recursive = true,
        };
        root.Options.Add(jsonOption);

        root.Add(BuildDoctorCommand());
        root.Add(BuildQuickActionsCommand());
        root.Add(BuildFeatureCommand());
        root.Add(BuildSpectrumCommand());
        root.Add(BuildRGBCommand());
        root.Add(BuildShellCommand());
        root.Add(BuildNetworkAccelerationCommand());
        root.Add(BuildStatusCommand());

        return root;
    }

    private static string? ReadLanguageOverride(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.StartsWith("--language=", StringComparison.OrdinalIgnoreCase))
                return argument["--language=".Length..];

            if (argument.Equals("--language", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count)
                return args[index + 1];
        }

        return null;
    }

    private static Command BuildDoctorCommand()
    {
        var cmd = new Command("doctor", Strings.Get(
            "CLI_Command_Doctor_Description",
            "Inspect CLI readiness without talking to the running app"));
        cmd.SetAction(_ =>
        {
            var report = CliDoctor.Inspect();
            if (CliOutput.Json)
            {
                CliOutput.Write(CliDoctor.ToJsonPayload(report));
                return;
            }

            CliDoctor.WriteHuman(report);
        });
        return cmd;
    }

    private static Command BuildStatusCommand()
    {
        var cmd = new Command("status", Strings.Get(
            "CLI_Command_Status_Description",
            "Show running app status and startup-related switches"));
        cmd.Aliases.Add("st");
        cmd.SetAction(async _ =>
        {
            var result = await IpcClient.GetAppStatusAsync().ConfigureAwait(false);
            CliOutput.Success("status", result);
        });

        return cmd;
    }

    private static Command BuildQuickActionsCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = Strings.Get("CLI_Argument_QaName_Description", "Name of the Quick Action"),
            Arity = ArgumentArity.ZeroOrOne
        };

        var listOption = new Option<bool>("--list", "-l")
        {
            Description = Strings.Get("CLI_Option_QaList_Description", "List available Quick Actions"),
            Arity = ArgumentArity.ZeroOrOne
        };

        var cmd = new Command("quickAction", Strings.Get("CLI_Command_QuickAction_Description", "Run Quick Action"));
        cmd.Aliases.Add("qa");
        cmd.Add(nameArgument);
        cmd.Add(listOption);
        cmd.SetAction(async parseResult =>
        {
            if (parseResult.GetValue(listOption))
            {
                var result = await IpcClient.ListQuickActionsAsync().ConfigureAwait(false);
                CliOutput.SuccessList("quickAction.list", result);
                return;
            }

            var name = parseResult.GetRequiredValue(nameArgument);
            await IpcClient.RunQuickActionAsync(name).ConfigureAwait(false);
            CliOutput.Success("quickAction.run", name: name);
        });
        cmd.Validators.Add(result =>
        {
            if (HasArgument(result, nameArgument))
                return;

            if (HasOption(result, listOption))
                return;

            result.AddError(Strings.Get(
                "CLI_Error_QaNameOrList_Required",
                $"{nameArgument.Name} or --list should be specified",
                nameArgument.Name!));
        });

        return cmd;
    }

    private static Command BuildFeatureCommand()
    {
        var getCmd = BuildGetFeatureCommand();
        var setCmd = BuildSetFeatureCommand();

        var listOption = new Option<bool>("--list", "-l")
        {
            Description = Strings.Get("CLI_Option_FeatureList_Description", "List available features"),
            Arity = ArgumentArity.ZeroOrOne
        };

        var cmd = new Command("feature", Strings.Get("CLI_Command_Feature_Description", "Control features"));
        cmd.Aliases.Add("f");
        cmd.Add(getCmd);
        cmd.Add(setCmd);
        cmd.Add(listOption);
        cmd.SetAction(async parseResult =>
        {
            if (!parseResult.GetValue(listOption))
                return;

            var value = await IpcClient.ListFeaturesAsync().ConfigureAwait(false);
            CliOutput.SuccessList("feature.list", value);
        });
        cmd.Validators.Add(result =>
        {
            if (HasCommand(result, getCmd))
                return;

            if (HasCommand(result, setCmd))
                return;

            if (HasOption(result, listOption))
                return;

            result.AddError(Strings.Get(
                "CLI_Error_FeatureSubcommandOrList_Required",
                $"{getCmd.Name}, {setCmd.Name} or --list should be specified",
                getCmd.Name!,
                setCmd.Name!));
        });

        return cmd;
    }

    private static Command BuildGetFeatureCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = Strings.Get("CLI_Argument_FeatureName_Description", "Name of the feature"),
            Arity = ArgumentArity.ExactlyOne
        };

        var cmd = new Command("get", Strings.Get("CLI_Command_GetFeature_Description", "Get value of a feature"));
        cmd.Aliases.Add("g");
        cmd.Add(nameArgument);
        cmd.SetAction(async parseResult =>
        {
            var name = parseResult.GetRequiredValue(nameArgument);
            var result = await IpcClient.GetFeatureValueAsync(name).ConfigureAwait(false);
            CliOutput.Success("feature.get", result, name);
        });

        return cmd;
    }

    private static Command BuildSetFeatureCommand()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = Strings.Get("CLI_Argument_FeatureName_Description", "Name of the feature"),
            Arity = ArgumentArity.ExactlyOne
        };
        var valueArgument = new Argument<string>("value")
        {
            Description = Strings.Get("CLI_Argument_FeatureValue_Description", "Value of the feature"),
            Arity = ArgumentArity.ZeroOrOne
        };

        var listOption = new Option<bool>("--list", "-l")
        {
            Description = Strings.Get("CLI_Option_FeatureValueList_Description", "List available feature values"),
            Arity = ArgumentArity.ZeroOrOne
        };

        var cmd = new Command("set", Strings.Get("CLI_Command_SetFeature_Description", "Set value of a feature"));
        cmd.Aliases.Add("s");
        cmd.Add(nameArgument);
        cmd.Add(valueArgument);
        cmd.Add(listOption);
        cmd.SetAction(async parseResult =>
        {
            var name = parseResult.GetValue(nameArgument);

            if (parseResult.GetValue(listOption))
            {
                var result = await IpcClient.ListFeatureValuesAsync(name!).ConfigureAwait(false);
                CliOutput.SuccessList("feature.values", result, name);
                return 0;
            }

            var value = parseResult.GetValue(valueArgument);
            if (string.IsNullOrEmpty(value))
                return CliOutput.Fail("invalid", $"A value is required for feature '{name}'.", "feature.set");

            await IpcClient.SetFeatureValueAsync(name!, value).ConfigureAwait(false);
            CliOutput.Success("feature.set", value, name);
            return 0;
        });
        cmd.Validators.Add(result =>
        {
            if (HasArgument(result, nameArgument))
                return;

            if (HasOption(result, listOption))
                return;

            result.AddError(Strings.Get(
                "CLI_Error_FeatureNameOrList_Required",
                $"{nameArgument.Name} or --list should be specified",
                nameArgument.Name!));
        });

        return cmd;
    }

    private static Command BuildSpectrumCommand()
    {
        var profileCommand = BuildSpectrumProfileCommand();
        var brightnessCommand = BuildSpectrumBrightnessCommand();

        var cmd = new Command("spectrum", Strings.Get("CLI_Command_Spectrum_Description", "Control Spectrum backlight"));
        cmd.Aliases.Add("s");
        cmd.Add(profileCommand);
        cmd.Add(brightnessCommand);
        return cmd;
    }

    private static Command BuildSpectrumProfileCommand()
    {
        var getCmd = BuildGetSpectrumProfileCommand();
        var setCmd = BuildSetSpectrumProfileCommand();

        var cmd = new Command("profile", Strings.Get("CLI_Command_SpectrumProfile_Description", "Control Spectrum backlight profile"));
        cmd.Aliases.Add("p");
        cmd.Add(getCmd);
        cmd.Add(setCmd);

        return cmd;
    }

    private static Command BuildGetSpectrumProfileCommand()
    {
        var cmd = new Command("get", Strings.Get("CLI_Command_GetSpectrumProfile_Description", "Get current Spectrum profile"));
        cmd.Aliases.Add("g");
        cmd.SetAction(async _ =>
        {
            var result = await IpcClient.GetSpectrumProfileAsync().ConfigureAwait(false);
            CliOutput.Success("spectrum.profile.get", result);
        });

        return cmd;
    }

    private static Command BuildSetSpectrumProfileCommand()
    {
        var valueArgument = new Argument<int>("profile")
        {
            Description = Strings.Get("CLI_Argument_SpectrumProfile_Description", "Profile to set"),
            Arity = ArgumentArity.ExactlyOne
        };

        var cmd = new Command("set", Strings.Get("CLI_Command_SetSpectrumProfile_Description", "Set current Spectrum profile"));
        cmd.Aliases.Add("s");
        cmd.Add(valueArgument);
        cmd.SetAction(async parseResult =>
        {
            var value = parseResult.GetRequiredValue(valueArgument);
            await IpcClient.SetSpectrumProfileAsync($"{value}").ConfigureAwait(false);
            CliOutput.Success("spectrum.profile.set", $"{value}");
        });

        return cmd;
    }

    private static Command BuildSpectrumBrightnessCommand()
    {
        var getCmd = BuildGetSpectrumBrightnessCommand();
        var setCmd = BuildSetSpectrumBrightnessCommand();

        var cmd = new Command("brightness", Strings.Get("CLI_Command_SpectrumBrightness_Description", "Control Spectrum brightness"));
        cmd.Aliases.Add("b");
        cmd.Add(getCmd);
        cmd.Add(setCmd);

        return cmd;
    }

    private static Command BuildGetSpectrumBrightnessCommand()
    {
        var cmd = new Command("get", Strings.Get("CLI_Command_GetSpectrumBrightness_Description", "Get current Spectrum brightness"));
        cmd.Aliases.Add("g");
        cmd.SetAction(async _ =>
        {
            var result = await IpcClient.GetSpectrumBrightnessAsync().ConfigureAwait(false);
            CliOutput.Success("spectrum.brightness.get", result);
        });

        return cmd;
    }

    private static Command BuildSetSpectrumBrightnessCommand()
    {
        var valueArgument = new Argument<int>("brightness")
        {
            Description = Strings.Get("CLI_Argument_SpectrumBrightness_Description", "Brightness to set"),
            Arity = ArgumentArity.ExactlyOne
        };

        var cmd = new Command("set", Strings.Get("CLI_Command_SetSpectrumBrightness_Description", "Set current Spectrum brightness"));
        cmd.Aliases.Add("s");
        cmd.Add(valueArgument);
        cmd.SetAction(async parseResult =>
        {
            var value = parseResult.GetRequiredValue(valueArgument);
            await IpcClient.SetSpectrumBrightnessAsync($"{value}").ConfigureAwait(false);
            CliOutput.Success("spectrum.brightness.set", $"{value}");
        });

        return cmd;
    }

    private static Command BuildRGBCommand()
    {
        var getCmd = BuildGetRGBCommand();
        var setCmd = BuildSetRGBCommand();

        var cmd = new Command("rgb", Strings.Get("CLI_Command_RGB_Description", "Control RGB backlight preset"));
        cmd.Aliases.Add("r");
        cmd.Add(getCmd);
        cmd.Add(setCmd);

        return cmd;
    }

    private static Command BuildGetRGBCommand()
    {
        var cmd = new Command("get", Strings.Get("CLI_Command_GetRGB_Description", "Get current RGB preset"));
        cmd.Aliases.Add("g");
        cmd.SetAction(async _ =>
        {
            var result = await IpcClient.GetRGBPresetAsync().ConfigureAwait(false);
            CliOutput.Success("rgb.get", result);
        });

        return cmd;
    }

    private static Command BuildSetRGBCommand()
    {
        var valueArgument = new Argument<int>("preset")
        {
            Description = Strings.Get("CLI_Argument_RGBPreset_Description", "Preset to set"),
            Arity = ArgumentArity.ExactlyOne
        };

        var cmd = new Command("set", Strings.Get("CLI_Command_SetRGB_Description", "Set current RGB preset"));
        cmd.Aliases.Add("s");
        cmd.Add(valueArgument);
        cmd.SetAction(async parseResult =>
        {
            var value = parseResult.GetRequiredValue(valueArgument);
            await IpcClient.SetRGBPresetAsync($"{value}").ConfigureAwait(false);
            CliOutput.Success("rgb.set", $"{value}");
        });

        return cmd;
    }

    private static Command BuildNetworkAccelerationCommand()
    {
        var statusOption = CreateFlagOption("--status", "-s",
            Strings.Get("CLI_Option_NetworkStatus_Description", "Show network acceleration status"));
        var startOption = CreateFlagOption("--start", "-a",
            Strings.Get("CLI_Option_NetworkStart_Description", "Start network acceleration explicitly"));
        var stopOption = CreateFlagOption("--stop", "-x",
            Strings.Get("CLI_Option_NetworkStop_Description", "Stop network acceleration and restore state"));
        var diagnosticsOption = CreateFlagOption("--diagnostics", "-d",
            Strings.Get("CLI_Option_NetworkDiagnostics_Description", "Run network diagnostics without changing system state"));

        var cmd = new Command("network", Strings.Get("CLI_Command_Network_Description", "Manage network acceleration"));
        cmd.Aliases.Add("net");
        cmd.Aliases.Add("networkAcceleration");
        cmd.Add(statusOption);
        cmd.Add(startOption);
        cmd.Add(stopOption);
        cmd.Add(diagnosticsOption);
        cmd.SetAction(async parseResult =>
        {
            if (parseResult.GetValue(statusOption))
            {
                CliOutput.Success("network.status", await IpcClient.GetNetworkAccelerationStatusAsync().ConfigureAwait(false));
                return;
            }

            if (parseResult.GetValue(startOption))
            {
                CliOutput.Success("network.start", await IpcClient.StartNetworkAccelerationAsync().ConfigureAwait(false));
                return;
            }

            if (parseResult.GetValue(stopOption))
            {
                CliOutput.Success("network.stop", await IpcClient.StopNetworkAccelerationAsync().ConfigureAwait(false));
                return;
            }

            if (parseResult.GetValue(diagnosticsOption))
                CliOutput.Success("network.diagnostics", await IpcClient.RunNetworkDiagnosticsAsync().ConfigureAwait(false));
        });
        cmd.Validators.Add(result =>
        {
            var optionCount = 0;
            if (HasOption(result, statusOption)) optionCount++;
            if (HasOption(result, startOption)) optionCount++;
            if (HasOption(result, stopOption)) optionCount++;
            if (HasOption(result, diagnosticsOption)) optionCount++;

            if (optionCount > 1)
            {
                result.AddError(Strings.Get("CLI_Error_NetworkOnlyOneAction", "Please specify only one network action at a time"));
                return;
            }

            if (optionCount == 0)
                result.AddError(Strings.Get("CLI_Error_NetworkAtLeastOneAction", "At least one network action option should be specified"));
        });

        return cmd;
    }

    private static Command BuildShellCommand()
    {
        var statusOption = CreateFlagOption("--status", "-s",
            Strings.Get("CLI_Option_ShellStatus_Description", "Check current registration status"));
        var installOption = CreateFlagOption("--install", "-i",
            Strings.Get("CLI_Option_ShellInstall_Description", "Install Nilesoft Shell"));
        var uninstallOption = CreateFlagOption("--uninstall", "-x",
            Strings.Get("CLI_Option_ShellUninstall_Description", "Uninstall Nilesoft Shell"));
        var installStatusOption = CreateFlagOption("--install-status", "-is",
            Strings.Get("CLI_Option_ShellInstallStatus_Description", "Check current installation status"));

        var cmd = new Command("shell", Strings.Get("CLI_Command_Shell_Description", "Manage shell context menu extension (Nilesoft Shell)"));
        cmd.Aliases.Add("sh");
        cmd.Add(statusOption);
        cmd.Add(installOption);
        cmd.Add(uninstallOption);
        cmd.Add(installStatusOption);
        cmd.SetAction(async parseResult =>
        {
            var status = parseResult.GetValue(statusOption);
            var install = parseResult.GetValue(installOption);
            var uninstall = parseResult.GetValue(uninstallOption);
            var installStatus = parseResult.GetValue(installStatusOption);

            if (status)
            {
                var isRegistered = await IpcClient.IsShellRegisteredAsync().ConfigureAwait(false);
                if (CliOutput.Json)
                {
                    CliOutput.Success("shell.status", isRegistered ? "true" : "false");
                }
                else
                {
                    Console.WriteLine(isRegistered
                        ? Strings.Get("CLI_Shell_RegisteredYes", "Shell is registered")
                        : Strings.Get("CLI_Shell_RegisteredNo", "Shell is not registered"));
                }
                return;
            }

            if (installStatus)
            {
                var isInstalled = await IpcClient.IsShellInstalledAsync().ConfigureAwait(false);
                if (CliOutput.Json)
                {
                    CliOutput.Success("shell.installStatus", isInstalled ? "true" : "false");
                }
                else
                {
                    Console.WriteLine(isInstalled
                        ? Strings.Get("CLI_Shell_InstalledYes", "Shell is installed")
                        : Strings.Get("CLI_Shell_InstalledNo", "Shell is not installed"));
                }
                return;
            }

            if (install)
            {
                await IpcClient.InstallShellAsync().ConfigureAwait(false);
                if (CliOutput.Json)
                    CliOutput.Success("shell.install", "initiated");
                else
                    Console.WriteLine(Strings.Get("CLI_Shell_InstallInitiated", "Shell installation initiated"));
                return;
            }

            if (uninstall)
            {
                await IpcClient.UninstallShellAsync().ConfigureAwait(false);
                if (CliOutput.Json)
                    CliOutput.Success("shell.uninstall", "initiated");
                else
                    Console.WriteLine(Strings.Get("CLI_Shell_UninstallInitiated", "Shell uninstallation initiated"));
                return;
            }

            if (CliOutput.Json)
            {
                CliOutput.Error("invalid", Strings.Get("CLI_Error_ShellAtLeastOneAction", "At least one action option should be specified"), "shell");
                return;
            }

            Console.WriteLine(Strings.Get("CLI_Shell_NoAction_Hint", "Please specify an action:"));
            Console.WriteLine(Strings.Get("CLI_Shell_NoAction_Status", "  --status (-s): Check current registration status"));
            Console.WriteLine(Strings.Get("CLI_Shell_NoAction_Install", "  --install (-i): Install Nilesoft Shell"));
            Console.WriteLine(Strings.Get("CLI_Shell_NoAction_Uninstall", "  --uninstall (-x): Uninstall Nilesoft Shell"));
            Console.WriteLine(Strings.Get("CLI_Shell_NoAction_InstallStatus", "  --install-status (-is): Check current installation status"));
        });
        cmd.Validators.Add(result =>
        {
            var optionCount = 0;
            if (HasOption(result, statusOption)) optionCount++;
            if (HasOption(result, installStatusOption)) optionCount++;
            if (HasOption(result, installOption)) optionCount++;
            if (HasOption(result, uninstallOption)) optionCount++;

            if (optionCount > 1)
            {
                result.AddError(Strings.Get("CLI_Error_ShellOnlyOneAction", "Please specify only one action at a time"));
                return;
            }

            if (optionCount == 0)
                result.AddError(Strings.Get("CLI_Error_ShellAtLeastOneAction", "At least one action option should be specified"));
        });

        return cmd;
    }

    private static Option<bool> CreateFlagOption(string name, string alias, string description) =>
        new(name, alias)
        {
            Description = description,
            Arity = ArgumentArity.ZeroOrOne
        };

    private static bool HasArgument<T>(SymbolResult result, Argument<T> argument) =>
        result.GetResult(argument) is not null;

    private static bool HasCommand(SymbolResult result, Command command) =>
        result.GetResult(command) is not null;

    private static bool HasOption(SymbolResult result, Option option) =>
        result.GetResult(option) is { Implicit: false };

    private static int OnException(Exception ex)
    {
        var message = ex switch
        {
            IpcConnectException => Strings.Get(
                "CLI_IpcError_ConnectFailed",
                "Failed to connect. " +
                "Make sure that Universal Device Toolkit is running " +
                "in background and CLI is enabled in Settings."),
            IpcException => ex.Message,
            _ => ex.ToString()
        };

        var exitCode = ex switch
        {
            IpcConnectException => -1,
            IpcException => -2,
            _ => -99
        };

        var code = ex switch
        {
            IpcConnectException => "connect",
            IpcException => "ipc",
            _ => "error"
        };

        if (CliOutput.Json)
        {
            CliOutput.Error(code, message);
            return exitCode;
        }

        if (!Console.IsOutputRedirected)
        {
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Red;
        }

        Console.Error.WriteLine(message);

        if (!Console.IsOutputRedirected)
            Console.ResetColor();

        return exitCode;
    }
}
