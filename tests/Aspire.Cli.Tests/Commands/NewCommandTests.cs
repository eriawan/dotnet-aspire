// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Cli.Commands;
using Aspire.Cli.Interaction;
using Aspire.Cli.Tests.TestServices;
using Aspire.Cli.Tests.Utils;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspire.Cli.Tests.Commands;

public class NewCommandTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task NewCommandWithHelpArgumentReturnsZero()
    {
        var services = CliTestHelper.CreateServiceCollection(outputHelper);
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<RootCommand>();
        var result = command.Parse("new --help");

        var exitCode = await result.InvokeAsync().WaitAsync(CliTestConstants.DefaultTimeout);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task NewCommandInteractiveFlowSmokeTest()
    {
        var services = CliTestHelper.CreateServiceCollection(outputHelper, options => {

            // Set of options that we'll give when prompted.
            options.NewCommandPrompterFactory = (sp) =>
            {
                var interactionService = sp.GetRequiredService<IInteractionService>();
                return new TestNewCommandPrompter(interactionService);
            };

            options.DotNetCliRunnerFactory = (sp) =>
            {
                var runner = new TestDotNetCliRunner();
                runner.SearchPackagesAsyncCallback = (dir, query, prerelease, take, skip, nugetSource, cancellationToken) =>
                {
                    var package = new NuGetPackage()
                    {
                        Id = "Aspire.ProjectTemplates",
                        Source = "nuget",
                        Version = "9.2.0"
                    };

                    return (
                        0, // Exit code.
                        new NuGetPackage[] { package } // Single package.
                        );
                };

                return runner;
            };
        });
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<NewCommand>();
        var result = command.Parse("new");

        var exitCode = await result.InvokeAsync().WaitAsync(CliTestConstants.DefaultTimeout);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task NewCommandOrdersTemplatePackageVersionsCorrectly()
    {
        IEnumerable<NuGetPackage>? promptedPackages = null;

        var services = CliTestHelper.CreateServiceCollection(outputHelper, options => {

            // Set of options that we'll give when prompted.
            options.NewCommandPrompterFactory = (sp) =>
            {
                var interactionService = sp.GetRequiredService<IInteractionService>();
                var prompter =  new TestNewCommandPrompter(interactionService);

                prompter.PromptForTemplatesVersionCallback = (packages) =>
                {
                    promptedPackages = packages;
                    return promptedPackages.First();
                };

                return prompter;
            };

            options.DotNetCliRunnerFactory = (sp) =>
            {
                var runner = new TestDotNetCliRunner();
                runner.SearchPackagesAsyncCallback = (dir, query, prerelease, take, skip, nugetSource, cancellationToken) =>
                {
                    var package92 = new NuGetPackage()
                    {
                        Id = "Aspire.ProjectTemplates",
                        Source = "othernuget",
                        Version = "9.2.0"
                    };

                    var package93 = new NuGetPackage()
                    {
                        Id = "Aspire.ProjectTemplates",
                        Source = "nuget",
                        Version = "9.3.0"
                    };

                    return (
                        0, // Exit code.
                        new NuGetPackage[] { package92, package93 }
                        );
                };

                return runner;
            };
        });
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<NewCommand>();
        var result = command.Parse("new");

        var exitCode = await result.InvokeAsync().WaitAsync(CliTestConstants.DefaultTimeout);
        Assert.Equal(0, exitCode);
        Assert.NotNull(promptedPackages);
        Assert.Collection(
            promptedPackages,
            package => Assert.Equal("9.3.0", package.Version),
            package => Assert.Equal("9.2.0", package.Version)
        );
    }

    [Fact]
    public async Task NewCommandOrdersTemplatePackageVersionsCorrectlyWithPrerelease()
    {
        IEnumerable<NuGetPackage>? promptedPackages = null;

        var services = CliTestHelper.CreateServiceCollection(outputHelper, options => {

            // Set of options that we'll give when prompted.
            options.NewCommandPrompterFactory = (sp) =>
            {
                var interactionService = sp.GetRequiredService<IInteractionService>();
                var prompter =  new TestNewCommandPrompter(interactionService);

                prompter.PromptForTemplatesVersionCallback = (packages) =>
                {
                    promptedPackages = packages;
                    return promptedPackages.First();
                };

                return prompter;
            };

            options.DotNetCliRunnerFactory = (sp) =>
            {
                var runner = new TestDotNetCliRunner();
                runner.SearchPackagesAsyncCallback = (dir, query, prerelease, take, skip, nugetSource, cancellationToken) =>
                {
                    var package92 = new NuGetPackage()
                    {
                        Id = "Aspire.ProjectTemplates",
                        Source = "othernuget",
                        Version = "9.2.0"
                    };

                    var package94 = new NuGetPackage()
                    {
                        Id = "Aspire.ProjectTemplates",
                        Source = "internalfeed",
                        Version = "9.4.0-preview.1234"
                    };

                    var package93 = new NuGetPackage()
                    {
                        Id = "Aspire.ProjectTemplates",
                        Source = "nuget",
                        Version = "9.3.0"
                    };

                    return (
                        0, // Exit code.
                        new NuGetPackage[] { package92, package94, package93 }
                        );
                };

                return runner;
            };
        });
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<NewCommand>();
        var result = command.Parse("new");

        var exitCode = await result.InvokeAsync().WaitAsync(CliTestConstants.DefaultTimeout);
        Assert.Equal(0, exitCode);
        Assert.NotNull(promptedPackages);
        Assert.Collection(
            promptedPackages,
            package => Assert.Equal("9.4.0-preview.1234", package.Version),
            package => Assert.Equal("9.3.0", package.Version),
            package => Assert.Equal("9.2.0", package.Version)
        );
    }

    [Fact]
    public async Task NewCommandDoesNotPromptForProjectNameIfSpecifiedOnCommandLine()
    {
        var promptedForName = false;

        var services = CliTestHelper.CreateServiceCollection(outputHelper, options => {

            // Set of options that we'll give when prompted.
            options.NewCommandPrompterFactory = (sp) =>
            {
                var interactionService = sp.GetRequiredService<IInteractionService>();
                var prompter = new TestNewCommandPrompter(interactionService);

                prompter.PromptForProjectNameCallback = (defaultName) =>
                {
                    promptedForName = true;
                    throw new InvalidOperationException("This should not be called");
                };

                return prompter;
            };

            options.DotNetCliRunnerFactory = (sp) =>
            {
                var runner = new TestDotNetCliRunner();
                runner.SearchPackagesAsyncCallback = (dir, query, prerelease, take, skip, nugetSource, cancellationToken) =>
                {
                    var package = new NuGetPackage()
                    {
                        Id = "Aspire.ProjectTemplates",
                        Source = "nuget",
                        Version = "9.2.0"
                    };

                    return (
                        0, // Exit code.
                        new NuGetPackage[] { package } // Single package.
                        );
                };

                return runner;
            };
        });
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<NewCommand>();
        var result = command.Parse("new --name MyApp");

        var exitCode = await result.InvokeAsync().WaitAsync(CliTestConstants.DefaultTimeout);
        Assert.Equal(0, exitCode);
        Assert.False(promptedForName);
    }

    [Fact]
    public async Task NewCommandDoesNotPromptForOutputPathIfSpecifiedOnCommandLine()
    {
        bool promptedForPath = false;

        var services = CliTestHelper.CreateServiceCollection(outputHelper, options => {

            // Set of options that we'll give when prompted.
            options.NewCommandPrompterFactory = (sp) =>
            {
                var interactionService = sp.GetRequiredService<IInteractionService>();
                var prompter = new TestNewCommandPrompter(interactionService);

                prompter.PromptForOutputPathCallback = (path) =>
                {
                    promptedForPath = true;
                    throw new InvalidOperationException("This should not be called");
                };

                return prompter;
            };

            options.DotNetCliRunnerFactory = (sp) =>
            {
                var runner = new TestDotNetCliRunner();
                runner.SearchPackagesAsyncCallback = (dir, query, prerelease, take, skip, nugetSource, cancellationToken) =>
                {
                    var package = new NuGetPackage()
                    {
                        Id = "Aspire.ProjectTemplates",
                        Source = "nuget",
                        Version = "9.2.0"
                    };

                    return (
                        0, // Exit code.
                        new NuGetPackage[] { package } // Single package.
                        );
                };

                return runner;
            };
        });
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<NewCommand>();
        var result = command.Parse("new --output notsrc");

        var exitCode = await result.InvokeAsync().WaitAsync(CliTestConstants.DefaultTimeout);
        Assert.Equal(0, exitCode);
        Assert.False(promptedForPath);
    }

    [Fact]
    public async Task NewCommandDoesNotPromptForTemplateIfSpecifiedOnCommandLine()
    {
        bool promptedForTemplate = false;

        var services = CliTestHelper.CreateServiceCollection(outputHelper, options => {

            // Set of options that we'll give when prompted.
            options.NewCommandPrompterFactory = (sp) =>
            {
                var interactionService = sp.GetRequiredService<IInteractionService>();
                var prompter = new TestNewCommandPrompter(interactionService);

                prompter.PromptForTemplateCallback = (path) =>
                {
                    promptedForTemplate = true;
                    throw new InvalidOperationException("This should not be called");
                };

                return prompter;
            };

            options.DotNetCliRunnerFactory = (sp) =>
            {
                var runner = new TestDotNetCliRunner();
                runner.SearchPackagesAsyncCallback = (dir, query, prerelease, take, skip, nugetSource, cancellationToken) =>
                {
                    var package = new NuGetPackage()
                    {
                        Id = "Aspire.ProjectTemplates",
                        Source = "nuget",
                        Version = "9.2.0"
                    };

                    return (
                        0, // Exit code.
                        new NuGetPackage[] { package } // Single package.
                        );
                };

                return runner;
            };
        });
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<NewCommand>();
        var result = command.Parse("new aspire-starter");

        var exitCode = await result.InvokeAsync().WaitAsync(CliTestConstants.DefaultTimeout);
        Assert.Equal(0, exitCode);
        Assert.False(promptedForTemplate);
    }

    [Fact]
    public async Task NewCommandDoesNotPromptForTemplateVersionIfSpecifiedOnCommandLine()
    {
        bool promptedForTemplateVersion = false;

        var services = CliTestHelper.CreateServiceCollection(outputHelper, options => {

            // Set of options that we'll give when prompted.
            options.NewCommandPrompterFactory = (sp) =>
            {
                var interactionService = sp.GetRequiredService<IInteractionService>();
                var prompter = new TestNewCommandPrompter(interactionService);

                prompter.PromptForTemplatesVersionCallback = (packages) =>
                {
                    promptedForTemplateVersion = true;
                    throw new InvalidOperationException("This should not be called");
                };

                return prompter;
            };

            options.DotNetCliRunnerFactory = (sp) =>
            {
                var runner = new TestDotNetCliRunner();
                runner.SearchPackagesAsyncCallback = (dir, query, prerelease, take, skip, nugetSource, cancellationToken) =>
                {
                    var package = new NuGetPackage()
                    {
                        Id = "Aspire.ProjectTemplates",
                        Source = "nuget",
                        Version = "9.2.0"
                    };

                    return (
                        0, // Exit code.
                        new NuGetPackage[] { package } // Single package.
                        );
                };

                return runner;
            };
        });
        var provider = services.BuildServiceProvider();

        var command = provider.GetRequiredService<NewCommand>();
        var result = command.Parse("new --version 9.2.0");

        var exitCode = await result.InvokeAsync().WaitAsync(CliTestConstants.DefaultTimeout);
        Assert.Equal(0, exitCode);
        Assert.False(promptedForTemplateVersion);
    }
}

internal sealed class TestNewCommandPrompter(IInteractionService interactionService) : NewCommandPrompter(interactionService)
{
    public Func<IEnumerable<NuGetPackage>, NuGetPackage>? PromptForTemplatesVersionCallback { get; set; }
    public Func<(string TemplateName, string TemplateDescription, string? PathAppendage)[], (string TemplateName, string TemplateDescription, string? PathAppendage)>? PromptForTemplateCallback { get; set; }
    public Func<string, string>? PromptForProjectNameCallback { get; set; }
    public Func<string, string>? PromptForOutputPathCallback { get; set; }

    public override Task<(string TemplateName, string TemplateDescription, string? PathAppendage)> PromptForTemplateAsync((string TemplateName, string TemplateDescription, string? PathAppendage)[] validTemplates, CancellationToken cancellationToken)
    {
        return PromptForTemplateCallback switch
        {
            { } callback => Task.FromResult(callback(validTemplates)),
            _ => Task.FromResult(validTemplates[0]) // If no callback is provided just accept the first template.
        };
    }

    public override Task<string> PromptForProjectNameAsync(string defaultName, CancellationToken cancellationToken)
    {
        return PromptForProjectNameCallback switch
        {
            { } callback => Task.FromResult(callback(defaultName)),
            _ => Task.FromResult(defaultName) // If no callback is provided just accept the default.
        };
    }

    public override Task<string> PromptForOutputPath(string path, CancellationToken cancellationToken)
    {
        return PromptForOutputPathCallback switch
        {
            { } callback => Task.FromResult(callback(path)),
            _ => Task.FromResult(path) // If no callback is provided just accept the default.
        };
    }

    public override Task<NuGetPackage> PromptForTemplatesVersionAsync(IEnumerable<NuGetPackage> candidatePackages, CancellationToken cancellationToken)
    {
        return PromptForTemplatesVersionCallback switch
        {
            { } callback => Task.FromResult(callback(candidatePackages)),
            _ => Task.FromResult(candidatePackages.First()) // If no callback is provided just accept the first package.
        };
    }
}
