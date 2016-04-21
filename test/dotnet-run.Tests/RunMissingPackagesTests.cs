// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class RunMissingPackagesTests : TestBase
    {
        [Fact]
        public void MissingPackageDuringRunCauseFailure()
        {
            // Arrange
            TestInstance instance = TestAssetsManager.CreateTestInstance("PortableTests");
            var testProject = Path.Combine(instance.TestRoot, "StandaloneApp", "project.json");
            var workingDirectory = Path.GetDirectoryName(testProject);
            var testNuGetCache = Path.Combine(instance.Path, "packages");
            var oldLocation = Path.Combine(testNuGetCache, "system.console");
            var newLocation = Path.Combine(testNuGetCache, "system.console.different");

            var restoreCommand = new RestoreCommand();

            restoreCommand.WorkingDirectory = workingDirectory;
            restoreCommand.Environment["NUGET_PACKAGES"] = testNuGetCache;
            restoreCommand.Execute();

            var buildCommand = new BuildCommand(testProject);

            buildCommand.WorkingDirectory = workingDirectory;
            buildCommand.Environment["NUGET_PACKAGES"] = testNuGetCache;
            buildCommand.Execute();

            // Delete all System.Console packages.
            foreach (var directory in Directory.EnumerateDirectories(testNuGetCache, "*system.console*"))
            {
                Directory.Delete(directory, true);
            }

            var runCommand = new RunCommand(testProject);

            runCommand.WorkingDirectory = workingDirectory;
            runCommand.Environment["NUGET_PACKAGES"] = testNuGetCache;

            // Act & Assert
            runCommand
                .ExecuteWithCapturedOutput()
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("Unhandled Exception: System.IO.FileNotFoundException: Could not load file or assembly 'System.Console");
        }
    }
}
