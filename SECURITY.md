# Política de Segurança

A segurança e a integridade de dados são prioridades fundamentais neste projeto. Esta política orienta desenvolvedores, operadores e pesquisadores de segurança sobre como relatar vulnerabilidades ou falhas de forma responsável.

---

## Versões com Suporte

| Versão | Suportada | Notas |
|:---:|:---:|---|
| **1.2.x** | Sim | Versão ativa recomendada para uso em produção. |
| < 1.2.0 | Não | Versões legadas baseadas em scripts em lote não devem ser utilizadas. |

---

## Como Relatar uma Vulnerabilidade

Caso encontre uma vulnerabilidade de segurança (por exemplo, falha na manipulação de credenciais, risco de corrupção ou escape de processos):

1. **Não abra uma Issue pública** para relatar vulnerabilidades de segurança que possam colocar dados de usuários em risco.
2. Utilize o recurso oficial de **Divulgação Privada de Segurança (Private Vulnerability Reporting)** do GitHub:
   - Acesse a aba [Security / Advisories](https://github.com/jhoneandrade/recup-firebird/security/advisories) do repositório.
   - Clique em **"Report a vulnerability"**.
3. Como alternativa, envie uma mensagem direta ao autor através dos canais disponíveis no perfil do [GitHub](https://github.com/jhoneandrade) ou [LinkedIn](https://br.linkedin.com/in/jhone-andrade-b450ab164).

---

## O que Incluir no Relatório

Para acelerar a análise e correção, inclua:
- Uma descrição clara da vulnerabilidade.
- Passos detalhados para reproduzir o problema.
- Prova de conceito (PoC) ou trecho de código relevante, quando aplicável.
- O impacto potencial da falha (confidencialidade, integridade ou disponibilidade).

---

## Boas Práticas Recomendadas ao Operador

- **Privilégios Administrativos**: Execute a ferramenta apenas com privilégios de Administrador em máquinas confiáveis, pois a gestão de serviços exige acesso ao Service Control Manager (SCM).
- **Proteção do Arquivo de Configuração**: Quando a opção *"Salvar dados neste computador"* for utilizada, proteja o arquivo local `recupbd.cfg` com permissões adequadas de ACL no sistema operacional.
- **Validação de Backups**: Embora a ferramenta preserve a base original com timestamp antes de qualquer alteração, mantenha rotinas de backup secundárias regulares em ambiente de produção.
