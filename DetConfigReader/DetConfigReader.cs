using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Telnet;

namespace DetConfigReader
{
    public class DetConfigReader
    {
        static CancellationTokenSource cts = new CancellationTokenSource();
        static Stopwatch sw = new();
        public static async Task<string> TelnetDetInfo(string DetIp)
        {
            using TelnetConnection client = new TelnetConnection(DetIp, 23);
            string remoteFilePath = @"/media/card/apps/detector.config";
            var TimeoutMs = 1000;
            sw.Restart();
            client.Login("root", "root", TimeoutMs);//.Should().Be(true);
            Console.WriteLine($" {sw.ElapsedMilliseconds} login ");
            sw.Restart();
            // Supress Echo on client:
            client.WriteByte(0xFF);    // IAC
            client.WriteByte(0xFE);    // Dont
            client.WriteByte(0x01);    // ECHO

            //// Enable again with:
            //client.WriteByte(0xFF);    // IAC
            //client.Write(0xFC);    // WONT
            //client.Write(0x01);    // ECHO
            var sxCmd = "stty cols 200";
            client.WriteLine(sxCmd);
            Console.WriteLine(">" + sxCmd);
            Console.WriteLine();
            Console.WriteLine($" {sw.ElapsedMilliseconds} stty ");

            var lx = await client.ReadLine(default);
            Console.WriteLine($" {sw.ElapsedMilliseconds} readln ");
            Console.WriteLine(lx.sx);
            sxCmd = "[ -d /mnt/sd ] && echo \"/mnt/sd\"" + " || "
                + "[ -d /mnt/flash ] && echo \"/mnt/flash\"" + " || "
                + "echo \"/media/card\"";
            sw.Restart();
            client.WriteLine(sxCmd);
            Console.WriteLine(">" + sxCmd);
            Console.WriteLine();

            lx = await client.ReadLine(default);
            Console.WriteLine($" {sw.ElapsedMilliseconds} path ");
            Console.WriteLine(lx.sx);
            Console.WriteLine();
            sw.Restart();
            lx = await client.ReadLine(default);
            Console.WriteLine($" {sw.ElapsedMilliseconds} path 1 ");
            var StoragePath = lx.sx.Trim();
            Console.WriteLine($"Path = { lx.sx}");
            Console.WriteLine();
            sw.Restart();
            lx = await client.ReadToPrompt("# ",default);
            Console.WriteLine(lx.sx);
            Console.WriteLine($" {sw.ElapsedMilliseconds} copy next ");
            sw.Restart();
            var bytes = await CopyFileBytesFromDetector(client, remoteFilePath);
            Console.WriteLine($" {sw.ElapsedMilliseconds} copy done ");
            string asciiText = Encoding.UTF8.GetString(bytes.AsSpan());//, 0, nBytes);
            Console.WriteLine($" {sw.ElapsedMilliseconds} ascii ");
            return asciiText;
        }
        public static async Task<ImmutableArray<byte>> CopyFileBytesFromDetector(
    TelnetConnection client,
    string remoteFilePath)
        {
            var result = ImmutableArray.CreateBuilder<byte>();// new List<Byte>();
            var blocks = CopyFromDetector(//ServerIp,
                client, remoteFilePath);
            await foreach (var b in blocks)
            {
                result.AddRange(b);
                ArrayPool<byte>.Shared.Return(b.Array);
            }
            return result.ToImmutable();
        }
        static async IAsyncEnumerable<ArraySegment<byte>> CopyFromDetector(
            TelnetConnection client,
            //string ServerIp, 
            string remoteFilePath)
        {
            //https://stackoverflow.com/questions/7875540/how-to-write-multiple-line-string-using-bash-with-variables
            //https://stackoverflow.com/questions/27920806/how-to-avoid-heredoc-expanding-variables
            {
                //    using (TelnetConnection client = new TelnetConnection(ServerIp, 23))
                {
                    bool eof, txo;
                    string command, reply;
                    byte[] buffer = new byte[16];
                    //client.IsConnected.Should().Be(true);
//flush
                    //while (true)
                    //{
                    //    (eof, txo, reply) = await client.ReadLine(cts.Token);
                    //    if (txo) break;
                    //    //!String.IsNullOrEmpty(reply = await client.ReadAsync(TimeSpan.FromMilliseconds(100))))
                    //    Console.WriteLine(reply);
                    //}
                    //

                    Console.WriteLine($" {sw.ElapsedMilliseconds} cb 1 ");

                    command = $"hd {remoteFilePath};echo ALLDONENOW";
                    client.WriteLine(command);
                    Console.WriteLine($" {sw.ElapsedMilliseconds} cb 2 ");
                    (_, _, reply) = await client.ReadLine(cts.Token);
                    Console.WriteLine(reply);
                    Console.WriteLine($" {sw.ElapsedMilliseconds} cb 3 ");

                    var lines = LinesFrom(client);
                    await foreach (var replyLine in lines)
                    {
                        var rentalArray = ArrayPool<byte>.Shared.Rent(16);
                        int lineBytes = getbytes(rentalArray.AsSpan(), replyLine);
                        var rental = new ArraySegment<byte>(rentalArray, 0, lineBytes);
                        yield return rental;
                    }
                    Console.WriteLine($" {sw.ElapsedMilliseconds} cb 4 ");
                    //Use Channels
                    //client.WriteLine("\u0004");
                    //while (true)
                    //{
                    //    (eof, txo, reply) = await client.ReadLine(cts.Token);
                    //    if (txo) break;
                    //    //while (!String.IsNullOrEmpty(reply = await client.TerminatedReadAsync("\n", TimeSpan.FromMilliseconds(10))))
                    //    Console.WriteLine(reply);
                    //}
                }
            }
        }
        static async IAsyncEnumerable<string> LinesFrom(TelnetConnection client)
        {
            string reply = "";
            bool eof, txo;
            while (reply != "ALLDONENOW")
            {
                (eof, txo, reply) = await client.ReadLine(cts.Token);
                yield return reply;
                if (txo) break;
                if (eof) break;
                if (reply.Contains("ALLDO"))
                {
                    if (eof) break;
                }
            }
            while (reply != "ALLDONENOW")
            {
                if (reply == "") break;

            }
        }
        static int getbytes(Span<byte> buffer, string replyLine)
        {
            int lineBytes = 0;
            buffer.Clear();
            for (int nb = 0; nb < 8; nb++)
            {
                int startIndex = 8 + 2 + 3 * nb;
                if (startIndex + 2 > replyLine.Length) break;
                var sxHex = replyLine.Substring(startIndex, 2);
                bool ok = byte.TryParse(sxHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out byte value);
                if (!ok) break;
                buffer[lineBytes] = value;
                lineBytes++;
            }
            if (lineBytes != 8) return lineBytes;
            for (int nb = 0; nb < 8; nb++)
            {
                int startIndex = 8 + 2 + 8 * 3 + 1 + 3 * nb;
                if (startIndex + 2 > replyLine.Length) break;
                var sxHex = replyLine.Substring(startIndex, 2);
                bool ok = byte.TryParse(sxHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture.NumberFormat, out byte value);
                if (!ok) break;
                buffer[lineBytes] = value;
                lineBytes++;
            }
            return lineBytes;
        }
    }
    class PipeServer
    {
        static
        int
        Main(string[] args)
        {
            if (args.Length < 2
            || (System.String.Compare(args[0], "in") != 0
            && System.String.Compare(args[0], "out") != 0))
            {
                System.Console.WriteLine("Usage: PipeServer <in | out> <process> <args>");

                return 1;
            }

            ///////////////////////////////////
            // // // Process arguments // // //
            ///////////////////////////////////
            // Convert pipe direction
            System.IO.Pipes.PipeDirection pipe_dir = 0;
            if (System.String.Compare(args[0], "in") == 0)
            {
                pipe_dir = System.IO.Pipes.PipeDirection.In;
            }
            if (System.String.Compare(args[0], "out") == 0)
            {
                pipe_dir = System.IO.Pipes.PipeDirection.Out;
            }

            // Find process name to start
            string proc_name = args[1];

            // Build commandline argument string
            string proc_args = "";
            for (System.UInt16 i = 2; i < args.Length; i++)
            {
                if (args[i].IndexOf(" ") > -1)
                {
                    proc_args += "\"" + args[i].Replace("\"", "\\\"") + "\" ";
                }
                else
                {
                    proc_args += args[i] + " ";
                }
            }

            // Create server
            string pipe_name = "";
            System.IO.Pipes.NamedPipeServerStream pipe_stream = null;
            for (System.UInt16 i = 1; i < 65535; i++)
            {
                // Generate new pipe name
                pipe_name = "pipeserver_" + System.Convert.ToString(i);

                try
                {
                    // Start server
                    pipe_stream = new System.IO.Pipes.NamedPipeServerStream(pipe_name, pipe_dir, 1);

                    break;
                }
                catch (System.IO.IOException _)
                {
                    continue;
                }
            }
            if (pipe_stream == null)
            {
                System.Console.WriteLine("Could not create pipe");

                return 1;
            }

            // Make sure the process knows about the pipe name
            proc_args = proc_args.Replace("{pipe}", "\\\\.\\pipe\\" + pipe_name);

            // Run process
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = proc_name;
            proc.StartInfo.Arguments = proc_args;
            proc.Start();

            // Connect pipes and wait until EOF
            pipe_stream.WaitForConnection();
            try
            {
                if (pipe_dir == System.IO.Pipes.PipeDirection.In)
                {
                    pipe_stream.CopyTo(System.Console.OpenStandardOutput());
                }
                if (pipe_dir == System.IO.Pipes.PipeDirection.Out)
                {
                    System.Console.OpenStandardInput().CopyTo(pipe_stream);
                }
            }
            catch (System.IO.IOException e)
            {
                System.Console.WriteLine("error: {0}", e.Message);

                return 1;
            }

            // Wait for process termination
            while (!proc.HasExited)
            {
                proc.WaitForExit();
            }

            // Return correct exit code
            return proc.ExitCode;
        }
    }
}
