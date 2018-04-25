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
			var dateTimeTaken = DateTime.ParseExact(
				fileName.Replace(".jpg", string.Empty), 
				"yyyy-MM-dd HHmmss", 
				System.Globalization.CultureInfo.InvariantCulture);

			return new ImageFileDetails
			{
				Path = path,
				FileName = fileName,
				DateTimeTaken = dateTimeTaken
			};
		}
	}
}
