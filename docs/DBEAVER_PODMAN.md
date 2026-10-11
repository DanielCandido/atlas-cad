# Acesso ao PostgreSQL pelo DBeaver no Windows

O PostgreSQL do ambiente de teste roda dentro da Podman Machine. Em algumas
instalações com WSL, a porta publicada em `127.0.0.1:5432` aceita a conexão TCP,
mas o `wslrelay` não encaminha o protocolo PostgreSQL. Nesse caso, use um túnel
SSH até a Podman Machine.

Esse túnel é independente do Cloudflare Tunnel e não publica o banco na
Internet.

## 1. Iniciar o ambiente

Na raiz do repositório:

```powershell
podman machine start

podman compose --env-file infra/.env.tunnel `
  -f infra/compose.tunnel.yaml `
  -f infra/compose.debug.yaml `
  up -d
```

Confirme que o banco está saudável:

```powershell
podman inspect atlas-cad-tunnel-postgres-1 `
  --format "{{.State.Health.Status}}"
```

## 2. Obter os dados atuais do túnel

O IP privado do container e a porta SSH da Podman Machine podem mudar depois de
uma recriação ou reinicialização. Consulte-os antes de configurar o DBeaver:

```powershell
podman inspect atlas-cad-tunnel-postgres-1 `
  --format "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}"

podman system connection list
```

Use a conexão `podman-machine-default-root`. No ambiente atual, os valores são:

```text
IP privado do PostgreSQL: 10.89.2.2
Host SSH:                  127.0.0.1
Porta SSH:                 61811
Usuário SSH:               root
Chave privada:             C:\Users\Cobe Tecnologia\.local\share\containers\podman\machine\machine
```

## 3. Configurar o DBeaver

Crie uma conexão PostgreSQL. Na aba principal, informe:

```text
Host:     IP privado obtido com podman inspect
Porta:    5432
Database: valor de POSTGRES_DB em infra/.env.tunnel
Usuário:  valor de POSTGRES_USER em infra/.env.tunnel
Senha:    valor de POSTGRES_PASSWORD em infra/.env.tunnel
SSL mode: disable
```

Na aba **SSH**, habilite **Use SSH Tunnel** e informe:

```text
Host/IP:      127.0.0.1
Porta:        porta da conexão podman-machine-default-root
Usuário:      root
Autenticação: Public Key
Private key:  caminho Identity exibido por podman system connection list
Passphrase:   vazio
```

Teste primeiro o túnel SSH e depois a conexão PostgreSQL.

## 4. Alternativa com túnel manual

Também é possível abrir o túnel em um PowerShell e manter a janela aberta:

```powershell
ssh -N `
  -L 15432:10.89.2.2:5432 `
  -p 61811 `
  -i "C:\Users\Cobe Tecnologia\.local\share\containers\podman\machine\machine" `
  -o StrictHostKeyChecking=no `
  -o UserKnownHostsFile=NUL `
  root@127.0.0.1
```

Substitua o IP e a porta pelos valores atuais. Enquanto esse comando estiver em
execução, configure o DBeaver sem SSH:

```text
Host: 127.0.0.1
Porta: 15432
```

Para fechar o túnel manual, pressione `Ctrl+C` no PowerShell. O fechamento do
túnel não interrompe o PostgreSQL nem os demais containers.

## 5. Diagnóstico

Se o DBeaver apresentar timeout:

1. confirme que a Podman Machine está em execução;
2. confirme que o container está `healthy`;
3. consulte novamente o IP privado e a porta SSH;
4. verifique se a chave privada selecionada é a da conexão root;
5. confirme que SSH, proxy ou SSL antigos não ficaram habilitados na conexão.

Não publique a porta `5432` no roteador nem crie uma rota para ela no
Cloudflare Tunnel.
