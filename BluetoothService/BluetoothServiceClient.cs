using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Ktos.SocketService.Bluetooth
{
    public delegate void DataReceivedEventHandler(object sender, DataReceivedEventArgs e);

    /// <summary>
    /// Event arguments when data from client (or server) are received
    /// </summary>
    public class DataReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Received message (without length)
        /// </summary>
        public object Data { get; private set; }

        public Type DataType { get; private set; }

        /// <summary>
        /// Creates a new DataReceivedEventArgs object
        /// </summary>
        /// <param name="data">A data received from connection</param>
        public DataReceivedEventArgs(object data, Type dataType)
            : base()
        {
            Data = data;
            DataType = dataType;
        }
    }


    public class BluetoothServiceClient
    {
        private BluetoothDevice bluetoothDevice;
        private RfcommDeviceService chatService;
        private StreamSocket chatSocket;
        private DataWriter chatWriter;

        public Guid RfcommChatServiceUuid { get; set; } = Guid.Parse("34B1CF4D-1069-4AD6-89B6-E161D79BE4D8");

        // The Id of the Service Name SDP attribute
        public const UInt16 SdpServiceNameAttributeId = 0x100;

        // The SDP Type of the Service Name SDP attribute.
        // The first byte in the SDP Attribute encodes the SDP Attribute Type as follows :
        //    -  the Attribute Type size in the least significant 3 bits,
        //    -  the SDP Attribute Type value in the most significant 5 bits.
        public const byte SdpServiceNameAttributeType = (4 << 3) | 5;

        // The value of the Service Name SDP attribute
        public string SdpServiceName { get; set; } = "Bluetooth Rfcomm Chat Service";

        public async void ConnectAsync(RfcommChatDeviceDisplay deviceInfoDisp)
        {
            // Perform device access checks before trying to get the device.
            // First, we check if consent has been explicitly denied by the user.
            DeviceAccessStatus accessStatus = DeviceAccessInformation.CreateFromId(deviceInfoDisp.Id).CurrentStatus;
            if (accessStatus == DeviceAccessStatus.DeniedByUser)
            {
                throw new UnauthorizedAccessException("This app does not have access to connect to the remote device (please grant access in Settings > Privacy > Other Devices");
            }
            // If not, try to get the Bluetooth device
            try
            {
                bluetoothDevice = await BluetoothDevice.FromIdAsync(deviceInfoDisp.Id);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            // If we were unable to get a valid Bluetooth device object,
            // it's most likely because the user has specified that all unpaired devices
            // should not be interacted with.
            if (bluetoothDevice == null)
            {
                throw new InvalidOperationException("Bluetooth Device returned null. Access Status = " + accessStatus.ToString());
            }

            // This should return a list of uncached Bluetooth services (so if the server was not active when paired, it will still be detected by this call
            var rfcommServices = await bluetoothDevice.GetRfcommServicesForIdAsync(
                RfcommServiceId.FromUuid(RfcommChatServiceUuid), BluetoothCacheMode.Uncached);

            if (rfcommServices.Services.Count > 0)
            {
                chatService = rfcommServices.Services[0];
            }
            else
            {
                throw new InvalidOperationException("Could not discover the chat service on the remote device");
            }

            // Do various checks of the SDP record to make sure you are talking to a device that actually supports the Bluetooth Rfcomm Chat Service
            var attributes = await chatService.GetSdpRawAttributesAsync();
            if (!attributes.ContainsKey(SdpServiceNameAttributeId))
            {
                throw new InvalidOperationException("The Chat service is not advertising the Service Name attribute (attribute id=0x100). " +
                    "Please verify that you are running the BluetoothRfcommChat server.");
            }
            var attributeReader = DataReader.FromBuffer(attributes[SdpServiceNameAttributeId]);
            var attributeType = attributeReader.ReadByte();
            if (attributeType != SdpServiceNameAttributeType)
            {
                throw new InvalidOperationException(
                    "The Chat service is using an unexpected format for the Service Name attribute. " +
                    "Please verify that you are running the BluetoothRfcommChat server.");
            }
            var serviceNameLength = attributeReader.ReadByte();

            // The Service Name attribute requires UTF-8 encoding.
            attributeReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

            lock (this)
            {
                chatSocket = new StreamSocket();
            }
            try
            {
                await chatSocket.ConnectAsync(chatService.ConnectionHostName, chatService.ConnectionServiceName);

                // TODO: powiadomienie, że połączono
                //SetChatUI(attributeReader.ReadString(serviceNameLength), bluetoothDevice.Name);

                chatWriter = new DataWriter(chatSocket.OutputStream);
                DataReader chatReader = new DataReader(chatSocket.InputStream);

                ReceiveDataLoop(chatReader);
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x80070490) // ERROR_ELEMENT_NOT_FOUND
            {
                throw new InvalidOperationException("Please verify that you are running the BluetoothRfcommChat server.");
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x80072740) // WSAEADDRINUSE
            {
                throw new InvalidOperationException("Please verify that there is no other RFCOMM connection to the same device.");
            }
        }

        public async void SendMessageAsync(object data)
        {
            try
            {
                await DataHandler.WriteDataAsync(chatWriter, data);
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x80072745)
            {
                // The remote device has disconnected the connection
                throw new InvalidOperationException("Remote side disconnect: " + ex.HResult.ToString() + " - " + ex.Message, ex);
            }
        }

        public event DataReceivedEventHandler DataReceived;

        private async void ReceiveDataLoop(DataReader chatReader)
        {
            try
            {
                uint size = await chatReader.LoadAsync(sizeof(uint));
                if (size < sizeof(uint))
                {
                    Disconnect("Remote device terminated connection - make sure only one instance of server is running on remote device");
                    return;
                }

                var type = DataHandler.RecognizeDataType(chatReader);
                switch (type)
                {
                    case DataHandlerTypes.ByteArray:
                        DataReceived?.Invoke(this, new DataReceivedEventArgs(await DataHandler.ReadByteArrayAsync(chatReader), typeof(byte[]))); break;
                    case DataHandlerTypes.Int32:
                        DataReceived?.Invoke(this, new DataReceivedEventArgs(await DataHandler.ReadInt32Async(chatReader), typeof(Int32))); break;
                    case DataHandlerTypes.String:
                        DataReceived?.Invoke(this, new DataReceivedEventArgs(await DataHandler.ReadStringAsync(chatReader), typeof(string))); break;
                    case DataHandlerTypes.IBuffer:
                        DataReceived?.Invoke(this, new DataReceivedEventArgs(await DataHandler.ReadIBufferAsync(chatReader), typeof(IBuffer))); break;
                }

                ReceiveDataLoop(chatReader);
            }
            catch (Exception ex)
            {
                lock (this)
                {
                    if (chatSocket == null)
                    {
                        // Do not print anything here -  the user closed the socket.
                        //if ((uint)ex.HResult == 0x80072745)
                            //rootPage.NotifyUser("Disconnect triggered by remote device", NotifyType.StatusMessage);
                        //else if ((uint)ex.HResult == 0x800703E3)
                            //rootPage.NotifyUser("The I/O operation has been aborted because of either a thread exit or an application request.", NotifyType.StatusMessage);
                    }
                    else
                    {
                        Disconnect("Read stream failed with error: " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Cleans up the socket and DataWriter and reset the UI
        /// </summary>
        /// <param name="disconnectReason"></param>
        public void Disconnect(string disconnectReason)
        {
            if (chatWriter != null)
            {
                chatWriter.DetachStream();
                chatWriter = null;
            }


            if (chatService != null)
            {
                chatService.Dispose();
                chatService = null;
            }
            lock (this)
            {
                if (chatSocket != null)
                {
                    chatSocket.Dispose();
                    chatSocket = null;
                }
            }            
        }
    }
}
