using NdefLibrary.Ndef;
using PCSC;
using System;
using System.Linq;

namespace write_nfc_tag
{
    internal class Program
    {
        static void Main(string[] args)
        {

			using (var context = ContextFactory.Instance.Establish(SCardScope.System))
			{
				var readerNames = context.GetReaders();
				var readerName = readerNames.Where(s => !s.Contains("Hello")).First(); // Filter out Windows Hello interface

				using (var rfidReader = context.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any))
				{

					var nfcReader = new PcscNfc.NfcTagReader(rfidReader);

					Console.WriteLine("Press enter to write tag");
					Console.ReadLine();

					NdefTextRecord nfcRecord = new NdefTextRecord();

					nfcRecord.LanguageCode = "en";

					nfcRecord.Text = "Hello";

					NdefMessage message = new NdefMessage();

					message.Add(nfcRecord);

					nfcReader.WriteNdefMessage(message.ToByteArray());

					Console.WriteLine("done");
				}
			}
		}
    }
}
