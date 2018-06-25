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
			Get1400HourFiles();

			while (true)
			{
				await Task.WhenAll(
					Run(),
					Task.Delay(60 * 60 * 1000));
			}
		}

		public static async Task Run()
		{
			var config = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json")
				.Build();

			var appSettings = config.GetSection("AppSettings").Get<AppSettings>();

			//UnsortedFiles(appSettings);
			//await CreateTimelapseMP4FromUnsortedFiles(appSettings);

			var directories = Directory.EnumerateDirectories(appSettings.SourceImageLocation);
			foreach (var directory in directories)
			{
				var date = Path.GetFileName(directory);
				if (date.Equals(DateTime.Today.ToString("yyyy-MM-dd"), StringComparison.CurrentCultureIgnoreCase))
				{
					continue;
				}

				var sourceDirectory = Path.Combine(appSettings.SourceImageLocation, date);
				var destinationDirectory = Path.Combine(appSettings.LocalImageLocation, date);

				await GetFilesAndSaveResized(sourceDirectory, destinationDirectory);
				//await CreateTimelapseMP4(appSettings, destinationDirectory, date);
			}
		}

		public static async Task GetFilesAndSaveResized(string sourceDirectory, string destinationDirectory)
		{
			if (await IsPathInFinishedFile(sourceDirectory))
			{
				Console.WriteLine($"Skipping copy files and resize for directory {sourceDirectory}");
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

		public static async Task CreateTimelapseMP4(AppSettings appSettings, string localImageDirectory, string filename)
		{
			if (!Directory.Exists(appSettings.MP4OutputDirectory))
			{
				Directory.CreateDirectory(appSettings.MP4OutputDirectory);
			}

			var savePath = $"{appSettings.MP4OutputDirectory}\\{filename}.mp4";
			if (File.Exists(savePath))
			{
				Console.WriteLine($"Movie already exists for savePath {savePath}");
				return;
			}

			if (!Directory.EnumerateFiles(localImageDirectory, "*.jpg").Any())
			{
				Console.WriteLine($"No files to create movie in directory {localImageDirectory}");
				return;
			}

			var stopwatch = Stopwatch.StartNew();
			var result = string.Empty;
			if (IsLinux())
			{
				string command = $"ffmpeg -framerate 30 -i {localImageDirectory}/image_%04d.jpg -c:v libx264 -r 30 {savePath}";
				Console.WriteLine(command);

				result = $"{command}\r\n";
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
			}
			else
			{
				string command = $"\"{appSettings.WindowsFfmpegLocation}\" -framerate 30 -i {localImageDirectory}\\image_%04d.jpg -c:v libx264 -r 30 {savePath}";
				Console.WriteLine(command);
				await File.AppendAllTextAsync($"windowsCommands.log", $"{command}\r\n");

				result = $"{command}\r\n";
				//using (var proc = new Process())
				//{
				//	proc.StartInfo.FileName = "cmd.exe";
				//	proc.StartInfo.Arguments = "/C " + command;
				//	proc.StartInfo.UseShellExecute = false;
				//	proc.StartInfo.RedirectStandardOutput = true;
				//	proc.StartInfo.RedirectStandardError = true;

				//	proc.Start();

				//	result += proc.StandardOutput.ReadToEnd();
				//	result += proc.StandardError.ReadToEnd();

				//	proc.WaitForExit();
				//}
			}

			var elapsedMillis = stopwatch.ElapsedMilliseconds;
			Console.WriteLine($"Finished creating movie in {elapsedMillis} ms");
			result += $"\r\nelapsedMillis: {elapsedMillis}\r\n";
			await LogCreateOutput(result, filename);
		}

		public static void Get1400HourFiles()
		{
			var saveDir = "1400HourFiles";
			if (!Directory.Exists(saveDir))
			{
				Directory.CreateDirectory(saveDir);
			}

			var fileGroup = Directory.GetFiles(@"\\192.168.1.14\projects\VWP Timelapse\timelapse construction", "*.jpg", SearchOption.AllDirectories)
				.Select(item => ImageFileDetails.CreateImageFileDetails(item))
				.Where(item => item != null)
				.GroupBy(item => item.DateTimeTaken.Date)
				.ToList();

			foreach (var files in fileGroup)
			{
				var fileAfter = files
					.OrderBy(item => item.DateTimeTaken)
					.FirstOrDefault(item => item.DateTimeTaken.Hour >= 14);

				var fileBefore = files
					.OrderByDescending(item => item.DateTimeTaken)
					.FirstOrDefault(item => item.DateTimeTaken.Hour < 14);

				ImageFileDetails saveFile = null;
				if ((fileAfter?.DateTimeTaken.Hour ?? 23) - 14 <= 14 - (fileBefore?.DateTimeTaken.Hour ?? 0))
				{
					saveFile = fileAfter;
				}

				if ((fileAfter?.DateTimeTaken.Hour ?? 23) - 14 >= 14 - (fileBefore?.DateTimeTaken.Hour ?? 0))
				{
					saveFile = fileBefore;
				}

				if (saveFile == null && fileBefore != null)
				{
					saveFile = fileBefore;
				}

				if (saveFile == null)
				{
					continue;
				}
				else
				{
					try
					{
						// Load the image and save a resized version
						using (var image = Image.Load(saveFile.Path))
						{
							image.Mutate(x => x.Resize(image.Width / 2, image.Height / 2));
							image.Save($"{saveDir}\\{saveFile.DateTimeTaken.ToString("yyyy-MM-ddTHHmmss")}.jpg");
						}

						// Load the image and save a thumbnail
						using (var image = Image.Load(saveFile.Path))
						{
							var height = 200;
							decimal widthRatio = image.Height / height;
							int width = (int)Math.Round(image.Width / widthRatio, 0);
							image.Mutate(x => x.Resize(width, height));
							image.Save($"{saveDir}\\{saveFile.DateTimeTaken.ToString("yyyy-MM-ddTHHmmss")}_thumb.jpg");
						}
					}
					catch (Exception excep)
					{
						var a = excep.Message;
					}
					
				}
			}
		}

		public static void UnsortedFiles(AppSettings appSettings)
		{
			var filesToCopy = Directory.GetFiles(appSettings.UnsortedImagesDirectory, "*.jpg", SearchOption.AllDirectories)
				.Select(item => ImageFileDetails.CreateImageFromEpochFile(item))
				.ToList();
			filesToCopy.RemoveAll(item => item == null);
			filesToCopy = filesToCopy.Distinct().ToList();

			// Sort the files so that renaming with index is possible
			filesToCopy = filesToCopy.OrderBy(item => item.DateTimeTaken).ToList();

			var finishedDates = Directory.GetDirectories(appSettings.LocalImageLocation)
				.Select(item => {
					DateTime.TryParseExact(
						Path.GetFileName(item),
						"yyyy-MM-dd",
						System.Globalization.CultureInfo.InvariantCulture,
						System.Globalization.DateTimeStyles.None, out var parsedDate);
					return parsedDate;
				})
				.ToList();
			filesToCopy.RemoveAll(item => finishedDates.Contains(item.DateTimeTaken.Date));

			Console.WriteLine($"Total files in directory {appSettings.UnsortedImagesDirectory}: {filesToCopy.Count}");

			var index = 0;
			var overallIndex = 0;
			var stopwatch = Stopwatch.StartNew();

			foreach (var fileToCopy in filesToCopy)
			{
				stopwatch.Restart();

				// Create destinationDirectory if needed and reset index (files are sorted)
				var destinationDirectory = Path.Combine(appSettings.LocalImageLocation, fileToCopy.DateTimeTaken.ToString("yyyy-MM-dd"));
				if (!Directory.Exists(destinationDirectory))
				{
					Directory.CreateDirectory(destinationDirectory);
					index = 0;
				}

				var localFileName = $"image_{index++.ToString("D4")}.jpg";
				var destinationPath = Path.Combine(destinationDirectory, localFileName);
				long downloadTimeInSeconds = 0;
				try
				{
					// Load the image and save a resized version
					using (var image = Image.Load(fileToCopy.Path))
					{
						downloadTimeInSeconds = stopwatch.ElapsedMilliseconds;
						image.Mutate(x => x.Resize(image.Width / 2, image.Height / 2));
						image.Save(destinationPath);
					}

					var info = $"Finished copying and resizing file: {fileToCopy.FileName} date {fileToCopy.DateTimeTaken.ToString("yyyy-MM-dd HH:mm:ss")}. " +
						$"File {overallIndex++}/{filesToCopy.Count}. " +
						$"Statistics {downloadTimeInSeconds} - {stopwatch.ElapsedMilliseconds}";
					Console.WriteLine(info);
				}
				catch (Exception excep)
				{
					index--;
					// Oops, 0 kb file?
				}
			}
		}

		public static async Task CreateTimelapseMP4FromUnsortedFiles(AppSettings appSettings)
		{
			var sources = Directory.EnumerateDirectories(appSettings.LocalImageLocation);
			foreach (var source in sources)
			{
				var date = Path.GetFileName(source);
				await CreateTimelapseMP4(appSettings, source, date);
			}
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
			if (!File.Exists(FinishedPathsLogFile))
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
