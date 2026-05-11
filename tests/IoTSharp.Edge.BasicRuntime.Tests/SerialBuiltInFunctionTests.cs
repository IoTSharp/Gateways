using System.IO.Ports;
using System.Text;

namespace IoTSharp.Edge.BasicRuntime.Tests;

public sealed class SerialBuiltInFunctionTests
{
    [Fact]
    public void Runtime_can_open_write_read_and_close_a_serial_session()
    {
        var factory = new LoopbackSerialPortFactory();
        var runtime = new BasicRuntime(factory);
        var result = runtime.Execute("""
            port = SERIAL_OPEN("loopback", 115200, 8, "N", 1, nil, "rs485", 250, 250, "utf-8", "\n")
            if port = 0 then
              return "open failed: " + SERIAL_LAST_ERROR()
            endif

            if SERIAL_AVAILABLE(port) <> 0 then
              return "unexpected buffered data"
            endif

            if SERIAL_WRITE(port, "ping") <> 4 then
              return "write failed: " + SERIAL_LAST_ERROR(port)
            endif

            if SERIAL_AVAILABLE(port) <> 4 then
              return "available mismatch: " + STR(SERIAL_AVAILABLE(port))
            endif

            bytes = SERIAL_READ(port)
            if LEN(bytes) <> 4 then
              return "wrong byte count: " + STR(LEN(bytes))
            endif

            if bytes(0) <> 112 or bytes(1) <> 105 or bytes(2) <> 110 or bytes(3) <> 103 then
              return "wrong bytes"
            endif

            if SERIAL_WRITE_LINE(port, "ok") <> 3 then
              return "line write failed: " + SERIAL_LAST_ERROR(port)
            endif

            line = SERIAL_READ_LINE(port)
            if line <> "ok" then
              return "wrong line: " + line
            endif

            if SERIAL_CLOSE(port) = 0 then
              return "close failed: " + SERIAL_LAST_ERROR(port)
            endif

            return "ok"
            """);

        Assert.Equal("ok", result.ReturnValue);
        Assert.Single(factory.OpenedOptions);
        Assert.Equal("loopback", factory.OpenedOptions[0].PortName);
        Assert.Equal(115200, factory.OpenedOptions[0].BaudRate);
        Assert.Equal(8, factory.OpenedOptions[0].DataBits);
        Assert.Equal(Parity.None, factory.OpenedOptions[0].Parity);
        Assert.Equal(StopBits.One, factory.OpenedOptions[0].StopBits);
        Assert.Equal(Handshake.RequestToSend, factory.OpenedOptions[0].Handshake);
        Assert.Equal(BasicSerialBusMode.Rs485, factory.OpenedOptions[0].Mode);
        Assert.Equal(250, factory.OpenedOptions[0].ReadTimeoutMs);
        Assert.Equal(250, factory.OpenedOptions[0].WriteTimeoutMs);
        Assert.Equal("utf-8", factory.OpenedOptions[0].TextEncoding.WebName);
        Assert.Equal("\n", factory.OpenedOptions[0].NewLine);
    }

    [Fact]
    public void Runtime_exposes_last_serial_error()
    {
        var runtime = new BasicRuntime(new LoopbackSerialPortFactory());
        var result = runtime.Execute("""
            port = SERIAL_OPEN("loopback")
            if port = 0 then
              return "open failed"
            endif

            if SERIAL_CLOSE(port) = 0 then
              return "close failed"
            endif

            if SERIAL_CLOSE(port) = 0 then
              return SERIAL_LAST_ERROR()
            endif

            return "unexpected"
            """);

        Assert.Equal("未找到串口句柄。", result.ReturnValue);
    }

    private sealed class LoopbackSerialPortFactory : IBasicSerialPortFactory
    {
        public List<BasicSerialPortOptions> OpenedOptions { get; } = [];

        public IBasicSerialPortSession Open(BasicSerialPortOptions options)
        {
            OpenedOptions.Add(options);
            return new LoopbackSerialPortSession(options);
        }
    }

    private sealed class LoopbackSerialPortSession : IBasicSerialPortSession
    {
        private readonly Queue<byte> _incoming = new();
        private int _disposed;

        public LoopbackSerialPortSession(BasicSerialPortOptions options)
        {
            Options = options;
        }

        public BasicSerialPortOptions Options { get; }

        public string PortName => Options.PortName;

        public bool IsOpen => Volatile.Read(ref _disposed) == 0;

        public int BytesToRead => IsOpen ? _incoming.Count : 0;

        public int BytesToWrite => 0;

        public Encoding TextEncoding => Options.TextEncoding;

        public string NewLine => Options.NewLine;

        public bool DtrEnabled { get; private set; }

        public bool RtsEnabled { get; private set; }

        public int Read(byte[] buffer, int offset, int count)
        {
            EnsureOpen();
            var read = 0;
            while (read < count && _incoming.Count > 0)
            {
                buffer[offset + read] = _incoming.Dequeue();
                read++;
            }

            return read;
        }

        public string ReadExisting()
        {
            EnsureOpen();
            return Decode(DrainIncoming());
        }

        public string ReadLine()
        {
            EnsureOpen();
            var newline = TextEncoding.GetBytes(NewLine);
            var buffer = new List<byte>();
            while (_incoming.Count > 0)
            {
                buffer.Add(_incoming.Dequeue());
                if (EndsWith(buffer, newline))
                {
                    buffer.RemoveRange(buffer.Count - newline.Length, newline.Length);
                    break;
                }
            }

            return Decode(buffer.ToArray());
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            EnsureOpen();
            for (var index = 0; index < count; index++)
            {
                _incoming.Enqueue(buffer[offset + index]);
            }
        }

        public void Write(string text)
        {
            EnsureOpen();
            var bytes = TextEncoding.GetBytes(text);
            Write(bytes, 0, bytes.Length);
        }

        public void WriteLine(string text)
        {
            EnsureOpen();
            var bytes = TextEncoding.GetBytes(text + NewLine);
            Write(bytes, 0, bytes.Length);
        }

        public void DiscardInBuffer()
            => _incoming.Clear();

        public void DiscardOutBuffer()
        {
        }

        public void SetDtrEnable(bool enabled)
            => DtrEnabled = enabled;

        public void SetRtsEnable(bool enabled)
            => RtsEnabled = enabled;

        public void Close()
        {
            Interlocked.Exchange(ref _disposed, 1);
        }

        public void Dispose()
            => Close();

        private string Decode(byte[] bytes)
            => bytes.Length == 0 ? string.Empty : TextEncoding.GetString(bytes);

        private byte[] DrainIncoming()
        {
            var bytes = _incoming.ToArray();
            _incoming.Clear();
            return bytes;
        }

        private static bool EndsWith(IReadOnlyList<byte> bytes, IReadOnlyList<byte> suffix)
        {
            if (suffix.Count == 0 || bytes.Count < suffix.Count)
            {
                return false;
            }

            for (var index = 0; index < suffix.Count; index++)
            {
                if (bytes[bytes.Count - suffix.Count + index] != suffix[index])
                {
                    return false;
                }
            }

            return true;
        }

        private void EnsureOpen()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Loopback port is closed.");
            }
        }
    }
}
