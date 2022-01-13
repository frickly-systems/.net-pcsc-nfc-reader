using NdefLibrary.Ndef;
using PCSC;
using PCSC.Monitoring;

// very simple example to watch for inserted 

string[] readerNames;
using (var context = ContextFactory.Instance.Establish(SCardScope.System))
{
    readerNames = context.GetReaders();
}

using (var monitor = MonitorFactory.Instance.Create(SCardScope.System))
{
    monitor.CardInserted += (sender, args) => CardInserted(args);
    monitor.CardRemoved += (sender, args) => CardRemoved(args);

    monitor.Start(readerNames);

    Console.WriteLine("Press enter to exit");
    Console.ReadLine();
}

static void CardInserted(CardStatusEventArgs args)
{
    Console.WriteLine($"Card inserted: ATR {BitConverter.ToString(args.Atr)}");

    using (var context = ContextFactory.Instance.Establish(SCardScope.System))
    {

        using (var rfidReader = context.ConnectReader(args.ReaderName, SCardShareMode.Shared, SCardProtocol.Any))
        {
            try
            {
                var nfcReader = new PcscNfc.NfcTagReader(rfidReader);

                var rawMsg = nfcReader.ReadNdefMessage();
                var ndefMessage = NdefMessage.FromByteArray(rawMsg);

                foreach (NdefRecord record in ndefMessage)
                {
                    if (record.CheckSpecializedType(false) == typeof(NdefTextRecord))
                    {
                        var spRecord = new NdefTextRecord(record);
                        Console.WriteLine($"Text record: {spRecord.Text}");
                    }
                }
            } catch (Exception ex)
            {
                Console.Error.WriteLine("Error reading card as nfc tag");
            }
        }
    }
}

static void CardRemoved(CardStatusEventArgs args)
{
    Console.WriteLine($"Card removed");
}
