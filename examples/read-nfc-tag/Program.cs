using NdefLibrary.Ndef;
using PCSC;
using System.Text;

using (var context = ContextFactory.Instance.Establish(SCardScope.System))
{
    var readerNames = context.GetReaders();
    var readerName = readerNames.Where(s => !s.Contains("Hello")).First(); // Filter out Windows Hello interface

    using (var rfidReader = context.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any))
    {
        var nfcReader = new PcscNfc.NfcTagReader(rfidReader);

		Console.WriteLine("Press enter to read tag");
		Console.ReadLine();

		var rawMsg = nfcReader.ReadNdefMessage();
		var ndefMessage = NdefMessage.FromByteArray(rawMsg);

		foreach (NdefRecord record in ndefMessage)
		{
			Console.WriteLine("Record type: " + Encoding.UTF8.GetString(record.Type, 0, record.Type.Length));
			if (record.CheckSpecializedType(false) == typeof(NdefTextRecord))
			{
				var spRecord = new NdefTextRecord(record);
				Console.Write($"Text {spRecord.Text}");
			}
		}
	}
}