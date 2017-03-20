using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace Ktos.SocketService.Bluetooth
{
    /// <summary>
    /// A Delegate for event when client connects to the server
    /// </summary>
    /// <param name="sender">Instance of the server class</param>
    /// <param name="e">Client ID and client socket information</param>
    public delegate void ClientConnectedEventHandler(object sender, ClientConnectedEventArgs e);

    /// <summary>
    /// A delegate handling disconnection event
    /// </summary>
    /// <param name="sender">Instance of client/server class</param>
    /// <param name="e">Client ID of a disconnected client and exception (if any)</param>
    public delegate void DisconnectedEventHandler(object sender, DisconnectedEventArgs e);

    /// <summary>
    /// Event args when client connects
    /// </summary>
    public class ClientConnectedEventArgs : EventArgs
    {
        /// <summary>
        /// Information about connected client
        /// </summary>
        public StreamSocketInformation ClientInformation { get { return this.clientInformation; } }
        private StreamSocketInformation clientInformation;

        /// <summary>
        /// GUID of connected client
        /// </summary>
        public string ClientId { get; private set; }

        /// <summary>
        /// Creates a new ClientConnectedEventArgs object
        /// </summary>
        /// <param name="clientInformation">Client socket information</param>
        /// <param name="clientId">Client's GUID</param>
        public ClientConnectedEventArgs(StreamSocketInformation clientInformation, string clientId)
            : base()
        {
            this.clientInformation = clientInformation;
            this.ClientId = clientId;
        }
    }

    /// <summary>
    /// Event args when client is disconnected
    /// </summary>
    public class DisconnectedEventArgs : EventArgs
    {
        /// <summary>
        /// Exception, if disconnection is because of exception
        /// </summary>
        public Exception Error { get; private set; }

        /// <summary>
        /// Client ID, which was disconnected
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Creates a new DisconnectedEventArgs object
        /// </summary>
        /// <param name="ex">Exception, if disconnection is because of exception</param>
        public DisconnectedEventArgs(Exception ex)
            : base()
        {
            this.Error = ex;
        }

        /// <summary>
        /// Creates a new DisconnectedEventArgs object
        /// </summary>
        /// <param name="id">Client GUID, when client disconnects</param>
        public DisconnectedEventArgs(string id)
            : base()
        {
            this.Id = id;
        }

        /// <summary>
        /// Creates a new DisconnectedEventArgs object
        /// </summary>
        /// <param name="ex">Exception, if disconnection is because of exception</param>
        /// <param name="id">Client GUID of a disconnected client</param>
        public DisconnectedEventArgs(Exception ex, string id)
            : base()
        {
            this.Error = ex;
            this.Id = id;
        }
    }

    /// <summary>
    /// Generic class for various exceptions from SocketService
    /// </summary>
    public class SocketServiceException : Exception
    {
        /// <summary>
        /// Creates a new SocketServiceException object
        /// </summary>
        /// <param name="message">Informational message about exception</param>
        public SocketServiceException(string message)
            : base(message)
        {

        }

        /// <summary>
        /// Creates a new SocketServiceException object
        /// </summary>
        /// <param name="message">Informational message about exception</param>
        /// <param name="innerException">Exception which caused throwing SocketServiceException</param>
        public SocketServiceException(string message, Exception innerException)
            : base(message, innerException)
        {

        }

    }
}
