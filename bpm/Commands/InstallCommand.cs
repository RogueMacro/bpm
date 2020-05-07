﻿using bpm.Logging;
using bpm.Networking;
using bpm.Packages;
using bpm.Utilities;
using ServiceStack;
using System.IO;
using System.Linq;

namespace bpm.Commands
{
    public class InstallCommand : ICommand
    {
        public void Execute(string[] args)
        {
            if (args.Length == 0)
            {
                Log.Usage("install <package> [-global]");
                return;
            }

            string packageName = CommandTool.GetArgumentValueOrIndex("package", 0, ref args);

            bool isGlobal = CommandTool.GetArgument("-global", ref args);
            bool isForceInstall = CommandTool.GetArgument("-force", ref args);

            if (args.Length != 0)
            {
                Log.Fatal("Invalid arguments: " + args.Join(", "), ExitCode.INVALID_ARGUMENTS);
                return;
            }

            var packages = PackageListFetcher.GetPackages();
            var package = packages.GetPackage(packageName);
            if (package == null)
            {
                Log.Fatal($"Could not find package: '{packageName}'", ExitCode.PACKAGE_NOT_FOUND);
                return;
            }


            var packagePath = BpmPath.GetPackagePath(package.Name, isGlobal);
            if (!isForceInstall && packagePath != "")
            {
                Package oldPackageFile = Package.FromFile(Path.Combine(packagePath, "Package.json"));

                var githubFile = BpmPath.GetPackageFileUrl(oldPackageFile);
                Package newPackageFile = Package.FromGithubFile(githubFile);
                Log.Trace($"(Old) {oldPackageFile.Name} v{oldPackageFile.Version} <-> (New) {newPackageFile.Name} v{newPackageFile.Version}");
                if (newPackageFile.Version.IsSameAs(oldPackageFile.Version))
                {
                    Log.Info($"{oldPackageFile.Name} v{oldPackageFile.Version} is already installed.");
                    return;
                }
                else
                {
                    string backOrForth = newPackageFile.Version.IsGreaterThan(oldPackageFile.Version) ? "upgrade" : "go back";
                    if (Input.GetChoice($"{oldPackageFile.Name} is already installed (v{oldPackageFile.Version}). Do you want to {backOrForth} to v{newPackageFile.Version}?"))
                        isForceInstall = true;
                    else
                        return;
                }
            }

            Log.Info($"Installing: {package.Name}");

            var bpmPath = isGlobal ? BpmPath.GlobalFolder : BpmPath.UserFolder;
            var cloner = new GithubRepoCloner(package.RepoUrl, Path.Combine(bpmPath, "packages", package.Name));
            bool success = cloner.Clone(isForceInstall);

            if (success)
                Log.Success("Installed successfully");
        }
    }
}