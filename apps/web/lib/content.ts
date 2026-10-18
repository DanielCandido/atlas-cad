export const product = {
  name: 'MLINES',
  eyebrow: 'BIBLIOTECA ATLAS CAD',
  description:
    'Automatize a representação de perfis metálicos no AutoCAD com um fluxo pensado para quem projeta.',
  features: [
    'Conversão de linhas e polilinhas em MLINE',
    'Associação de identificação de perfis no desenho',
    'Estilos para representação em planta e corte',
    'Padronização do fluxo de detalhamento',
  ],
  requirements: [
    'Windows 10 ou 11 de 64 bits',
    'AutoCAD 2022 a 2026 detectado automaticamente pelo instalador',
    'Internet para ativação e renovação periódica da licença',
  ],
};
export const docs = [
  {
    slug: 'visao-geral',
    title: 'Visão geral',
    group: 'Comece aqui',
    summary: 'O que é o MLINES e como funciona o fluxo completo.',
    body: [
      [
        'O que é',
        'O MLINES é a biblioteca do ATLAS CAD para automatizar representações de perfis metálicos no AutoCAD.',
      ],
      [
        'Fluxo completo',
        'Você assina pelo site, aguarda a confirmação do Asaas e executa o instalador, que detecta o AutoCAD e conduz a ativação segura.',
      ],
      [
        'Compatibilidade',
        'Um único instalador atende ao AutoCAD 2022–2026 no Windows de 64 bits e seleciona automaticamente os componentes compatíveis.',
      ],
    ],
  },
  {
    slug: 'compra-e-conta',
    title: 'Compra e área do cliente',
    group: 'Primeiros passos',
    summary: 'Assinatura, pagamento, download e gerenciamento do plano.',
    body: [
      [
        'Criar a assinatura',
        'Na página Assinar, informe seus dados, escolha o plano mensal ou anual e selecione cartão de crédito, PIX ou boleto. No cartão, informe também telefone e endereço de cobrança exigidos pelo Asaas e conclua no checkout seguro hospedado; o ATLAS CAD não recebe os dados do cartão. Se você já possui uma conta, entre na Área do Cliente para reabrir ou gerar uma nova cobrança.',
      ],
      [
        'Liberação',
        'A criação do checkout ou da cobrança não libera o produto. O download aparece automaticamente na Área do Cliente somente após o webhook de pagamento confirmado ou recebido.',
      ],
      [
        'Gerenciar o plano',
        'Na Área do Cliente você consulta a vigência, reabre ou recria um checkout, abre cobranças pendentes, troca de plano e cancela a assinatura. O cancelamento interrompe renovações e revoga a ativação para futuras validações.',
      ],
    ],
  },
  {
    slug: 'instalacao',
    title: 'Instalação e ativação',
    group: 'Primeiros passos',
    summary: 'Passo a passo desde o instalador até o primeiro uso no AutoCAD.',
    body: [
      [
        'Baixar e instalar',
        'Feche o AutoCAD, baixe Instalador-AtlasCad.exe na Área do Cliente e execute-o. O instalador detecta as versões compatíveis, abre o vínculo seguro com sua conta e instala somente os componentes necessários.',
      ],
      [
        'Desinstalar',
        'Feche o AutoCAD e baixe Desinstalar-AtlasCad.exe na Área do Cliente. O desinstalador remove o plugin e preserva os dados locais da licença para uma futura reinstalação.',
      ],
    ],
  },
  {
    slug: 'licenciamento',
    title: 'Licenciamento',
    group: 'Referência',
    summary: 'Vínculo por dispositivo, renovação e funcionamento offline.',
    body: [
      [
        'Vínculo seguro',
        'A licença é vinculada a uma chave criptográfica não exportável criada para o usuário atual do Windows. Copiar arquivos para outro computador não copia a licença.',
      ],
      [
        'Uso offline',
        'Após uma validação online bem-sucedida, os recursos protegidos funcionam por até 72 horas sem conexão. O plugin tenta renovar automaticamente nas últimas 24 horas; o comando ATLAS_LICENSE_RENEW também força uma tentativa.',
      ],
      [
        'Troca de máquina',
        'Uma assinatura admite uma máquina ativa. Na Área do Cliente, desative a máquina atual e execute o instalador no novo Windows para iniciar outro vínculo seguro.',
      ],
    ],
  },
  {
    slug: 'primeiro-uso',
    title: 'Primeiro uso',
    group: 'Uso',
    summary: 'Valide a instalação e carregue o catálogo protegido.',
    body: [
      [
        'Diagnóstico',
        'Execute ATLAS_DIAGNOSTICS e confirme que adaptador, licença e catálogo aparecem como válidos.',
      ],
      [
        'Catálogo',
        'Execute ATLAS_INSTALL_CATALOG após a ativação. A chave do catálogo é entregue apenas a uma máquina com assinatura válida.',
      ],
      [
        'Automação',
        'Abra uma cópia do desenho, selecione as entidades de eixo e use os comandos do MLINES. Confira estilo, geometria e layers antes de salvar.',
      ],
    ],
  },
  {
    slug: 'solucao-de-problemas',
    title: 'Solução de problemas',
    group: 'Suporte',
    summary: 'Respostas para os erros mais comuns.',
    body: [
      [
        'Comando não aparece',
        'Feche o AutoCAD e execute novamente o instalador unificado. Confirme que um bundle AtlasCad existe em %APPDATA%\\Autodesk\\ApplicationPlugins. O carregamento é automático; NETLOAD deve ser usado somente para diagnóstico.',
      ],
      [
        'Pagamento confirmado sem download',
        'Atualize a Área do Cliente após alguns minutos. Se continuar bloqueado, informe ao suporte o e-mail da conta e o identificador da cobrança — nunca envie senha ou código completo de ativação.',
      ],
      [
        'Licença expirada',
        'Conecte o computador à internet e execute ATLAS_LICENSE_RENEW. Verifique também se data e hora do Windows estão corretas.',
      ],
    ],
  },
] as const;
export function getDoc(slug: string) {
  return docs.find((d) => d.slug === slug);
}
