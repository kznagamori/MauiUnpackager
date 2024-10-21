using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace MauiUnpackager
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length != 1)
			{
				Console.WriteLine("Usage: MauiUnpackager <path_to_csproj>");
				return;
			}

			string csprojPath = args[0];

			if (!File.Exists(csprojPath))
			{
				Console.WriteLine($"Error: The file '{csprojPath}' does not exist.");
				return;
			}

			try
			{
				// Load the csproj file
				XDocument doc = XDocument.Load(csprojPath);

				XElement? project = doc.Element("Project");
				if (project == null)
				{
					Console.WriteLine("Error: Invalid csproj file format.");
					return;
				}

				XElement? propertyGroup = project.Elements("PropertyGroup").FirstOrDefault();
				if (propertyGroup == null)
				{
					propertyGroup = new XElement("PropertyGroup");
					project.Add(propertyGroup);
				}

				// Add or update <WindowsPackageType>None</WindowsPackageType>
				UpdateOrCreateElement(propertyGroup, "WindowsPackageType", "None");

				// Save the updated csproj file
				doc.Save(csprojPath);
				Console.WriteLine("csproj file updated successfully.");

				// Update launchSettings.json
				string propertiesDir = Path.Combine(Path.GetDirectoryName(csprojPath) ?? "", "Properties");
				string launchSettingsPath = Path.Combine(propertiesDir, "launchSettings.json");

				if (File.Exists(launchSettingsPath))
				{
					UpdateLaunchSettings(launchSettingsPath);
				}
				else
				{
					Console.WriteLine("Warning: launchSettings.json not found.");
				}

				// Get the TargetFramework from csproj
				string targetFramework = GetTargetFramework(doc);
				if (string.IsNullOrEmpty(targetFramework))
				{
					Console.WriteLine("Error: Could not find TargetFramework in csproj file.");
					return;
				}

				// Create unpackaged_publish.bat
				CreateBatchFile(csprojPath, targetFramework);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error: {ex.Message}");
			}
		}

		static void UpdateOrCreateElement(XElement parent, string elementName, string value)
		{
			XElement? element = parent.Elements(elementName).FirstOrDefault();
			if (element == null)
			{
				element = new XElement(elementName, value);
				parent.Add(element);
			}
			else
			{
				element.Value = value;
			}
		}

		static void UpdateLaunchSettings(string launchSettingsPath)
		{
			try
			{
				string json = File.ReadAllText(launchSettingsPath);
				JObject? launchSettings = JObject.Parse(json);

				if (launchSettings != null && launchSettings["profiles"] is JObject profiles)
				{
					foreach (var profile in profiles.Properties())
					{
						if (profile.Value["commandName"] != null && profile.Value["commandName"]?.ToString() == "MsixPackage")
						{
							profile.Value["commandName"] = "Project";
						}
					}

					string updatedJson = launchSettings.ToString(Newtonsoft.Json.Formatting.Indented);
					File.WriteAllText(launchSettingsPath, updatedJson);
					Console.WriteLine("launchSettings.json updated successfully.");
				}
				else
				{
					Console.WriteLine("Error: Invalid launchSettings.json format.");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error updating launchSettings.json: {ex.Message}");
			}
		}

		static string GetTargetFramework(XDocument doc)
		{
			XElement? targetFrameworkElement = doc.Descendants("TargetFrameworks")
				.FirstOrDefault(e => (string?)e.Attribute("Condition") == "$([MSBuild]::IsOSPlatform('windows'))");

			return targetFrameworkElement?.Value.Split(';')
				.FirstOrDefault(tf => tf.StartsWith("net") && tf.Contains("-windows")) ?? string.Empty;
		}

		static void CreateBatchFile(string csprojPath, string targetFramework)
		{
			string directory = Path.GetDirectoryName(csprojPath) ?? throw new InvalidOperationException("Failed to get directory name.");
			string batchFilePath = Path.Combine(directory, "unpackaged_app_publish.bat");

			string[] lines =
			{
				"@echo off",
				$"dotnet publish -f {targetFramework} -c Release -p:RuntimeIdentifierOverride=win10-x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true"
			};

			File.WriteAllLines(batchFilePath, lines);
			Console.WriteLine("unpackaged_app_publish.bat file created successfully.");
		}
	}
}
