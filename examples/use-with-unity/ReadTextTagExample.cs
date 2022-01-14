using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PCSC;
using System.Threading;
using PCSC.Monitoring;
using PcscNfc;
using NdefLibrary.Ndef;
using System;

class NfcState
{
    public string Card { get; set; }
}

public class ReadTextTagExample : MonoBehaviour
{
    NfcState state = new NfcState();
    Thread t;

    void NfcThread()
    {

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

            try
            {
                Thread.Sleep(Timeout.Infinite);
            } catch (ThreadInterruptedException)
            {
                // thread interrupted
            }
        }

        void CardInserted(CardStatusEventArgs args)
        {
            lock(state)
            {
                state.Card = null;
            }
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
                                var textRecord = new NdefTextRecord(record);
                                lock (state)
                                {
                                    state.Card = textRecord.Text;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // catch error
                    }
                }
            }
        }

        void CardRemoved(CardStatusEventArgs args)
        {
            lock(state) { state.Card = null; }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        t = new Thread(NfcThread);
        t.Start();
    }

    void OnDestroy()
    {
        t.Interrupt();
    }

    // Update is called once per frame
    void Update()
    {
        lock (state)
        {
            Debug.Log(state.Card);
        }
    }
}
