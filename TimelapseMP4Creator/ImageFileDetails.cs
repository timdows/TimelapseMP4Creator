using System;

namespace TimelapseMP4Creator
{
	public class ImageFileDetails
    {
		public string Path { get; set; }
		public string FileName { get; set; }
		public DateTime DateTimeTaken { get; set; }

		public static ImageFileDetails CreateImageFileDetails(string path)
		{
			var fileName = System.IO.Path.GetFileName(path);

			DateTime dateTimeTaken;

			if (!DateTime.TryParseExact(
				fileName.Replace(".jpg", string.Empty), 
				"yyyy-MM-dd HHmmss", 
				System.Globalization.CultureInfo.InvariantCulture, 
				System.Globalization.DateTimeStyles.None,
				out dateTimeTaken))
			{
				return null;
			}

			return new ImageFileDetails
			{
				Path = path,
				FileName = fileName,
				DateTimeTaken = dateTimeTaken
			};
		}

		public static ImageFileDetails CreateImageFromEpochFile(string path)
		{
			var fileName = System.IO.Path.GetFileName(path);
			DateTimeOffset dateTimeOffset;
			if (int.TryParse(fileName.Replace(".jpg", string.Empty), out var offset))
			{
				dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(offset);
			}
			else
			{
				return null;
			}
			
			DateTime dateTimeTaken = dateTimeOffset.UtcDateTime;

			return new ImageFileDetails
			{
				Path = path,
				FileName = fileName,
				DateTimeTaken = dateTimeTaken
			};
		}
	}
}
