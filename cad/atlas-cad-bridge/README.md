# ATLAS CAD Bridge — prova de conceito

Este adaptador C# demonstra a **criação de MLINE dentro do AutoCAD** por meio da API gerenciada, usando um estilo já registrado no DWG. Não importa MLN ainda e **não é um produto comercial**.

## Preparação
1. No Windows com AutoCAD e Visual Studio, referencie `AcMgd.dll`, `AcDbMgd.dll` e `AcCoreMgd.dll` da instalação apropriada. `Copy Local = false`.
2. Use o build .NET Framework 4.8 para AutoCAD 2022–2024 e .NET 8 para 2025–2026; valide com a documentação de sua versão.
3. Carregue a DLL usando `NETLOAD` e execute `ATLAS_MLINE_BRIDGE`.
4. O comando **bloqueia intencionalmente** enquanto `DevelopmentGate` não for substituído por validação real de licença. Para ensaios locais, faça isso em branch privado, jamais no pacote distribuído.
5. O estilo deve existir no DWG. A importação protegida de MLN é a próxima etapa, não está implementada.

### Próxima integração
- Criar `ATLAS_IMPORT_STYLE` que decodifica um catálogo autenticado, valida licença e registra `MlineStyle` em transação.
- Criar uma interface de comunicação com AutoLISP que receba pontos WCS, nome do estilo e flag de fechamento. O comando atual é interativo, não um bridge de produção.
- Somente após isso alterar as chamadas `SF-LoadLibrary` e `SF-DrawMline` no STEELFLOW. Não remova a verificação de sucesso antes de `entdel`.

**Nota:** não compilado nem testado no AutoCAD neste ambiente. Assinaturas e comportamento da API devem ser validados no SDK Autodesk instalado.
