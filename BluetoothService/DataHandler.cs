using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace Ktos.SocketService.Bluetooth
{
    enum DataHandlerTypes : int
    {
        ByteArray = 0,
        Int32 = 1,
        String = 2,
        IBuffer = 3
    }

    class DataHandler
    {
        public static Type FromDataHandlerTypes(DataHandlerTypes dht)
        {
            switch (dht)
            {
                case DataHandlerTypes.ByteArray:
                    return typeof(byte[]);
                case DataHandlerTypes.IBuffer:
                    return typeof(IBuffer);
                case DataHandlerTypes.Int32:
                    return typeof(Int32);
                case DataHandlerTypes.String:
                    return typeof(string);
            }

            return typeof(object);
        }

        public static async Task WriteDataAsync(DataWriter chatWriter, object data)
        {
            if (data.GetType() == typeof(byte[]))
            {
                chatWriter.WriteInt32((int)DataHandlerTypes.ByteArray);
                chatWriter.WriteUInt32((uint)(data as byte[]).Length);
                chatWriter.WriteBytes(data as byte[]);
            }
            else if (data.GetType() == typeof(IBuffer))
            {
                chatWriter.WriteInt32((int)DataHandlerTypes.IBuffer);
                chatWriter.WriteUInt32((data as IBuffer).Length);
                chatWriter.WriteBuffer(data as IBuffer);
            }
            else if (data.GetType() == typeof(Int32))
            {
                chatWriter.WriteInt32((int)DataHandlerTypes.Int32);
                chatWriter.WriteUInt32(sizeof(Int32));
                chatWriter.WriteInt32((int)data);
            }
            else if (data.GetType() == typeof(string))
            {
                chatWriter.WriteInt32((int)DataHandlerTypes.String);
                chatWriter.WriteUInt32((uint)(data as string).Length);
                chatWriter.WriteString(data as string);
            }

            await chatWriter.StoreAsync();
        }

        public static DataHandlerTypes RecognizeDataType(DataReader reader)
        {
            int dataType = reader.ReadInt32();
            return (DataHandlerTypes)dataType;
        }

        public static async Task<byte[]> ReadByteArrayAsync(DataReader reader)
        {
            uint dataLength = await LoadByDataLength(reader);
            byte[] result = new byte[dataLength];

            reader.ReadBytes(result);

            return result;
        }

        public static async Task<string> ReadStringAsync(DataReader reader)
        {
            uint dataLength = await LoadByDataLength(reader);
            return reader.ReadString(dataLength);
        }

        public static async Task<int> ReadInt32Async(DataReader reader)
        {
            uint dataLength = await LoadByDataLength(reader);
            return reader.ReadInt32();
        }

        private static async Task<uint> LoadByDataLength(DataReader reader)
        {
            var dataLength = reader.ReadUInt32();

            uint actualDataLength = await reader.LoadAsync(dataLength);
            if (actualDataLength == dataLength)
                return dataLength;
            else
                throw new Exception("Underlying connection lost");
        }

        public static async Task<IBuffer> ReadIBufferAsync(DataReader reader)
        {
            uint dataLength = await LoadByDataLength(reader);
            return reader.ReadBuffer(dataLength);
        }
    }
}
