using System;

// minimalistic telnet implementation
// conceived by Tom Janssens on 2007/06/06  for codeproject
//
// http://www.corebvba.be

// Modifications
//
// Date         Person      Description
// ==========   =========   =======================================================================
// 2013-06-06   jsagara     Implements IDisposable. Miscellaneous refactoring.
//
// Alex fiddle around to prevent the polling, which was really slow
//

using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Telnet
{
    public class TelnetConnection : IDisposable
    {
        private TcpClient tcpSocket;
        private int TimeoutMs = 100;

        public bool IsConnected
        {
            get
            {
                return tcpSocket.Connected;
            }
        }

        public TelnetConnection(string hostname, int port)
        {
            tcpSocket = new TcpClient(hostname, port);
        }

        ~TelnetConnection()
        {
            Dispose(false);
        }

        Stopwatch sw = new();
        public string Login(string username, string password, int loginTimeoutMs)
        {
            int oldTimeoutMs = TimeoutMs;
            TimeoutMs = loginTimeoutMs;

            //            sw.Restart();
            string s = Read(':');
            if (!s.TrimEnd().EndsWith(":"))
            {
                throw new Exception("Failed to connect : no login prompt");
            }
            //            Console.WriteLine($" {sw.ElapsedMilliseconds} ru ");

            WriteLine(username);
            //            Console.WriteLine($" {sw.ElapsedMilliseconds} wu ");

            sw.Restart();
            s += Read(':');
            if (!s.TrimEnd().EndsWith(":"))
            {
                throw new Exception("Failed to connect : no password prompt");
            }
            //            Console.WriteLine($" {sw.ElapsedMilliseconds} rp ");

            WriteLine(password);
            //            Console.WriteLine($" {sw.ElapsedMilliseconds} wp ");

            sw.Restart();
            s += Read('\n');
            //            Console.WriteLine($" {sw.ElapsedMilliseconds} login done ");

            TimeoutMs = oldTimeoutMs;

            return s;
        }

        public void WriteLine(string cmd)
        {
            Write(cmd + "\n");
        }

        public void WriteByte(byte b)
        {
            if (!tcpSocket.Connected)
            {
                return;
            }
            tcpSocket.GetStream().WriteByte(b);
        }
        public void Write(string cmd)
        {
            if (!tcpSocket.Connected)
            {
                return;
            }

            byte[] buf = ASCIIEncoding.ASCII.GetBytes(cmd.Replace("\0xFF", "\0xFF\0xFF"));
            tcpSocket.GetStream().Write(buf, 0, buf.Length);
        }
        public string Read(int terminator = 0x100)
        {
            if (!tcpSocket.Connected)
            {
                return null;
            }

            var sb = new StringBuilder();

            int ch = '\x0';
            do
            {
                int totDelay = 0;
                for (int nLoops = 0; nLoops < 20; nLoops++)
                {
                    do
                    {
                        ch = ParseTelnet(sb);
                    } while (tcpSocket.Available > 0);

                    int delayMs = ((sb.ToString()?.Trim() is not { Length: > 0 } sx) ? ch : sx[^1]) switch
                    {
                        0 => 100,
                        int c when (c == terminator)
                        => 5,
                        _ => 10,
                    };

                    if (totDelay > TimeoutMs)
                    {
                        break;
                    }
                    Thread.Sleep(delayMs);
                    totDelay += delayMs;
                }
            } while (tcpSocket.Available > 0);

            return sb.ToString();
        }
        private StringBuilder sb = new StringBuilder(256);
        public async Task<(bool eof, bool timeout, string sx)> ReadLine(CancellationToken ct)
        {
            if (!tcpSocket.Connected)
            {
                return (true, false, null);
            }

            sb.Clear();
            while (true)
            {
                var ch = await GetCh(ct);
                if (ch == -1) return (true, false, sb.ToString());
                if (ch == -2) return (false, true, sb.ToString());
                if (ch == '\r') continue;
                if (ch == '\n') break;
                sb.Append((char)ch);
            }
            return (false, false, sb.ToString());
        }
        public async Task<(bool eof, bool timeout, string sx)> ReadToPrompt(string promptEnd, CancellationToken ct)
        {
            if (!tcpSocket.Connected)
            {
                return (true, false, null);
            }

            sb.Clear();
            while (true)
            {
                var ch = await GetCh(ct);
                if (ch == -1) return (true, false, sb.ToString());
                if (ch == -2) return (false, true, sb.ToString());
                if (ch == '\r') continue;
                if (ch == '\n') break;
                sb.Append((char)ch);
                if (sb.ToString().EndsWith(promptEnd)) break;
            }
            return (false, false, sb.ToString());
        }
        public async ValueTask<int> GetCh(CancellationToken ct)
        {
            var buf = new byte[1];
            int nb;
            int TotalDelay = 0;
            while (tcpSocket.Available == 0)
            {
                if (TotalDelay > 500) return -2;
                await Task.Delay(10);
                TotalDelay += 10;
            }
            while (true)
            {
                TotalDelay = 0;
                while (tcpSocket.Available == 0)
                {
                    if (TotalDelay > 50) return -2;
                    await Task.Delay(10);
                    TotalDelay += 10;
                }
                //var ctsDelay = new CancellationTokenSource(5);
                //using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, ctsDelay.Token))
                nb = await tcpSocket.GetStream().ReadAsync(buf, ct);
                if (nb != 1) return -1;
                int input = buf[0];
                if (input != (int)Verbs.Iac) return input;
                //case (int)Verbs.Iac:
                // interpret as command
                int inputVerb = tcpSocket.GetStream().ReadByte();
                if (inputVerb == -1) return -1;
                switch (inputVerb)
                {
                    case (int)Verbs.Iac: return inputVerb;
                    // literal IAC = 255 escaped, so append char 255 to string

                    case (int)Verbs.Do:
                    case (int)Verbs.Dont:
                    case (int)Verbs.Will:
                    case (int)Verbs.Wont:
                        // reply to all commands with "WONT", unless it is SGA (suppres go ahead)
                        int inputoption = tcpSocket.GetStream().ReadByte();
                        if (inputoption == -1) return -1;

                        tcpSocket.GetStream().WriteByte((byte)Verbs.Iac);
                        if (inputoption == (int)Options.Sga)
                        {
                            tcpSocket.GetStream().WriteByte(inputVerb == (int)Verbs.Do ? (byte)Verbs.Will : (byte)Verbs.Do);
                        }
                        //if (inputoption == (int)Options.Echo)
                        //{
                        //    tcpSocket.GetStream().WriteByte(inputVerb == (int)Verbs.Do ? (byte)Verbs.Will : (byte)Verbs.Do);
                        //}
                        else
                        {
                            tcpSocket.GetStream().WriteByte(inputVerb == (int)Verbs.Do ? (byte)Verbs.Wont : (byte)Verbs.Dont);
                        }

                        tcpSocket.GetStream().WriteByte((byte)inputoption);
                        break;
                }
            }
        }
        private int ParseTelnet(StringBuilder sb)
        {
            int input = -2;
            while (tcpSocket.Available > 0)
            {
                input = tcpSocket.GetStream().ReadByte();
                switch (input)
                {
                    case -1:
                        break;

                    case (int)Verbs.Iac:
                        // interpret as command
                        int inputVerb = tcpSocket.GetStream().ReadByte();
                        if (inputVerb == -1)
                        {
                            break;
                        }

                        switch (inputVerb)
                        {
                            case (int)Verbs.Iac:
                                // literal IAC = 255 escaped, so append char 255 to string
                                input = inputVerb;
                                sb.Append(input);
                                break;

                            case (int)Verbs.Do:
                            case (int)Verbs.Dont:
                            case (int)Verbs.Will:
                            case (int)Verbs.Wont:
                                // reply to all commands with "WONT", unless it is SGA (suppres go ahead)
                                int inputoption = tcpSocket.GetStream().ReadByte();
                                if (inputoption == -1)
                                {
                                    break;
                                }

                                tcpSocket.GetStream().WriteByte((byte)Verbs.Iac);

                                if (inputoption == (int)Options.Sga)
                                {
                                    tcpSocket.GetStream().WriteByte(inputVerb == (int)Verbs.Do ? (byte)Verbs.Will : (byte)Verbs.Do);
                                }
                                else
                                {
                                    tcpSocket.GetStream().WriteByte(inputVerb == (int)Verbs.Do ? (byte)Verbs.Wont : (byte)Verbs.Dont);
                                }

                                tcpSocket.GetStream().WriteByte((byte)inputoption);
                                break;
                        }

                        break;

                    default:
                        sb.Append((char)input);
                        break;
                }
            }
            return input;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (tcpSocket != null)
                {
                    ((IDisposable)tcpSocket).Dispose();
                    tcpSocket = null;
                }
            }
        }


        #region Private Enums

        enum Verbs
        {
            Will = 251,
            Wont = 252,
            Do = 253,
            Dont = 254,
            Iac = 255
        }

        enum Options
        {
            Echo = 1,
            Sga = 3
        }

        #endregion
    }
}