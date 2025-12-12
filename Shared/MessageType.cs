namespace RemoteFileSystem.Shared;

public enum MessageType : byte
{
    RequestLogin = 0b000, // {username}\n{password}
    RequestChangeDirectory = 0b001,
    RequestDownload = 0b010,
    RequestUpload = 0b011, // {path}\n{file_contents}
    ResponseCommandOutput = 0b100,
    ResponseFile = 0b101,
    ResponseError = 0b110,
    ResponseLoginSuccess = 0b111,
    RequestPrintDirectory = 0b1000,
    RequestMakeDirectory = 0b1001,
    RequestList = 0b1010,
    RequestRemoveFile = 0b1011,
    RequestRemoveRecursive = 0b1100,
    RequestCopy = 0b1101,
    RequestCopyRecursive = 0b1110,
}
