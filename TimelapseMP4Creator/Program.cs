using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Transforms;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TimelapseMP4Creator
{
	public class Program
	{
		const string FinishedPathsLogFile = "finishedPaths.log";

		public static async Task Main(string[] args)
		{
			var config = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json")
				.Build();

			var appSettings = config.GetSection("AppSettings").Get<AppSettings>();

			var directories = Directory.EnumerateDirectories(appSettings.SourceImageLocation);
			foreach (var directory in directories)
			{
				var date = Path.GetFileName(directory);
				var sourceDirectory = Path.Combine(appSettings.SourceImageLocation, date);
				var destinationDirectory = Path.Combine(appSettings.LocalImageLocation, date);

				await GetFilesAndSaveResized(sourceDirectory, destinationDirectory);
				await CreateTimelapseMP4(destinationDirectory, appSettings.MP4OutputDirectory, date);
			}
		}

		public static async Task GetFilesAndSaveResized(string sourceDirectory, string destinationDirectory)
		{
			if (await IsPathInFinishedFile(sourceDirectory))
			{
				return;
			}

			// Check if source directory exists
			if (!Directory.Exists(sourceDirectory))
			{
				throw new Exception($"SourceImageLocation {sourceDirectory} does not exist");
			}

			// Remove content if the directory exists
			if (Directory.Exists(destinationDirectory))
			{
				var directoryInfo = new DirectoryInfo(destinationDirectory);
				foreach (var file in directoryInfo.EnumerateFiles())
				{
					file.Delete();
				}
			}
			else
			{
				Directory.CreateDirectory(destinationDirectory);
			}
			
			var filesToCopy = Directory.GetFiles(sourceDirectory, "*.jpg")
				.Select(item => ImageFileDetails.CreateImageFileDetails(item))
				.ToList();
			filesToCopy.RemoveAll(item => item == null);

			// Sort the files so that renaming with index is possible
			filesToCopy = filesToCopy.OrderBy(item => item.DateTimeTaken).ToList();

			Console.WriteLine($"Total files in source directory {sourceDirectory}: {filesToCopy.Count}");

			var index = 0;
			var stopwatch = Stopwatch.StartNew();
			foreach (var fileToCopy in filesToCopy)
			{
				stopwatch.Restart();

				var localFileName = $"image_{index++.ToString("D4")}.jpg";
				var destinationPath = Path.Combine(destinationDirectory, localFileName);
				long downloadTimeInSeconds = 0;

				// Load the image and save a resized version
				using (var image = Image.Load(fileToCopy.Path))
				{
					downloadTimeInSeconds = stopwatch.ElapsedMilliseconds;
					image.Mutate(x => x.Resize(image.Width / 2, image.Height / 2));
					image.Save(destinationPath);
				}

				var info = $"Finished copying and resizing file: {fileToCopy.FileName}. File {index}/{filesToCopy.Count}. Statistics {downloadTimeInSeconds} - {stopwatch.ElapsedMilliseconds}";
				Console.WriteLine(info);
			}

			await AddPathToFinishedFile(sourceDirectory);
		}

		public static async Task CreateTimelapseMP4(string localImageDirectory, string mp4OutputDirectory, string filename)
		{
			if (!IsLinux())
			{
				return;
			}

			if (!Directory.Exists(mp4OutputDirectory))
			{
				Directory.CreateDirectory(mp4OutputDirectory);
			}

			var savePath = $"{mp4OutputDirectory}/{filename}.mp4";
			if (File.Exists(savePath))
			{
				return;
			}

			string command = $"ffmpeg -framerate 30 -i {localImageDirectory}/image_%04d.jpg -c:v libx264 -r 30 {savePath}";
			Console.WriteLine(command);

			string result = $"command\r\n";
			using (var proc = new Process())
			{
				proc.StartInfo.FileName = "/bin/bash";
				proc.StartInfo.Arguments = "-c \" " + command + " \"";
				proc.StartInfo.UseShellExecute = false;
				proc.StartInfo.RedirectStandardOutput = true;
				proc.StartInfo.RedirectStandardError = true;
				proc.Start();

				result += proc.StandardOutput.ReadToEnd();
				result += proc.StandardError.ReadToEnd();

				proc.WaitForExit();
			}

			await LogCreateOutput(result, filename);
		}

		public static async Task LogCreateOutput(string result, string filename)
		{
			await File.AppendAllTextAsync($"createOutput_{filename}.log", result);
		}

		public static async Task AddPathToFinishedFile(string path)
		{
			await File.AppendAllTextAsync(FinishedPathsLogFile, $"{path}\r\n");
		}

		public static async Task<bool> IsPathInFinishedFile(string path)
		{
			if (!File.Exists(path))
			{
				return false;
			}

			var lines = await File.ReadAllLinesAsync(FinishedPathsLogFile);
			return lines.Contains(path);
		}

		public static bool IsLinux()
		{
			int p = (int)Environment.OSVersion.Platform;
			return (p == 4) || (p == 6) || (p == 128);
		}
	}
}
