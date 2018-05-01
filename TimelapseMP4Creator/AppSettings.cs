namespace TimelapseMP4Creator
{
	public class AppSettings
	{
		public string SourceImageLocation { get; set; }
		public string LocalImageLocation { get; set; }
		public string WindowsFfmpegLocation { get; set; }
		public string MP4OutputDirectory { get; set; }
		public string UnsortedImagesDirectory { get; set; }
	}
}
