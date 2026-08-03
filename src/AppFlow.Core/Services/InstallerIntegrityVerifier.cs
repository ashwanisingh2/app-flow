namespace AppFlow.Core.Services;

using AppFlow.Core.Interfaces;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

/// <summary>
/// Streams SHA-256 verification and validates Authenticode through WinVerifyTrust.
/// </summary>
public sealed class InstallerIntegrityVerifier : IInstallerIntegrityVerifier
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<bool> VerifySha256Async(
        string location,
        string expectedHash,
        CancellationToken ct = default)
    {
        var normalizedHash = NormalizeHash(expectedHash);
        if (normalizedHash.Length != 64)
            return false;

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(normalizedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        await using var stream = await OpenReadAsync(location, ct).ConfigureAwait(false);
        using var sha256 = SHA256.Create();
        var actual = await sha256.ComputeHashAsync(stream, ct).ConfigureAwait(false);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public bool HasTrustedAuthenticodeSignature(string filePath)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(filePath))
            return false;

        return AuthenticodeTrust.IsTrusted(filePath);
    }

    private static async Task<Stream> OpenReadAsync(string location, CancellationToken ct)
    {
        if (Uri.TryCreate(location, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            var response = await HttpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return new ResponseStream(response, await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false));
        }

        return new FileStream(
            location,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
    }

    private static string NormalizeHash(string hash) =>
        hash.Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("AppFlow", "1.0"));
        return client;
    }

    private sealed class ResponseStream : Stream
    {
        private readonly HttpResponseMessage _response;
        private readonly Stream _inner;

        public ResponseStream(HttpResponseMessage response, Stream inner)
        {
            _response = response;
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
            _response.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private static class AuthenticodeTrust
    {
        private static readonly Guid GenericVerifyV2 =
            new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

        public static bool IsTrusted(string filePath)
        {
            var filePathPtr = Marshal.StringToCoTaskMemUni(filePath);
            var fileInfoPtr = IntPtr.Zero;
            var trustDataPtr = IntPtr.Zero;

            try
            {
                var fileInfo = new WinTrustFileInfo
                {
                    StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
                    FilePath = filePathPtr
                };
                fileInfoPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustFileInfo>());
                Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

                var trustData = new WinTrustData
                {
                    StructSize = (uint)Marshal.SizeOf<WinTrustData>(),
                    UIChoice = 2,             // WTD_UI_NONE
                    RevocationChecks = 0,     // WTD_REVOKE_NONE
                    UnionChoice = 1,          // WTD_CHOICE_FILE
                    FileInfo = fileInfoPtr,
                    StateAction = 0,          // WTD_STATEACTION_IGNORE
                    ProviderFlags = 0x00000010 // WTD_SAFER_FLAG
                };
                trustDataPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustData>());
                Marshal.StructureToPtr(trustData, trustDataPtr, false);

                return WinVerifyTrust(new IntPtr(-1), GenericVerifyV2, trustDataPtr) == 0;
            }
            finally
            {
                if (trustDataPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(trustDataPtr);
                if (fileInfoPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(fileInfoPtr);
                Marshal.FreeCoTaskMem(filePathPtr);
            }
        }

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
        private static extern int WinVerifyTrust(
            IntPtr windowHandle,
            [MarshalAs(UnmanagedType.LPStruct)] Guid actionId,
            IntPtr trustData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo
        {
            public uint StructSize;
            public IntPtr FilePath;
            public IntPtr FileHandle;
            public IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustData
        {
            public uint StructSize;
            public IntPtr PolicyCallbackData;
            public IntPtr SipClientData;
            public uint UIChoice;
            public uint RevocationChecks;
            public uint UnionChoice;
            public IntPtr FileInfo;
            public uint StateAction;
            public IntPtr StateData;
            public IntPtr UrlReference;
            public uint ProviderFlags;
            public uint UIContext;
        }
    }
}
