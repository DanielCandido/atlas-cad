# Segurança: decisões e limites

- Nunca distribuir arquivos-fonte LSP/MLN privados no instalador.
- Não distribuir chave privada de assinatura no plugin.
- Identificador de hardware não é segredo; preferir chave privada protegida por TPM quando possível.
- Autorização offline curta (meta 72h) e tokens assinados. Revogação imediata offline é impossível.
- Estilos MLINE persistidos em DWG podem ser inspecionados; criptografia do catálogo não impede extração do desenho.
- Criptografia de catálogo ainda **não implementada**. Não afirmar resistência a engenharia reversa.
- Usar HTTPS, rate limits, logs auditáveis e validação de webhooks quando forem implementados.
- `.env` e segredos não entram no controle de versão.
