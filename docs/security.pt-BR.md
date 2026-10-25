# Decisões e limites de segurança

[English](security.md)

- Nunca distribuir fontes LSP/MLN privadas, chaves privadas de assinatura do
  servidor, chaves administrativas da API ou códigos de ativação em texto claro
  no controle de versão ou no pacote do cliente.
- O M4 usa assinaturas ECDSA P-256; não existe gerador determinístico de chaves,
  proteção XOR ou segredo universal incorporado no cliente.
- A chave privada por dispositivo é uma chave Windows CNG nomeada e não
  exportável. O servidor armazena apenas a chave pública e seu fingerprint.
- O pareamento do instalador usa confirmação no navegador válida por dez minutos
  e provas ECDSA novas na criação, consulta e conclusão. A aprovação isolada não
  revoga a máquina anterior; substituição e ativação acontecem atomicamente
  quando o novo dispositivo prova possuir a chave privada. A conclusão é
  idempotente para permitir retry após perda de resposta.
- Entitlements são vinculados ao produto, ativação, geração e dispositivo e
  expiram após 72 horas. Estado local ausente, malformado, adulterado, copiado ou
  expirado é rejeitado pelas operações protegidas em C#.
- A transferência bloqueia futuras renovações da ativação anterior. Revogação
  imediata de uma máquina offline é impossível; o entitlement em cache continua
  válido até expirar.
- O M4 limita requisições em endpoints que alteram estado e compara a chave
  administrativa em tempo constante. Produção ainda exige HTTPS, segredos e
  chaves gerenciados, auditoria durável e banco transacional.
- O M5 criptografa e autentica o catálogo com AES-256-GCM. O cliente não contém
  chave de catálogo incorporada; um dispositivo ativo obtém uma chave versionada
  usando prova assinada e a armazena com Windows DPAPI do usuário atual.
- Nonces GCM são valores aleatórios de 96 bits gerados em cada build do pacote;
  o cabeçalho é autenticado como AAD. É proibido reutilizar o mesmo par
  chave/nonce.
- Estilos persistidos em DWG e plaintext presente em processo autorizado
  continuam inspecionáveis mesmo com o catálogo criptografado.
- O código do cliente pode sofrer engenharia reversa. A fronteira de segurança é
  a autorização curta do servidor combinada com a prova do dispositivo, não o
  sigilo do binário do plugin.

