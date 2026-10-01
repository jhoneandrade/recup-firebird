# Changelog

Todas as alterações notáveis neste projeto serão documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/),
e este projeto adere ao [Versionamento Semântico](https://semver.org/lang/pt-BR/).

---

## [1.2.1] - 2026-09-30

### Adicionado
- **Snapshot e Gerenciamento Inteligente de Serviços**:
  - Detecção dinâmica via Windows Service Controller dos serviços em execução antes de iniciar a recuperação.
  - Parada coordenada dos serviços e aplicações clientes que consom o banco antes de parar o serviço do Firebird.
  - Finalização de processos zumbis (`fb_inet_server.exe` e processos do ERP) para garantir liberação imediata dos handles do arquivo `.IB` / `.FDB`.
  - Mecanismo de re-tentativa na renomeação (3 tentativas com espera e limpeza de handles) para evitar falhas por travamentos transitórios do sistema operacional.
  - **Restauração Seletiva**: Ao concluir ou em caso de rollback, o sistema reinicia exclusivamente os serviços que estavam originalmente ativos naquela máquina específica, sem tentar iniciar serviços inexistentes ou desativados.
- **Feedback Visual Aprimorado**: Atualização imediata do rótulo de status em caso de falha de renomeação (`⚠️ Interrompido por segurança: banco em uso por outro processo.`) e diálogo de confirmação detalhado.

---

## [1.2.0] - 2026-09-28

### Adicionado
- **Interface Gráfica Dark Mode**: Interface compacta desenvolvida em Windows Forms com visual moderno, livre de bordas clássicas pesadas e adaptada para resoluções padrão.
- **Terminal com Abas e Filtro Inteligente**:
  - `Todos os Logs`: Saída detalhada completa do Firebird.
  - `! Somente Erros`: Isolamento exclusivo de corrupções reais de páginas (`bad checksum`, `wrong page type`, etc.).
  - `Etapas do Processo`: Resumo cronológico das 7 etapas da rotina.
- **Fluxo de Recuperação Não Destrutivo**:
  - Preservação da base original na pasta de origem com timestamp (`Nome_AAAA-MM-DD_HH-mm-ss.IB`).
  - Execução dos reparos físicos (`gfix -v -full` e `gfix -mend`) e lógicos (`gbak -b` e `gbak -c`) em cópia isolada de trabalho.
  - Substituição segura do banco final apenas após validação bem-sucedida.
- **Verificação Prévia de Espaço em Disco**: Cálculo automático do espaço necessário (~3.0x o tamanho da base) antes de iniciar qualquer operação.
- **Persistência Criptografada (AES-128)**: Opção de salvar credenciais e caminho de banco localmente com criptografia simétrica AES-128-CBC (`recupbd.cfg`).
- **Gerenciamento de Serviços Windows**: Parada e reinicialização automática dos serviços do Firebird Server (`FirebirdServer`, `FirebirdGuardian`).
- **Descoberta Inteligente de Ferramentas**: Localização automática dos binários `gbak.exe` e `gfix.exe` no Registro do Windows e em diretórios padrão de instalação do Firebird (versões 2.0, 2.1, 2.5, 3.0, 4.0 e 5.0).
- **Diálogos Nativos no Tema Escuro (`DarkMessageBox`)**: Janelas modais integradas ao esquema visual escuro com confirmação prévia resumida.
- **Janela Sobre o Desenvolvedor**: Card de identificação com versão, build e atalhos para perfis no LinkedIn e GitHub.
- **Automação de Build**: Script `build.bat` para compilação nativa em 1 clique via `csc.exe` do .NET Framework 4.0 e `build.py` com patch de idioma de recursos PE para `0x0416` (pt-BR).

### Modificado
- Higienização total de credenciais padrão: campos de usuário, senha e caminho iniciam vazios em ambientes novos.
- Remoção de falsos positivos na contagem de erros durante a restauração de metadados e índices do `gbak`.
- Remoção de binários compilados e ferramentas de terceiros da raiz do repositório Git, disponibilizando o executável via GitHub Releases.

### Segurança
- Injeção de credenciais via variáveis de ambiente em memória de processo (`ISC_USER`, `ISC_PASSWORD`), prevenindo exposição em listas de processos do sistema ou logs de linha de comando.
- Configuração de `.gitignore` rigoroso protegendo bases de dados (`*.IB`, `*.FDB`, `*.FBK`, `*.GDB`), configurações locais e scripts legados.
