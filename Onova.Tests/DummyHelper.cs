﻿using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Mono.Cecil;
using NUnit.Framework;

namespace Onova.Tests
{
    public static class DummyHelper
    {
        private const string OnovaFileName = "Onova.dll";
        private const string DummyFileName = "Onova.Tests.Dummy.exe";

        private static string TestDirPath => TestContext.CurrentContext.TestDirectory;
        private static string DummyDirPath => Path.Combine(TestDirPath, "Dummies");
        private static string DummyFilePath => Path.Combine(DummyDirPath, DummyFileName);
        private static string PackagesDirPath => Path.Combine(DummyDirPath, "Packages");

        private static readonly Cli DummyCli = new Cli(DummyFilePath);

        public static void DeleteDummy()
        {
            // There might be a small race condition and the files are still in use

            const int maxRetries = 5;
            var retry = 0;

            while (retry++ < maxRetries)
            {
                try
                {
                    Directory.Delete(DummyDirPath, true);
                    break;
                }
                catch (Exception ex)
                {
                    if (ex is DirectoryNotFoundException)
                        break;
                }

                Thread.Sleep(500);
            }
        }

        private static void CreateDummy(Version version)
        {
            // Create dummies directory
            Directory.CreateDirectory(DummyDirPath);

            // Copy files
            File.Copy(Path.Combine(TestDirPath, DummyFileName), DummyFilePath);
            File.Copy(Path.Combine(TestDirPath, OnovaFileName), Path.Combine(DummyDirPath, OnovaFileName));

            // Change version
            var definition = AssemblyDefinition.ReadAssembly(DummyFilePath);
            definition.Name.Version = version;
            definition.Write(DummyFilePath);
        }

        private static void CreateDummyPackage(Version version)
        {
            // Create packages directory
            Directory.CreateDirectory(PackagesDirPath);

            // Temporarily copy the dummy
            var dummyTempFilePath = Path.Combine(DummyDirPath, $"{DummyFileName}.{version}.exe");
            File.Copy(DummyFilePath, dummyTempFilePath);

            // Change version
            var definition = AssemblyDefinition.ReadAssembly(dummyTempFilePath);
            definition.Name.Version = version;
            definition.Write(dummyTempFilePath);

            // Create package
            using (var inputStream = File.OpenRead(dummyTempFilePath))
            using (var outputStream = File.Create(Path.Combine(PackagesDirPath, $"{version}.onv")))
            using (var zip = new ZipArchive(outputStream, ZipArchiveMode.Create))
            using (var entryStream = zip.CreateEntry(DummyFileName).Open())
                inputStream.CopyTo(entryStream);

            // Delete temp file
            File.Delete(dummyTempFilePath);
        }

        public static void SetupDummy()
        {
            // Delete old dummies if they exist
            DeleteDummy();

            // Create base dummy
            CreateDummy(Version.Parse("1.0.0.0"));

            // Create packages
            CreateDummyPackage(Version.Parse("1.0.0.0"));
            CreateDummyPackage(Version.Parse("2.0.0.0"));
        }

        public static async Task<Version> GetDummyVersionAsync()
        {
            var output = await DummyCli.ExecuteAsync("version");
            output.ThrowIfError();

            return Version.Parse(output.StandardOutput);
        }

        public static async Task UpdateDummyAsync()
        {
            var output = await DummyCli.ExecuteAsync("update");
            output.ThrowIfError();
        }
    }
}