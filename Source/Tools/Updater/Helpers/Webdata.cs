using System;
using System.Net;
using System.IO;

namespace mxd.GZDBUpdater
{
    public delegate void BytesDownloadedEventHandler(ByteArgs e);

    public class ByteArgs : EventArgs
    {
	    public int Downloaded;
	    public int Total;
    }

    static class Webdata
    {
        public static event BytesDownloadedEventHandler BytesDownloaded;

		// styd: the file is written as it comes (not kept in memory), and its size is checked
		public static bool SaveWebFile(string url, long expectedsize, string file)
		{
			try
			{
				// GitHub only accepts TLS 1.2 or later
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

				//open a data stream from the supplied URL
				WebRequest webReq = WebRequest.Create(url);
				webReq.Timeout = 30000;
				HttpWebRequest http = webReq as HttpWebRequest;
				if(http != null)
				{
					http.UserAgent = "Doom-Builder-64-II-Updater";
					http.ReadWriteTimeout = 60000;
				}

				long total, downloaded = 0;
				using(WebResponse webResponse = webReq.GetResponse())
				using(Stream dataStream = webResponse.GetResponseStream())
				using(FileStream newFile = new FileStream(file, FileMode.Create))
				{
					//Get the total size of the download
					total = (webResponse.ContentLength > 0 ? webResponse.ContentLength : expectedsize);

					//lets declare our downloaded bytes event args
					ByteArgs byteArgs = new ByteArgs { Downloaded = 0, Total = (int)total };

					//we need to test for a null as if an event is not consumed we will get an exception
					if(BytesDownloaded != null) BytesDownloaded(byteArgs);

					//Download the data in chuncks
					byte[] dataBuffer = new byte[65536];
					while(!MainForm.AppClosing)
					{
						//Let's try and read the data
						int bytesFromStream = dataStream.Read(dataBuffer, 0, dataBuffer.Length);
						if(bytesFromStream == 0) break; //Download complete

						//Write the downloaded data
						newFile.Write(dataBuffer, 0, bytesFromStream);
						downloaded += bytesFromStream;
						byteArgs.Downloaded = (int)downloaded;
						if(BytesDownloaded != null) BytesDownloaded(byteArgs);
					}
				}

				if(MainForm.AppClosing)
				{
					File.Delete(file);
					return false;
				}

				if((expectedsize > 0 && downloaded != expectedsize) || (total > 0 && downloaded != total))
				{
					File.Delete(file);
					MainForm.ErrorDescription = "Failed to download the update...\nThe downloaded file is not complete (" + downloaded + " of " + Math.Max(expectedsize, total) + " bytes).";
					return false;
				}

				return true;
			}
			catch(Exception e)
			{
				try { if(File.Exists(file)) File.Delete(file); }
				catch(Exception) { }

				//We may not be connected to the internet
				//Or the URL may be incorrect
				MainForm.ErrorDescription = "Failed to download the update...\n" + e.Message;
				return false;
			}
		}
    }
}
