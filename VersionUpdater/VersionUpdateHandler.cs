using System;
using System.Linq;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Projects;
using System.IO;
using System.Text.RegularExpressions;

namespace VersionUpdater
{
	public class VersionUpdateHandler
		: CommandHandler
	{
		protected override void Run ()
		{
			var solutions = IdeApp
				.Workspace
				.Items
				.Where (i => i.GetType ().Name == "Solution")
				.ToList ();

			var monitor = new SimpleProgressMonitor ();

			foreach (var s in solutions) {
				var solution = s as Solution;

				var version = solution.Version;
				var versionSplit = version.Split (new char [] { '.' });
				var build = Int32.Parse (versionSplit.Last ());
				var nextBuild = build;
				versionSplit [versionSplit.Length - 1] = nextBuild.ToString ();
				var nextVersion = String.Join (".", versionSplit);

				solution.Version = nextVersion;
				solution.Save (monitor);

				var projects = solution
					.Items
					.Where (i => i.GetType ().Name.EndsWith ("Project"))
					.ToList ();

				foreach (var p in projects) {
					var project = p as Project;

					var projectTypes = project
						.GetProjectTypes ()
						.ToList ();

					var files = project.Files;

					if (projectTypes.Any (pt => pt == "PortableDotNet")) {
						project.Version = nextVersion;
						project.Save (monitor);
						continue;
					}

					if (projectTypes.Any (pt => pt == "XamarinIOS")) {
						var infoPlist = files
							.Where (f => f.Name.EndsWith ("Info.plist"))
							.Single ();

						var plistLines = File.ReadAllLines (infoPlist.FilePath).ToList ();

						var versionLine = plistLines
							.Where (l => l.Contains ("CFBundleShortVersionString"))
							.Single ();
						var versionIndex = plistLines.IndexOf (versionLine) + 1;
						var versionElement = plistLines [versionIndex];
						versionElement = "\t<string>" + nextVersion + "</string>";
						plistLines [versionIndex] = versionElement;

						var buildLine = plistLines
							.Where (l => l.Contains ("CFBundleVersion"))
							.Single ();
						var buildIndex = plistLines.IndexOf (buildLine) + 1;
						var buildElement = plistLines [buildIndex];
						buildElement = "\t<string>" + nextBuild + "</string>";
						plistLines [buildIndex] = buildElement;

						File.WriteAllLines (infoPlist.FilePath, plistLines.ToArray ());

						project.Version = nextVersion;
						project.Save (monitor);
					}

					if (projectTypes.Any (pt => pt == "MonoDroid")) {
						var manifest = files
							.Where (f => f.Name.EndsWith ("AndroidManifest.xml"))
							.Single ();

						var manifestLines = File.ReadAllLines (manifest.FilePath).ToList ();

						var versionString = "android:versionName=";
						var versionMatch = versionString + "\\" + "\"" + "(.*?)" + "\\" + "\"";
						var nextVersionString = versionString + "\"" + nextVersion + "\"";

						var versionLine = manifestLines
							.Where (l => l.Contains (versionString))
							.Single ();
						var versionIndex = manifestLines.IndexOf (versionLine);
						var versionElement = manifestLines [versionIndex];
						versionElement = Regex.Replace (versionElement, versionMatch, nextVersionString);
						manifestLines [versionIndex] = versionElement;

						var buildString = "android:versionCode=";
						var buildMatch = buildString + "\\" + "\"" + "(.*?)" + "\\" + "\"";
						var nextBuildString = buildString + "\"" + nextBuild + "\"";

						var buildLine = manifestLines
							.Where (l => l.Contains (buildString))
							.Single ();
						var buildIndex = manifestLines.IndexOf (buildLine);
						var buildElement = manifestLines [buildIndex];
						buildElement = Regex.Replace (buildString, buildMatch, nextBuildString);
						manifestLines [buildIndex] = buildElement;

						File.WriteAllLines (manifest.FilePath, manifestLines.ToArray ());

						project.Version = nextVersion;
						project.Save (monitor);
					}
				}
			}
		}
	}
}

