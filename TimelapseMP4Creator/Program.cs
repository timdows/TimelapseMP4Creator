using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Transforms;
using System;
using System.IO;
using System.Linq;

namespace TimelapseMP4Creator
{
	public class Program
	{
		const string FinishedPathsLogFile = "finishedPaths.log";

		public static void Main(string[] args)
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

				if (IsPathInFinishedFile(sourceDirectory))
				{
					continue;
				}

				GetFilesAndSaveResized(sourceDirectory, destinationDirectory);
				CreateTimelapseMP4(destinationDirectory);
				AddPathToFinishedFile(sourceDirectory);
			}
		}

		public static void GetFilesAndSaveResized(string sourceDirectory, string destinationDirectory)
		{
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
			foreach (var fileToCopy in filesToCopy)
			{
				var localFileName = $"image_{index++.ToString("D4")}.jpg";
				var destinationPath = Path.Combine(destinationDirectory, localFileName);

				// Load the image and save a resized version
				using (var image = Image.Load(fileToCopy.Path))
				{
					image.Mutate(x => x.Resize(image.Width / 2, image.Height / 2));
					image.Save(destinationPath);
				}

				Console.WriteLine($"Finished copying and resizing file: {fileToCopy.FileName}. File {index}/{filesToCopy.Count}");
			}
		}

		public static void CreateTimelapseMP4(string localImageDirectory)
		{

		}

		public static void AddPathToFinishedFile(string path)
		{
			File.AppendAllText(FinishedPathsLogFile, $"{path}\r\n");
		}

		public static bool IsPathInFinishedFile(string path)
		{
			if (!File.Exists(path))
			{
				return false;
			}

			var lines = File.ReadAllLines(FinishedPathsLogFile);
			return lines.Contains(path);
		}
	}
}
