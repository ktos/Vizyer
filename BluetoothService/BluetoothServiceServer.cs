using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Ktos.SocketService.Bluetooth
{
    class BluetoothServiceServer
    {
        private RfcommServiceProvider rfcommProvider;
        private StreamSocketListener socketListener;

        public Guid RfcommChatServiceUuid { get; set; } = Guid.Parse("34B1CF4D-1069-4AD6-89B6-E161D79BE4D8");

        public const byte SdpServiceNameAttributeType = (4 << 3) | 5;

        // The value of the Service Name SDP attribute
        public string SdpServiceName { get; set; } = "Bluetooth Rfcomm Chat Service";

        // The Id of the Service Name SDP attribute
        public const UInt16 SdpServiceNameAttributeId = 0x100;
        private DataWriter writer;
        private StreamSocket socket;

        /// <summary>
        /// Initializes the server using RfcommServiceProvider to advertise the Chat Service UUID and start listening
        /// for incoming connections.
        /// </summary>
        private async void InitializeServer()
        {
            try
            {
                rfcommProvider = await RfcommServiceProvider.CreateAsync(RfcommServiceId.FromUuid(RfcommChatServiceUuid));
            }
            // Catch exception HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE).
            catch (Exception ex) when ((uint)ex.HResult == 0x800710DF)
            {
                // The Bluetooth radio may be off.
                throw new InvalidOperationException("Make sure your Bluetooth Radio is on: " + ex.Message, ex);
            }


            // Create a listener for this service and start listening
            socketListener = new StreamSocketListener();
            socketListener.ConnectionReceived += OnConnectionReceived;
            var rfcomm = rfcommProvider.ServiceId.AsString();

            await socketListener.BindServiceNameAsync(rfcommProvider.ServiceId.AsString(),
                SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);

            // Set the SDP attributes and start Bluetooth advertising
            InitializeServiceSdpAttributes(rfcommProvider);

            try
            {
                rfcommProvider.StartAdvertising(socketListener, true);
            }
            catch (Exception e)
            {
                // If you aren't able to get a reference to an RfcommServiceProvider, tell the user why.  Usually throws an exception if user changed their privacy settings to prevent Sync w/ Devices.  
                throw new Exception(e.Message);
            }
        }

        /// <summary>
        /// Creates the SDP record that will be revealed to the Client device when pairing occurs.  
        /// </summary>
        /// <param name="rfcommProvider">The RfcommServiceProvider that is being used to initialize the server</param>
        private void InitializeServiceSdpAttributes(RfcommServiceProvider rfcommProvider)
        {
            var sdpWriter = new DataWriter();

            // Write the Service Name Attribute.
            sdpWriter.WriteByte(SdpServiceNameAttributeType);

            // The length of the UTF-8 encoded Service Name SDP Attribute.
            sdpWriter.WriteByte((byte)SdpServiceName.Length);

            // The UTF-8 encoded Service Name value.
            sdpWriter.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            sdpWriter.WriteString(SdpServiceName);

            // Set the SDP Attribute on the RFCOMM Service Provider.
            rfcommProvider.SdpRawAttributes.Add(SdpServiceNameAttributeId, sdpWriter.DetachBuffer());
        }

        private void Disconnect()
        {
            if (rfcommProvider != null)
            {
                rfcommProvider.StopAdvertising();
                rfcommProvider = null;
            }

            if (socketListener != null)
            {
                socketListener.Dispose();
                socketListener = null;
            }

            if (writer != null)
            {
                writer.DetachStream();
                writer = null;
            }

            if (socket != null)
            {
                socket.Dispose();
                socket = null;
            }
        }

        public event DataReceivedEventHandler DataReceived;

        /// <summary>
        /// Invoked when the socket listener accepts an incoming Bluetooth connection.
        /// </summary>
        /// <param name="sender">The socket listener that accepted the connection.</param>
        /// <param name="args">The connection accept parameters, which contain the connected socket.</param>
        private async void OnConnectionReceived(
            StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            // Don't need the listener anymore
            socketListener.Dispose();
            socketListener = null;

            try
            {
                socket = args.Socket;
            }
            catch (Exception)
            {
                Disconnect();
                return;
            }

            // Note - this is the supported way to get a Bluetooth device from a given socket
            var remoteDevice = await BluetoothDevice.FromHostNameAsync(socket.Information.RemoteHostName);

            writer = new DataWriter(socket.OutputStream);
            var reader = new DataReader(socket.InputStream);
            bool remoteDisconnection = false;

            // TODO: notify connected
            //await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            //{
            //    rootPage.NotifyUser("Connected to Client: " + remoteDevice.Name, NotifyType.StatusMessage);
            //});

            // Infinite read buffer loop
            while (true)
            {
                try
                {
                    uint size = await reader.LoadAsync(sizeof(uint));
                    if (size < sizeof(uint))
                    {
                        remoteDisconnection = true;
                        return;
                    }

                    var type = DataHandler.RecognizeDataType(reader);
                    switch (type)
                    {
                        case DataHandlerTypes.ByteArray:
                            DataReceived?.Invoke(this, new DataReceivedEventArgs(await DataHandler.ReadByteArrayAsync(reader), typeof(byte[]))); break;
                        case DataHandlerTypes.Int32:
                            DataReceived?.Invoke(this, new DataReceivedEventArgs(await DataHandler.ReadInt32Async(reader), typeof(Int32))); break;
                        case DataHandlerTypes.String:
                            DataReceived?.Invoke(this, new DataReceivedEventArgs(await DataHandler.ReadStringAsync(reader), typeof(string))); break;
                        case DataHandlerTypes.IBuffer:
                            DataReceived?.Invoke(this, new DataReceivedEventArgs(await DataHandler.ReadIBufferAsync(reader), typeof(IBuffer))); break;
                    }
                    
                }
                // Catch exception HRESULT_FROM_WIN32(ERROR_OPERATION_ABORTED).
                catch (Exception ex) when ((uint)ex.HResult == 0x800703E3)
                {
                    // Client Disconnected Successfully
                    break;
                }
            }

            reader.DetachStream();
            if (remoteDisconnection)
            {
                Disconnect();
                // Client Disconnected Successfully
            }
        }
    }
}
