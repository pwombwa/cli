// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Cli.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Cli.Build
{
    /// <summary>
    /// A utility class used only for bootstrapping a change in NuGet where paths to the global
    /// packages path are now always lowercased. This code should be removed once the lowercase
    /// paths change has made it into stage 0.
    /// 
    /// See the following GitHub issue for more details.
    /// https://github.com/dotnet/cli/issues/2874
    /// </summary>
    public static class CaseInsensitivePaths
    {

        public static void Restore(BuildTargetContext c, DotNetCli dotnetCli)
        {
            dotnetCli.Restore("--verbosity", "verbose", "--disable-parallel", "--fallbacksource", Dirs.CorehostLocalPackages)
                .WorkingDirectory(Path.Combine(Dirs.RepoRoot, "src"))
                .Execute()
                .EnsureSuccessful();
            dotnetCli.Restore("--verbosity", "verbose", "--disable-parallel", "--infer-runtimes")
                .WorkingDirectory(Path.Combine(Dirs.RepoRoot, "tools"))
                .Execute()
                .EnsureSuccessful();
        }

        public static PathAndContent[] PrepareForRuntimeGraphGenerator(BuildTargetContext c, DotNetCli dotnetCli, string runtimeGraphGeneratorName, string nuGetVersion)
        {
            PathAndContent[] originals = null;
            if (nuGetVersion != null)
            {
                originals = new[]
                    {
                            Path.Combine("src", "dotnet"),
                            Path.Combine("src", "Microsoft.DotNet.Cli.Utils"),
                            Path.Combine("src", "Microsoft.DotNet.ProjectModel"),
                            Path.Combine("tools", runtimeGraphGeneratorName)
                        }
                    .Select(p => Path.Combine(Dirs.RepoRoot, p, "project.json"))
                    .Select(p => new PathAndContent
                    {
                        Path = p,
                        Content = UpdateNuGetVersion(c, p, nuGetVersion)
                    })
                    .ToArray();

                Restore(c, dotnetCli);
            }
            else
            {
                originals = new PathAndContent[0];
            }

            return originals;
        }

        public static void CleanUpAfterRuntimeGraphGenerator(BuildTargetContext c, DotNetCli dotnetCli, string nuGetVersion, PathAndContent[] originals)
        {
            if (nuGetVersion != null)
            {
                foreach (var original in originals)
                {
                    RestoreNuGetVersion(c, original.Path, original.Content);
                }

                Restore(c, dotnetCli);
            }
        }

        private static byte[] UpdateNuGetVersion(BuildTargetContext c, string projectJsonPath, string version)
        {
            c.Warn($"Setting the NuGet version in {projectJsonPath} to {version}.");

            var encoding = new UTF8Encoding(false);

            var originalBytes = File.ReadAllBytes(projectJsonPath);
            var originalJson = encoding.GetString(originalBytes);
            var projectJson = JsonConvert.DeserializeObject<JObject>(originalJson);
            var dependencies = (JObject)projectJson["dependencies"];
            foreach (var property in dependencies.Properties())
            {
                if (property.Name.StartsWith("NuGet."))
                {
                    if (property.Value.Type == JTokenType.String)
                    {
                        property.Value = version;
                    }
                    else
                    {
                        property.Value["version"] = version;
                    }
                }
            }

            var newJson = JsonConvert.SerializeObject(projectJson, Formatting.Indented);
            var newBytes = encoding.GetBytes(newJson);

            File.WriteAllBytes(projectJsonPath, newBytes);

            return originalBytes;
        }

        private static void RestoreNuGetVersion(BuildTargetContext c, string projectJsonPath, byte[] originalBytes)
        {
            c.Warn($"Restoring the original NuGet version to {projectJsonPath}.");

            File.WriteAllBytes(projectJsonPath, originalBytes);
        }

        public class PathAndContent
        {
            public string Path { get; set; }
            public byte[] Content { get; set; }
        }
    }
}
