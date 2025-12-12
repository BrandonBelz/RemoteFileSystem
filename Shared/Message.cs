namespace RemoteFileSystem.Shared;
using System.Net;
using System.Net.Sockets;
using System.Text;
public class Message {
    public MessageType Type { get; set; }
    public List<byte> Data { get; } = new List<byte>();
    public byte[] DataArray { get => Data.ToArray(); }
    public int DataLength { get => Data.Count; }

    public Message(MessageType type, byte[] data) {
        Type = type;
        Data = new List<byte>(data);
    }
    public Message(MessageType type) { Type = type; }
    public Message() {}
    public Message(MessageType type, string textData) {
        Type = type;
        Data = new List<byte>(Encoding.UTF8.GetBytes(textData));
    }

    public byte[] EncodeToBytes() {
        List<byte> messageBytes = new List<byte>();
        messageBytes.Add((byte)this.Type);

        byte[] dataLength = BitConverter.GetBytes(DataLength);
        if (BitConverter.IsLittleEndian) {
            Array.Reverse(dataLength);
        }
        messageBytes.AddRange(dataLength);

        messageBytes.AddRange(this.Data);
        return messageBytes.ToArray();
    }

    public async Task WriteMessageAsync(NetworkStream stream) {
        byte[] bytes = this.EncodeToBytes();
        await stream.WriteAsync(bytes, 0, bytes.Length);
    }

    public static async Task<Message>
    ReadNextMessageAsync(NetworkStream stream) {
        byte[] header = new byte[5];
        int read = 0;
        while (read < header.Length) {
            int bytesRead =
                await stream.ReadAsync(header, read, header.Length - read);
            read += bytesRead;
        }

        int dataLength = BitConverter.ToInt32(header, 1);
        dataLength = IPAddress.NetworkToHostOrder(
            dataLength); // Converts to system's integer representation

        byte[] data = new byte[dataLength];
        int dataRead = 0;
        while (dataRead < dataLength) {
            int bytesRead =
                await stream.ReadAsync(data, dataRead, dataLength - dataRead);
            if (bytesRead == 0) {
                throw new Exception("Connection Lost");
            }
            dataRead += bytesRead;
        }
        return new Message((MessageType)header[0], data);
    }
    public static Message NewErrorMessage(string errorText) {
        return new(MessageType.ResponseError,
                   Encoding.UTF8.GetBytes(errorText));
    }
    public (string first, string second) Split(string splitter) {
        string textData = Encoding.UTF8.GetString(Data.ToArray());
        int i = textData.IndexOf(splitter);
        string first = textData[..i];
        string second = textData[(i + 1)..];
        return (first, second);
    }
    public string GetString() {
        return Encoding.UTF8.GetString(Data.ToArray());
    }
}
