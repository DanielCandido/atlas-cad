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
    'AutoCAD para Windows — compatibilidade 2022–2026 em validação',
    'Instalação do plugin e licença ativa para recursos protegidos',
  ],
};
export const docs = [
  {
    slug: 'visao-geral',
    title: 'Visão geral',
    group: 'Comece aqui',
    summary: 'O que é a biblioteca MLINES e como funciona o fluxo de trabalho.',
    body: [
      [
        'O que é o MLINES',
        'O MLINES é a primeira biblioteca do ATLAS CAD, voltada à automação de representações de perfis metálicos no AutoCAD.',
      ],
      [
        'Como funciona',
        'O usuário identifica os elementos do desenho e utiliza os comandos de automação para associar perfis e gerar representações MLINE.',
      ],
      [
        'Disponibilidade',
        'A versão comercial e os requisitos definitivos ainda dependem da validação do plugin e do sistema de licenciamento.',
      ],
    ],
  },
  {
    slug: 'requisitos',
    title: 'Requisitos do sistema',
    group: 'Comece aqui',
    summary: 'Ambiente e dependências previstas para instalação.',
    body: [
      [
        'Ambiente alvo',
        'O desenvolvimento está direcionado ao AutoCAD para Windows, com builds distintos para 2022–2024 e 2025–2026.',
      ],
      [
        'Verificação de compatibilidade',
        'A compatibilidade de cada versão precisa ser validada em ambiente real antes do lançamento comercial.',
      ],
      [
        'Licenciamento',
        'O modelo previsto vincula uma licença a um dispositivo e admite revalidação periódica.',
      ],
    ],
  },
  {
    slug: 'instalacao',
    title: 'Instalação',
    group: 'Primeiros passos',
    summary: 'Fluxo planejado de instalação e ativação.',
    body: [
      [
        'Baixar o instalador',
        'Quando o produto estiver disponível, o instalador será fornecido na área do cliente.',
      ],
      [
        'Ativar a licença',
        'A ativação será realizada pelo gerenciador de licenças vinculado à conta do comprador.',
      ],
      [
        'Carregar no AutoCAD',
        'O plugin deverá ser carregado no AutoCAD compatível. Os passos definitivos serão publicados após testes do instalador.',
      ],
    ],
  },
  {
    slug: 'primeiro-uso',
    title: 'Primeiro uso',
    group: 'Primeiros passos',
    summary: 'Entenda a conversão de entidades para MLINE.',
    body: [
      [
        'Preparar o desenho',
        'Organize as entidades de eixo e as identificações de perfis no desenho.',
      ],
      [
        'Executar a automação',
        'Selecione as entidades e siga o fluxo de identificação de perfis disponível na sua versão do plugin.',
      ],
      [
        'Conferir o resultado',
        'Verifique o estilo aplicado, a representação geométrica e os layers antes de prosseguir com o projeto.',
      ],
    ],
  },
  {
    slug: 'perfis-e-estilos',
    title: 'Perfis e estilos',
    group: 'Referência',
    summary: 'Como o catálogo de estilos é organizado.',
    body: [
      [
        'Biblioteca de estilos',
        'Os estilos de MLINE representam perfis e podem ter variantes para planta e corte.',
      ],
      [
        'Nomenclatura',
        'O mecanismo de normalização procura reconhecer variações de escrita dos identificadores de perfis.',
      ],
      [
        'Limitações',
        'O catálogo comercial completo e as regras exatas de correspondência serão documentados após a homologação.',
      ],
    ],
  },
  {
    slug: 'licenciamento',
    title: 'Licenciamento',
    group: 'Referência',
    summary: 'Ativação, uso offline e transferência.',
    body: [
      [
        'Ativação',
        'A arquitetura prevê autorização vinculada ao dispositivo e validação local de um token assinado.',
      ],
      [
        'Uso offline',
        'A política inicialmente proposta é de até 72 horas sem revalidação, sujeita à implementação e testes.',
      ],
      [
        'Transferência',
        'A transferência de licença deverá revogar a ativação anterior no servidor, respeitando o prazo máximo de autorização offline.',
      ],
    ],
  },
  {
    slug: 'solucao-de-problemas',
    title: 'Solução de problemas',
    group: 'Suporte',
    summary: 'Orientações iniciais para diagnosticar erros.',
    body: [
      [
        'O comando não aparece',
        'Confirme se o plugin foi instalado e carregado na versão correta do AutoCAD.',
      ],
      [
        'Perfil não encontrado',
        'Revise a nomenclatura informada e a disponibilidade do estilo no catálogo.',
      ],
      [
        'Licença inválida',
        'Confira a ativação do dispositivo, o prazo offline e a conectividade para revalidação.',
      ],
    ],
  },
] as const;
export function getDoc(slug: string) {
  return docs.find((d) => d.slug === slug);
}
