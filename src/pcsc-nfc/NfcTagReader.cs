using PCSC.Iso7816;
using PCSC;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PcscNfc
{
    public class NfcTagReader
    {
        public const byte MAGIC_NUMBER = 0xE1;
        public const byte MAJOR_VERSION = 0x1;

        const int NULL_TLV_TAG = 0x00;
        const int TERMINATOR_TLV_TAG = 0xFE;
        const int NDEF_MESSAGE_TLV_TAG = 0x03;

        public ICardReader Reader { get; }

        private byte[] cc;

        public NfcTagReader(ICardReader reader)
        {
            Reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        private byte[] ReadBlocks(byte block, int bytes = 16)
        {
            // command to read out blocks (up to 16 bytes)
            var apdu = new CommandApdu(IsoCase.Case2Short, Reader.Protocol)
            {
                CLA = 0xFF,
                Instruction = InstructionCode.ReadBinary,
                P1 = 0x00,
                P2 = block,
                Le = bytes
            };

            var sendPci = SCardPCI.GetPci(Reader.Protocol);
            var receivePci = new SCardPCI();

            var receiveBuffer = new byte[256];
            var command = apdu.ToArray();

            var bytesReceived = Reader.Transmit(
                sendPci,
                command,
                command.Length,
                receivePci,
                receiveBuffer,
                receiveBuffer.Length);

            var responseApdu =
                new ResponseApdu(receiveBuffer, bytesReceived, IsoCase.Case2Short, Reader.Protocol);

            if (responseApdu.SW1 != (byte)SW1Code.Normal)
            {
                throw new Exception($"SW1 is {responseApdu.SW1}");
            }

            return responseApdu.GetData();
        }

        // run NFC Tag detection procedure
        private void ValidateNfcTag()
        {
            cc = ReadBlocks(3, 4);

            if (cc[0] != MAGIC_NUMBER)
            {
                throw new Exception("Magic number does not match");
            }

            if ((cc[1] & 0xF0) >> 4 != MAJOR_VERSION)
            {
                throw new Exception("Version does not match");
            }

            if ((cc[3] & 0xF0) != 0)
            {
                throw new Exception("no unencryped read allowed");
            }
        }

        private IEnumerable<byte> ReadRemainingData()
        {
            byte block = 4;
            var bytesLeftToRead = cc[2] * 8;

            while (bytesLeftToRead > 0)
            {
                var readBytes = bytesLeftToRead > 16 ? 16 : bytesLeftToRead;
                foreach (var b in ReadBlocks(block, readBytes))
                {
                    yield return b;
                }

                block += (byte)(readBytes / 4);
                bytesLeftToRead -= readBytes;
            }
        }

        private int ReadLengthField(IEnumerator<byte> bytesEnumerator)
        {
            int length;

            bytesEnumerator.MoveNext();
            if (bytesEnumerator.Current == 0xFF)
            {
                bytesEnumerator.MoveNext();
                length = bytesEnumerator.Current << 8;
                bytesEnumerator.MoveNext();
                length += bytesEnumerator.Current; // endianess might be wrong
            }
            else
            {
                length = bytesEnumerator.Current;
            }

            return length;
        }

        private List<byte> ReadValueField(IEnumerator<byte> bytesEnumerator, int length)
        {
            var message = new List<byte>();
            for (int i = 0; i < length; i++)
            {
                bytesEnumerator.MoveNext();
                message.Add(bytesEnumerator.Current);
            }

            return message;
        }

        public byte[] ReadNdefMessage()
        {
            using (Reader.Transaction(SCardReaderDisposition.Leave))
            {
                ValidateNfcTag();

                var bytesEnumerator = ReadRemainingData().GetEnumerator();

                while (bytesEnumerator.MoveNext()) // null tlv
                {
                    var tag = bytesEnumerator.Current;

                    if (tag == 0xFF)
                    {
                        throw new Exception("unexpected value");
                    }

                    if (tag == NULL_TLV_TAG)
                    {
                        continue;
                    }

                    if (tag == TERMINATOR_TLV_TAG)
                    {
                        return new byte[0];
                    }

                    // all other field have a length tag
                    var length = ReadLengthField(bytesEnumerator);

                    if (tag == NDEF_MESSAGE_TLV_TAG)
                    {
                        return ReadValueField(bytesEnumerator, length).ToArray();
                    }
                    else
                    {
                        // skip all other tags
                        // TODO: interpret tags and implement other tags

                        ReadValueField(bytesEnumerator, length);
                    }
                }

                throw new Exception("no ndef message tlv found");
            }
        }


        private void WriteBlock(byte block, byte[] data)
        {
            // command to read out blocks (up to 16 bytes)
            var apdu = new CommandApdu(IsoCase.Case3Short, Reader.Protocol)
            {
                CLA = 0xFF,
                Instruction = InstructionCode.UpdateBinary,
                P1 = 0x00,
                P2 = block,
                Data = data
            };

            var sendPci = SCardPCI.GetPci(Reader.Protocol);
            var receivePci = new SCardPCI();

            var receiveBuffer = new byte[256];
            var command = apdu.ToArray();

            var bytesReceived = Reader.Transmit(
                sendPci,
                command,
                command.Length,
                receivePci,
                receiveBuffer,
                receiveBuffer.Length);

            var responseApdu =
                new ResponseApdu(receiveBuffer, bytesReceived, IsoCase.Case3Short, Reader.Protocol);

            if (responseApdu.SW1 != (byte)SW1Code.Normal)
            {
                throw new Exception($"SW1 is {responseApdu.SW1}");
            }
        }

        public void ClearCard()
        {
            using (Reader.Transaction(SCardReaderDisposition.Leave))
            {
                ValidateNfcTag();

                byte block = 4;
                var bytesLeftToClear = cc[2] * 8;

                while(bytesLeftToClear > 0)
                {
                    WriteBlock(block, new byte[] { 0, 0, 0, 0 });
                    block++;
                    bytesLeftToClear -= 4;
                }

            }
        }

        int WriteableData { get { return cc[2] * 8; } }

        public void WriteNdefMessage(byte[] message)
        {
            if (message.Length > 0xFFFF)
            {
                throw new Exception("message to long");
            }

            using (Reader.Transaction(SCardReaderDisposition.Leave))
            {
                ValidateNfcTag();


                byte[] lengthPart;
                if (message.Length > 0xFE)
                {
                    lengthPart = new byte[] { 0xFF, (byte)(message.Length >> 8), (byte)(message.Length & 0xFF) };
                }
                else
                {
                    lengthPart = new byte[] { (byte)(message.Length & 0xFF) };
                }

                var data = (new byte[] { NDEF_MESSAGE_TLV_TAG })
                    .Concat(lengthPart)
                    .Concat(message)
                    .Concat((new byte[] { TERMINATOR_TLV_TAG }))
                    .ToArray();

                if (data.Length % 4 != 0)
                {
                    data = data.Concat(new byte[4 - (data.Length % 4)]).ToArray();
                }

                if (data.Length > WriteableData)
                {
                    throw new Exception("message to long for card");
                }

                for (byte block = 4, offset = 0; offset < data.Length; block++, offset += 4)
                {
                    WriteBlock(block, data.Skip(offset).Take(4).ToArray());
                }
            }
        }
    }
};
