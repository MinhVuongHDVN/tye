﻿using System;
using System.CommandLine;
using System.CommandLine.IO;
using System.IO;
using Tye.ConfigModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tye
{
    public class InitHost
    {

        public static string CreateTyeFile(FileInfo? path, bool force)
        {
            var (content, outputFilePath) = CreateTyeFileContent(path, force);

            File.WriteAllText(outputFilePath, content);

            return outputFilePath;
        }

        public static (string, string) CreateTyeFileContent(FileInfo? path, bool force)
        {
            if (path is FileInfo && path.Exists && !force)
            {
                ThrowIfTyeFilePresent(path, "tye.yml");
                ThrowIfTyeFilePresent(path, "tye.yaml");
            }

            var template = @"
# tye application configuration file
# read all about it at https://github.com/dotnet/tye
#
# define global settings here
# name: exampleapp # application name
# registry: exampleuser # dockerhub username or container registry hostname

# define multiple services here
services:
- name: myservice
  # project: app.csproj # msbuild project path (relative to this file)
  # executable: app.exe # path to an executable (relative to this file)
  # args: --arg1=3 # arguments to pass to the process
  # replicas: 5 # number of times to launch the application
  # env: # array of environment variables
  #  - name: key
  #    value: value
  # bindings: # optional array of bindings (ports, connection strings)
    # - port: 8080 # number port of the binding
".TrimStart();

            // Output in the current directory unless an input file was provided, then
            // output next to the input file.
            var outputFilePath = "tye.yaml";

            if (path is FileInfo && path.Exists)
            {
                var application = ConfigFactory.FromFile(path);
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                    .Build();

                var extension = path.Extension.ToLowerInvariant();
                var directory = path.Directory;

                // Clear all bindings if any for solutions and project files
                if (extension == ".sln" || extension == ".csproj" || extension == ".fsproj")
                {
                    // If the input file is a project or solution then use that as the name
                    application.Name = Path.GetFileNameWithoutExtension(path.Name).ToLowerInvariant();

                    foreach (var service in application.Services)
                    {
                        service.Bindings = null!;
                        service.Configuration = null!;
                        service.Project = service.Project!.Substring(directory.FullName.Length).TrimStart(Path.DirectorySeparatorChar);
                    }

                    // If the input file is a sln/project then place the config next to it
                    outputFilePath = Path.Combine(directory.FullName, "tye.yaml");
                }
                else
                {
                    // If the input file is a yaml, then use the directory name.
                    application.Name = path.Directory.Name.ToLowerInvariant();

                    // If the input file is a yaml, then replace it.
                    outputFilePath = path.FullName;
                }

                template = @"
# tye application configuration file
# read all about it at https://github.com/dotnet/tye
".TrimStart() + serializer.Serialize(application);
            }

            return (template, outputFilePath);
        }


        private static void ThrowIfTyeFilePresent(FileInfo? path, string yml)
        {
            var tyeYaml = Path.Combine(path!.DirectoryName, yml);
            if (File.Exists(tyeYaml))
            {
                throw new CommandException($"File '{tyeYaml}' already exists. Use --force to override the {yml} file if desired.");
            }
        }
    }
}
